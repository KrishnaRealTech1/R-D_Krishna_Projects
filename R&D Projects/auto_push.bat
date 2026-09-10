@echo off
setlocal EnableExtensions
title R&D Daily Fast Exact Verified GitHub Backup v10.3

set "SELF=%~f0"
set "PS1=%TEMP%\rnd_daily_v10_3_%RANDOM%_%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$x=[IO.File]::ReadAllLines($env:SELF);$m=[Array]::IndexOf($x,'#==POWERSHELL==');if($m-lt 0){exit 2};$x[($m+1)..($x.Length-1)]|Set-Content -LiteralPath $env:PS1 -Encoding UTF8"
if errorlevel 1 goto FAILSTART

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
set "RC=%ERRORLEVEL%"
del /q "%PS1%" >nul 2>&1

if not "%RC%"=="0" (
  echo.
  echo ======================================================================
  echo EXACT BACKUP FAILED
  echo ======================================================================
  echo Your ORIGINAL laptop source folders were NOT intentionally modified.
  echo Check the error/log shown above. Nothing is called successful unless
  echo the local backup mirror was verified first.
  pause
  exit /b %RC%
)

echo.
echo Exact verified incremental backup finished. Closing in 8 seconds...
timeout /t 8 /nobreak >nul
exit /b 0

:FAILSTART
echo ERROR: Could not start backup engine.
pause
exit /b 1

#==POWERSHELL==
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# CONFIGURATION
# ---------------------------------------------------------------------------
$Repo   = 'https://github.com/KrishnaRealTech1/R-D_Krishna_Projects.git'
$Branch = 'master'
$Mirror = Join-Path $env:USERPROFILE 'G'

# These are the same four locations used by v9.
# Add another line here if another laptop folder must also be backed up.
$Sources = @(
  [pscustomobject]@{ Source=(Join-Path $env:USERPROFILE 'MPLABXProjects');       Dest='MPLABXProjects' },
  [pscustomobject]@{ Source=(Join-Path $env:USERPROFILE 'PycharmProjects');      Dest='PycharmProjects' },
  [pscustomobject]@{ Source=(Join-Path $env:USERPROFILE 'Documents\Arduino');   Dest='Arduino' },
  [pscustomobject]@{ Source='D:\R&D Projects';                                  Dest='R&D Projects' }
)

$MaxBatchBytes = 400MB
$MaxBatchFiles = 350
$LargeFileAt   = 90MB

# Fast mode is default. Set this to $true for any run where you want the old
# v10.2-style second full Robocopy verification scan for EVERY source tree.
$ForceFullVerify = $false

$RunStamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$LogDir = Join-Path $env:USERPROFILE 'RND_Backup_Logs'
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null

function Fail([string]$Message) { throw $Message }

function GitRun {
  param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
  & git.exe -c core.longpaths=true -c core.autocrlf=false -c core.safecrlf=false -C $Mirror @Args
  if ($LASTEXITCODE -ne 0) { Fail "Git failed: $($Args -join ' ')" }
}

function PushGit {
  for ($i=1; $i -le 4; $i++) {
    Write-Host "      Push attempt $i/4..."
    & git.exe -c core.longpaths=true -c core.autocrlf=false -C $Mirror push -u origin $Branch
    if ($LASTEXITCODE -eq 0) { return }
    if ($i -lt 4) {
      Write-Host '      Retrying in 20 seconds...'
      Start-Sleep -Seconds 20
    }
  }
  Fail 'GitHub push failed after 4 attempts.'
}

function CommitPush([string]$Message) {
  & git.exe -c core.longpaths=true -C $Mirror diff --cached --quiet
  if ($LASTEXITCODE -eq 0) { return $false }

  & git.exe -c core.longpaths=true -c core.autocrlf=false -C $Mirror commit -m $Message
  if ($LASTEXITCODE -ne 0) { Fail "Commit failed: $Message" }
  PushGit
  return $true
}

function PathFile([string[]]$Paths,[string]$File) {
  $Utf8 = New-Object Text.UTF8Encoding($false)
  $Stream = New-Object IO.FileStream($File,[IO.FileMode]::Create,[IO.FileAccess]::Write)
  try {
    foreach ($P in $Paths) {
      $Bytes = $Utf8.GetBytes($P)
      $Stream.Write($Bytes,0,$Bytes.Length)
      $Stream.WriteByte(0)
    }
  }
  finally { $Stream.Dispose() }
}

function RunRoboMirror([string]$Source,[string]$Dest,[string]$LogFile) {
  # FAST + SAFE strategy:
  #   1) Watch the SOURCE while Robocopy performs its normal full comparison/mirror.
  #   2) If the source stayed quiet and Robocopy returned only normal success bits
  #      (0..3), that same pass is sufficient: it compared the full tree and
  #      completed all required copies/deletions.
  #   3) If anything changed while Robocopy was running, or Robocopy reports a
  #      mismatch bit (4), v10.3 performs the same dry-run verification as v10.2.
  # This avoids a second complete directory walk on the usual stable daily run.

  $State = [hashtable]::Synchronized(@{ Changed=$false; Overflow=$false })
  $Watcher = New-Object System.IO.FileSystemWatcher
  $Watcher.Path = $Source
  $Watcher.IncludeSubdirectories = $true
  $Watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor
                          [System.IO.NotifyFilters]::DirectoryName -bor
                          [System.IO.NotifyFilters]::LastWrite -bor
                          [System.IO.NotifyFilters]::Size -bor
                          [System.IO.NotifyFilters]::CreationTime
  # Maximum supported by FileSystemWatcher on Windows. If the buffer still
  # overflows, Error marks the run for a full verification pass.
  $Watcher.InternalBufferSize = 65536

  $Tag = 'rndwatch_' + $PID + '_' + ([guid]::NewGuid().ToString('N'))
  $Ids = @(
    "${Tag}_changed", "${Tag}_created", "${Tag}_deleted",
    "${Tag}_renamed", "${Tag}_error"
  )

  try {
    Register-ObjectEvent -InputObject $Watcher -EventName Changed -SourceIdentifier $Ids[0] -MessageData $State -Action { $event.MessageData.Changed = $true } | Out-Null
    Register-ObjectEvent -InputObject $Watcher -EventName Created -SourceIdentifier $Ids[1] -MessageData $State -Action { $event.MessageData.Changed = $true } | Out-Null
    Register-ObjectEvent -InputObject $Watcher -EventName Deleted -SourceIdentifier $Ids[2] -MessageData $State -Action { $event.MessageData.Changed = $true } | Out-Null
    Register-ObjectEvent -InputObject $Watcher -EventName Renamed -SourceIdentifier $Ids[3] -MessageData $State -Action { $event.MessageData.Changed = $true } | Out-Null
    Register-ObjectEvent -InputObject $Watcher -EventName Error   -SourceIdentifier $Ids[4] -MessageData $State -Action {
      $event.MessageData.Changed = $true
      $event.MessageData.Overflow = $true
    } | Out-Null
    $Watcher.EnableRaisingEvents = $true

    # /MIR = add/update/delete so destination matches source.
    # /COPY:DAT and /DCOPY:DAT preserve file/dir data, attributes and timestamps.
    # /XJ prevents accidental traversal outside the selected tree through junctions.
    # .git folders are excluded because an outer Git repository cannot faithfully
    # store another repository's .git metadata as ordinary project files.
    # No /TEE: writing every Robocopy line to BOTH console and log wastes time.
    # /MT:32 speeds copying many small files on a typical SSD/NVMe system.
    & robocopy.exe $Source $Dest /MIR /COPY:DAT /DCOPY:DAT /R:3 /W:2 /XJ /XD .git /MT:32 /NP "/LOG:$LogFile" *> $null
    [int]$Code = $LASTEXITCODE

    # Let queued filesystem notifications update the synchronized state.
    Start-Sleep -Milliseconds 250

    if ($Code -ge 8) { Fail "ROBOCOPY failed for: $Source  (code $Code). Log: $LogFile" }
    return [pscustomobject]@{
      Code          = $Code
      SourceChanged = [bool]$State.Changed
      WatchOverflow = [bool]$State.Overflow
    }
  }
  finally {
    $Watcher.EnableRaisingEvents = $false
    foreach ($Id in $Ids) {
      Unregister-Event -SourceIdentifier $Id -ErrorAction SilentlyContinue
    }
    Get-Job -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "${Tag}*" } | Remove-Job -Force -ErrorAction SilentlyContinue
    $Watcher.Dispose()
  }
}

function VerifyRoboMirror([string]$Source,[string]$Dest,[string]$LogFile) {
  # A dry-run /MIR must report code 0. Any non-zero code means Robocopy still
  # sees something to copy/delete/change, so we do NOT call the backup verified.
  # IMPORTANT: Robocopy writes a 'Log File : ...' status line to stdout even
  # when /LOG is used. PowerShell functions return ALL stdout as function output,
  # which made v10.1 return an array like ('Log File : ...', 0) and falsely fail.
  # Suppress Robocopy console output here so this function returns ONE integer only.
  & robocopy.exe $Source $Dest /MIR /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /XD .git /L /NP /NJH /NJS /NFL /NDL "/LOG:$LogFile" *> $null
  [int]$Code = $LASTEXITCODE
  return $Code
}

function IsInside([string]$Child,[string]$Parent) {
  $C = [IO.Path]::GetFullPath($Child).TrimEnd('\') + '\'
  $P = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
  return $C.StartsWith($P,[StringComparison]::OrdinalIgnoreCase)
}

Write-Host ''
Write-Host '======================================================================'
Write-Host ' R&D DAILY FAST EXACT VERIFIED GITHUB BACKUP v10.3'
Write-Host '======================================================================'
Write-Host 'Local destination is rebuilt/synchronized from the laptop first.'
Write-Host 'GitHub receives ONLY Git changes after the local mirror verifies clean.'
Write-Host 'v10.3 keeps exact mirroring but skips the second full scan when the source stayed stable.'
if ($ForceFullVerify) { Write-Host 'FORCE FULL VERIFY is ON: every source will use the v10.2 double-scan method.' }
Write-Host 'If files change DURING backup, it automatically falls back to the full v10.2 verification pass.'
Write-Host ''
foreach ($S in $Sources) { Write-Host ("  {0}  ->  /{1}/" -f $S.Source,$S.Dest) }
Write-Host ''

Write-Host '[1/8] Safety checks...'
if (-not (Get-Command git.exe -ErrorAction SilentlyContinue)) { Fail 'Git for Windows was not found.' }
if (-not (Get-Command robocopy.exe -ErrorAction SilentlyContinue)) { Fail 'ROBOCOPY was not found.' }

$SeenDest = @{}
foreach ($S in $Sources) {
  if (-not (Test-Path -LiteralPath $S.Source -PathType Container)) {
    Fail "Missing configured source folder: $($S.Source). Stopped for safety."
  }
  if ([IO.Path]::IsPathRooted($S.Dest) -or $S.Dest -match '(^|[\\/])\.\.([\\/]|$)') {
    Fail "Unsafe destination mapping: $($S.Dest)"
  }
  $Key = $S.Dest.ToLowerInvariant()
  if ($SeenDest.ContainsKey($Key)) { Fail "Duplicate destination mapping: $($S.Dest)" }
  $SeenDest[$Key] = $true
  if (IsInside $Mirror $S.Source) {
    Fail "Unsafe configuration: backup mirror '$Mirror' is inside source '$($S.Source)'."
  }
}
Write-Host '      All configured source folders exist.'

Write-Host '[2/8] Opening the persistent Git mirror...'
if (-not (Test-Path -LiteralPath (Join-Path $Mirror '.git') -PathType Container)) {
  if (Test-Path -LiteralPath $Mirror) {
    Write-Host "      Removing old non-Git mirror folder: $Mirror"
    Remove-Item -LiteralPath $Mirror -Recurse -Force
  }
  Write-Host '      One-time clone...'
  & git.exe -c core.longpaths=true clone --no-checkout $Repo $Mirror
  if ($LASTEXITCODE -ne 0) { Fail 'Could not clone the GitHub repository.' }
}

& git.exe -c core.longpaths=true -C $Mirror config core.longpaths true
& git.exe -c core.longpaths=true -C $Mirror config core.autocrlf false
& git.exe -c core.longpaths=true -C $Mirror config core.safecrlf false
& git.exe -c core.longpaths=true -C $Mirror config core.filemode false
# Safe Git performance options for repositories containing very many files.
& git.exe -c core.longpaths=true -C $Mirror config core.preloadIndex true
& git.exe -c core.longpaths=true -C $Mirror config core.untrackedCache true
& git.exe -c core.longpaths=true -C $Mirror config index.threads true
& git.exe -c core.longpaths=true -C $Mirror config user.name 'R&D Automatic Backup'
& git.exe -c core.longpaths=true -C $Mirror config user.email 'backup@localhost'
& git.exe -c core.longpaths=true -C $Mirror remote set-url origin $Repo

# Highest-precedence local Git attributes: never normalize CR/LF when storing files.
$InfoAttr = Join-Path $Mirror '.git\info\attributes'
$InfoDir  = Split-Path -Parent $InfoAttr
New-Item -ItemType Directory -Path $InfoDir -Force | Out-Null
Set-Content -LiteralPath $InfoAttr -Value '* -text' -Encoding ASCII

Write-Host '[3/8] Synchronizing Git history WITHOUT deleting local folders...'
& git.exe -c core.longpaths=true -C $Mirror fetch origin --prune
if ($LASTEXITCODE -ne 0) { Fail 'Could not fetch GitHub.' }

& git.exe -c core.longpaths=true -C $Mirror show-ref --verify --quiet "refs/remotes/origin/$Branch"
if ($LASTEXITCODE -ne 0) { Fail "Remote branch origin/$Branch does not exist." }

# IMPORTANT: Do NOT use git clean/reset --hard/checkout here. On Windows those
# commands can repeatedly prompt when a project folder is locked or has a very
# long path. The laptop source trees are the authority, and ROBOCOPY /MIR below
# will clean ONLY the four managed backup trees.
#
# Point HEAD + the index at the current remote commit WITHOUT touching files in
# the working tree. This makes interrupted previous runs safe to recover from.
& git.exe -c core.longpaths=true -C $Mirror symbolic-ref HEAD "refs/heads/$Branch"
if ($LASTEXITCODE -ne 0) { Fail "Could not select local branch $Branch." }
& git.exe -c core.longpaths=true -C $Mirror update-ref "refs/heads/$Branch" "refs/remotes/origin/$Branch"
if ($LASTEXITCODE -ne 0) { Fail "Could not align local branch with origin/$Branch." }
& git.exe -c core.longpaths=true -C $Mirror read-tree "refs/remotes/origin/$Branch"
if ($LASTEXITCODE -ne 0) { Fail 'Could not load the GitHub tree into the Git index.' }

# If the repository already has root LFS attributes, materialize only that small
# metadata file. This does not touch any laptop project folder.
& git.exe -c core.longpaths=true -C $Mirror checkout-index -f -- .gitattributes 2>$null
Write-Host '      Git index aligned with GitHub; no global folder deletion was attempted.'

Write-Host '[4/8] Mirroring laptop folders exactly into the local backup...'
$MirrorResults = @{}
foreach ($S in $Sources) {
  $D = Join-Path $Mirror $S.Dest
  New-Item -ItemType Directory -Path $D -Force | Out-Null
  $SafeName = ($S.Dest -replace '[^A-Za-z0-9_.-]','_')
  $CopyLog = Join-Path $LogDir ("{0}_{1}_copy.log" -f $RunStamp,$SafeName)

  Write-Host ("      MIRROR: {0}" -f $S.Source)
  $R = RunRoboMirror $S.Source $D $CopyLog
  $MirrorResults[$S.Dest] = $R
  if ($R.SourceChanged) {
    if ($R.WatchOverflow) {
      Write-Host '              Source watcher overflow/change detected -> full verification required.'
    } else {
      Write-Host '              Source changed while copying -> full verification required.'
    }
  }
}

Write-Host '[5/8] VERIFYING laptop -> local mirror before Git is allowed to commit...'
foreach ($S in $Sources) {
  $D = Join-Path $Mirror $S.Dest
  $SafeName = ($S.Dest -replace '[^A-Za-z0-9_.-]','_')
  $VerifyLog = Join-Path $LogDir ("{0}_{1}_verify.log" -f $RunStamp,$SafeName)
  $R = $MirrorResults[$S.Dest]

  # Robocopy success codes 0..3 contain no failure bit and no mismatch bit.
  # If FileSystemWatcher also confirms that the source did not change while the
  # full Robocopy comparison was running, a second full tree walk adds no value.
  $NeedsFullVerify = $ForceFullVerify -or $R.SourceChanged -or (($R.Code -band 4) -ne 0)

  if (-not $NeedsFullVerify) {
    Write-Host ("      FAST VERIFIED: /{0}/ (stable source; Robocopy code {1})." -f $S.Dest,$R.Code)
    continue
  }

  Write-Host ("      FULL VERIFY: /{0}/" -f $S.Dest)
  $V = VerifyRoboMirror $S.Source $D $VerifyLog
  if ($V -ne 0) {
    Write-Host ("      Verification found differences for {0}; retrying one sync..." -f $S.Dest)
    $RetryLog = Join-Path $LogDir ("{0}_{1}_retry.log" -f $RunStamp,$SafeName)
    $Retry = RunRoboMirror $S.Source $D $RetryLog
    $V = VerifyRoboMirror $S.Source $D $VerifyLog
  }
  if ($V -ne 0) {
    Fail "Verification FAILED for '$($S.Source)'. The source may still be changing or a file could not be mirrored. Robocopy verify code: $V. Log: $VerifyLog"
  }
  Write-Host ("      VERIFIED: /{0}/ matches the source tree." -f $S.Dest)
}

Write-Host '[6/8] Detecting all new/modified/deleted Git files in managed backup trees...'
$ManagedPaths = @($Sources | ForEach-Object { $_.Dest })
$StageArgs = @('add','-A','-f','--') + $ManagedPaths
& git.exe -c core.longpaths=true -c core.autocrlf=false -C $Mirror @StageArgs
if ($LASTEXITCODE -ne 0) { Fail 'git add failed for managed backup trees.' }

& git.exe -c core.longpaths=true -C $Mirror diff --cached --quiet
if ($LASTEXITCODE -eq 0) {
  Write-Host ''
  Write-Host '      NO CONTENT CHANGES - GitHub already matches the verified mirror.'
  Write-Host ''
  Write-Host '======================================================================'
  Write-Host ' EXACT VERIFIED BACKUP COMPLETED - NOTHING NEW TO UPLOAD'
  Write-Host '======================================================================'
  Write-Host "Logs: $LogDir"
  exit 0
}

$Changed = @(& git.exe -c core.longpaths=true -c core.quotepath=false -C $Mirror diff --cached --name-only --no-renames)
$Changed = @($Changed | Where-Object { $_ -and $_.Trim() } | Select-Object -Unique)
Write-Host ("      Changed/deleted paths: {0}" -f $Changed.Count)

Write-Host '[7/8] Preparing large files and uploading only the differences...'
& git.exe lfs version *> $null
$HasLfs = ($LASTEXITCODE -eq 0)
$Large = @()
foreach ($R in $Changed) {
  $F = Join-Path $Mirror ($R.Replace('/','\'))
  if (Test-Path -LiteralPath $F -PathType Leaf) {
    $Item = Get-Item -LiteralPath $F -Force
    if ($Item.Length -ge $LargeFileAt) { $Large += $R }
  }
}

if ($Large.Count -gt 0) {
  if (-not $HasLfs) {
    Fail "Found $($Large.Count) changed file(s) >= $([int]($LargeFileAt/1MB)) MB, but Git LFS is not installed. Install Git LFS and run again."
  }
  & git.exe -c core.longpaths=true -C $Mirror lfs install --local *> $null
  if ($LASTEXITCODE -ne 0) { Fail 'Git LFS local install failed.' }
  foreach ($R in $Large) {
    & git.exe -c core.longpaths=true -C $Mirror lfs track -- $R *> $null
    if ($LASTEXITCODE -ne 0) { Fail "Git LFS track failed: $R" }
    & git.exe -c core.longpaths=true -C $Mirror add -f -- $R
    if ($LASTEXITCODE -ne 0) { Fail "Git add failed after LFS tracking: $R" }
  }
  if (Test-Path -LiteralPath (Join-Path $Mirror '.gitattributes')) {
    & git.exe -c core.longpaths=true -C $Mirror add -f -- .gitattributes
    if ($LASTEXITCODE -ne 0) { Fail 'Could not stage .gitattributes.' }
  }
}

$All = @(& git.exe -c core.longpaths=true -c core.quotepath=false -C $Mirror diff --cached --name-only --no-renames)
$All = @($All | Where-Object { $_ -and $_.Trim() } | Select-Object -Unique)

[long]$Total = 0
foreach ($R in $All) {
  $F = Join-Path $Mirror ($R.Replace('/','\'))
  if (Test-Path -LiteralPath $F -PathType Leaf) {
    $Z = (Get-Item -LiteralPath $F -Force).Length
    if ($Z -ge $LargeFileAt -and $HasLfs) { $Total += 1MB } else { $Total += $Z }
  }
}

if ($All.Count -le $MaxBatchFiles -and $Total -le $MaxBatchBytes) {
  [void](CommitPush ("Exact verified backup - {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
}
else {
  Write-Host '      Large change set detected; committing only changed paths in batches.'
  & git.exe -c core.longpaths=true -C $Mirror reset
  if ($LASTEXITCODE -ne 0) { Fail 'Could not prepare Git batches.' }

  $Batch = New-Object 'System.Collections.Generic.List[string]'
  [long]$Bytes = 0
  $N = 0
  $PathSpecFile = Join-Path $env:TEMP "rnd_v10_3_paths_$PID.bin"

  function FlushBatch {
    if ($script:Batch.Count -eq 0) { return }
    $script:N++
    Write-Host ("      Batch {0}: {1} paths, approx {2:N1} MB" -f $script:N,$script:Batch.Count,($script:Bytes/1MB))
    PathFile $script:Batch.ToArray() $script:PathSpecFile
    & git.exe -c core.longpaths=true -c core.autocrlf=false -C $Mirror add -A -f --pathspec-from-file=$script:PathSpecFile --pathspec-file-nul
    if ($LASTEXITCODE -ne 0) { Fail "git add failed in batch $script:N" }
    [void](CommitPush ("Exact verified backup batch {0} - {1}" -f $script:N,(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
    $script:Batch.Clear()
    $script:Bytes = 0
  }

  foreach ($R in $All) {
    $F = Join-Path $Mirror ($R.Replace('/','\'))
    [long]$Z = 0
    if (Test-Path -LiteralPath $F -PathType Leaf) {
      $Z = (Get-Item -LiteralPath $F -Force).Length
      if ($Z -ge $LargeFileAt -and $HasLfs) { $Z = 1MB }
    }
    if ($Batch.Count -gt 0 -and (($Bytes + $Z) -gt $MaxBatchBytes -or $Batch.Count -ge $MaxBatchFiles)) {
      FlushBatch
    }
    $Batch.Add($R)
    $Bytes += $Z
  }
  FlushBatch
  Remove-Item -LiteralPath $PathSpecFile -Force -ErrorAction SilentlyContinue

  # Final catch-all is intentionally limited to the four managed trees.
  $FinalStageArgs = @('add','-A','-f','--') + $ManagedPaths
  & git.exe -c core.longpaths=true -c core.autocrlf=false -C $Mirror @FinalStageArgs
  if ($LASTEXITCODE -ne 0) { Fail 'Final git add failed for managed backup trees.' }
  if (Test-Path -LiteralPath (Join-Path $Mirror '.gitattributes')) {
    & git.exe -c core.longpaths=true -C $Mirror add -f -- .gitattributes
    if ($LASTEXITCODE -ne 0) { Fail 'Final .gitattributes add failed.' }
  }
  [void](CommitPush ("Exact verified backup final sync - {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')))
}

Write-Host '[8/8] Confirming GitHub accepted the final commit...'
& git.exe -c core.longpaths=true -C $Mirror fetch origin
if ($LASTEXITCODE -ne 0) { Fail 'Final GitHub verification fetch failed.' }
$LocalHead  = (& git.exe -c core.longpaths=true -C $Mirror rev-parse HEAD).Trim()
$RemoteHead = (& git.exe -c core.longpaths=true -C $Mirror rev-parse "origin/$Branch").Trim()
if ($LocalHead -ne $RemoteHead) {
  Fail "Final remote verification failed. Local HEAD $LocalHead does not equal origin/$Branch $RemoteHead"
}

# Verify only the managed backup trees plus LFS metadata. Unrelated stale files
# elsewhere in the persistent G mirror are deliberately ignored; they are never staged.
$StatusArgs = @('status','--porcelain','--') + $ManagedPaths
if (Test-Path -LiteralPath (Join-Path $Mirror '.gitattributes')) { $StatusArgs += '.gitattributes' }
$StatusAfter = @(& git.exe -c core.longpaths=true -C $Mirror @StatusArgs)
if ($StatusAfter.Count -gt 0) {
  Fail 'Managed backup trees are not clean after upload. Stopped instead of claiming success.'
}

Write-Host ''
Write-Host '======================================================================'
Write-Host ' EXACT VERIFIED BACKUP COMPLETED SUCCESSFULLY'
Write-Host '======================================================================'
Write-Host ("Changed paths processed this run: {0}" -f $All.Count)
Write-Host ("GitHub branch confirmed at: {0}" -f $RemoteHead)
Write-Host ("Logs: {0}" -f $LogDir)
Write-Host ''
Write-Host 'IMPORTANT:'
Write-Host '  - Laptop source folders were not intentionally modified.'
Write-Host '  - No global git clean/reset --hard is used, so locked folders cannot cause the old y/n delete loop.'
Write-Host '  - New, modified AND deleted files are mirrored.'
Write-Host '  - Ignored project files are force-added, so .gitignore will not hide them.'
Write-Host '  - Git CR/LF normalization is disabled to preserve file bytes.'
Write-Host '  - Stable source trees use one complete Robocopy scan instead of two.'
Write-Host '  - If a source changes during the copy, v10.3 automatically runs the full v10.2 verification.'
Write-Host '  - Nested .git metadata and Windows junctions are intentionally excluded.'
Write-Host '  - Git cannot store truly empty folders; GitHub may visually compact folders.'
Write-Host '    Those are Git/GitHub limitations, not missing ordinary project files.'
