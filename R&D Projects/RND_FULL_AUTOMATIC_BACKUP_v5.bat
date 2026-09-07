@echo off
setlocal EnableExtensions
title R&D Projects - FULL Automatic GitHub Backup v5

set "SELF=%~f0"
set "PS1=%TEMP%\rnd_full_backup_v5_%RANDOM%_%RANDOM%.ps1"

powershell -NoProfile -ExecutionPolicy Bypass -Command "$lines=[System.IO.File]::ReadAllLines($env:SELF); $marker=[Array]::IndexOf($lines,'#==POWERSHELL=='); if($marker -lt 0){exit 2}; $lines[($marker+1)..($lines.Length-1)] | Set-Content -LiteralPath $env:PS1 -Encoding UTF8"

if errorlevel 1 (
    echo ERROR: Could not prepare the backup engine.
    pause
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
set "RC=%ERRORLEVEL%"

del /q "%PS1%" >nul 2>&1

if not "%RC%"=="0" (
    echo.
    echo ==========================================================================
    echo BACKUP FAILED
    echo ==========================================================================
    echo Your original D:\R^&D Projects files were NOT intentionally deleted.
    echo.
    echo Send me a screenshot of the error above if this v5 script stops.
    pause
    exit /b %RC%
)

echo.
echo Backup finished successfully. This window will close in 8 seconds...
timeout /t 8 /nobreak >nul
exit /b 0

#==POWERSHELL==
$ErrorActionPreference = 'Stop'

$Source = 'D:\R&D Projects'
$RepoUrl = 'https://github.com/KrishnaRealTech1/R-D_Krishna_Projects.git'

# New mirror name intentionally avoids the damaged/locked mirror from older scripts.
$Mirror = Join-Path $env:LOCALAPPDATA 'RND_GitHub_Backup_Mirror_v5'

# Keep each normal Git push comfortably below GitHub's push-size limit.
$BatchLimit = 450MB
$MaxFilesPerBatch = 400
$PushDelaySeconds = 5

function Show-Header([string]$Text) {
    Write-Host ''
    Write-Host ('=' * 74)
    Write-Host $Text
    Write-Host ('=' * 74)
}

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)

    & git -C $Mirror @Args
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Args -join ' ')"
    }
}

function Push-WithRetry {
    param([string]$Branch)

    for ($Attempt = 1; $Attempt -le 4; $Attempt++) {
        Write-Host "    Upload attempt $Attempt/4..."
        & git -C $Mirror push -u origin $Branch

        if ($LASTEXITCODE -eq 0) {
            Start-Sleep -Seconds $PushDelaySeconds
            return
        }

        if ($Attempt -lt 4) {
            Write-Host '    Temporary push failure. Waiting 20 seconds before retry...'
            Start-Sleep -Seconds 20
        }
    }

    throw 'GitHub push failed after 4 attempts.'
}

function Commit-And-Push {
    param(
        [string]$Branch,
        [string]$Message
    )

    & git -C $Mirror diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        return $false
    }

    & git -C $Mirror commit -m $Message
    if ($LASTEXITCODE -ne 0) {
        throw "Commit failed: $Message"
    }

    Push-WithRetry -Branch $Branch
    return $true
}

function Get-RelativeGitPath {
    param([string]$FullPath)

    $Prefix = $Mirror.TrimEnd('\') + '\'
    if (-not $FullPath.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected path outside backup mirror: $FullPath"
    }

    return $FullPath.Substring($Prefix.Length).Replace('\','/')
}

function Write-NullSeparatedPathFile {
    param(
        [string[]]$Paths,
        [string]$FileName
    )

    $Utf8 = New-Object System.Text.UTF8Encoding($false)
    $Stream = New-Object System.IO.FileStream(
        $FileName,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None
    )

    try {
        foreach ($Path in $Paths) {
            $Bytes = $Utf8.GetBytes($Path)
            $Stream.Write($Bytes, 0, $Bytes.Length)
            $Stream.WriteByte(0)
        }
    }
    finally {
        $Stream.Dispose()
    }
}

Show-Header 'R&D PROJECTS - FULL AUTOMATIC GITHUB BACKUP'
Write-Host "PC folder : $Source"
Write-Host "GitHub    : $RepoUrl"
Write-Host "Work area : $Mirror"
Write-Host ''

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Source folder not found: $Source"
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git for Windows is not installed or is not available in PATH.'
}

if (-not (Get-Command robocopy -ErrorAction SilentlyContinue)) {
    throw 'ROBOCOPY was not found.'
}

Write-Host '[1/9] Preparing a clean private Git work area...'

if (-not (Test-Path -LiteralPath (Join-Path $Mirror '.git') -PathType Container)) {
    if (Test-Path -LiteralPath $Mirror) {
        Write-Host '    Removing incomplete v5 work area...'
        try {
            Get-ChildItem -LiteralPath $Mirror -Recurse -Force -ErrorAction SilentlyContinue |
                ForEach-Object { try { $_.IsReadOnly = $false } catch {} }
            Remove-Item -LiteralPath $Mirror -Recurse -Force -ErrorAction Stop
        }
        catch {
            $Mirror = Join-Path $env:LOCALAPPDATA ("RND_GitHub_Backup_Mirror_v5_" + (Get-Date -Format 'yyyyMMdd_HHmmss'))
            Write-Host "    Old work area is locked. Using new work area:"
            Write-Host "    $Mirror"
        }
    }

    & git clone $RepoUrl $Mirror
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not clone the GitHub repository.'
    }
}
else {
    Write-Host '    Existing v5 work area found.'
}

& git -C $Mirror remote set-url origin $RepoUrl
& git -C $Mirror config core.longpaths true

$GitName = (& git -C $Mirror config user.name 2>$null)
if (-not $GitName) {
    & git -C $Mirror config user.name 'R&D Automatic Backup'
}

$GitEmail = (& git -C $Mirror config user.email 2>$null)
if (-not $GitEmail) {
    & git -C $Mirror config user.email 'backup@localhost'
}

Write-Host '[2/9] Reading the latest GitHub version...'
Invoke-Git fetch origin --prune

$Branch = $null
$HeadRef = (& git -C $Mirror symbolic-ref refs/remotes/origin/HEAD 2>$null)

if ($LASTEXITCODE -eq 0 -and $HeadRef) {
    $Branch = ($HeadRef.Trim() -split '/')[-1]
}

if (-not $Branch) {
    & git -C $Mirror show-ref --verify --quiet refs/remotes/origin/main
    if ($LASTEXITCODE -eq 0) { $Branch = 'main' }
}

if (-not $Branch) {
    & git -C $Mirror show-ref --verify --quiet refs/remotes/origin/master
    if ($LASTEXITCODE -eq 0) { $Branch = 'master' }
}

if (-not $Branch) {
    $Branch = 'main'
}

Write-Host "    Branch: $Branch"

# IMPORTANT:
# We deliberately DO NOT use "git clean -fdx".
# The older script failed there because some copied files/directories were read-only.
# Reset tracked files only; ROBOCOPY below will reconcile the PC folder.
& git -C $Mirror show-ref --verify --quiet "refs/remotes/origin/$Branch"
if ($LASTEXITCODE -eq 0) {
    & git -C $Mirror checkout -B $Branch "origin/$Branch"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not check out origin/$Branch."
    }

    & git -C $Mirror reset --hard "origin/$Branch"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not reset to origin/$Branch."
    }
}
else {
    & git -C $Mirror checkout -B $Branch
    if ($LASTEXITCODE -ne 0) {
        throw "Could not prepare branch $Branch."
    }
}

Write-Host '[3/9] Removing read-only attributes in the PRIVATE work area...'

# This touches only the private mirror, never D:\R&D Projects.
# It prevents "Permission denied" when a file copied on an earlier run was read-only.
Get-ChildItem -LiteralPath $Mirror -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' } |
    ForEach-Object {
        try { $_.IsReadOnly = $false } catch {}
    }

Write-Host '[4/9] Mirroring new, changed and deleted PC files...'
Write-Host '    (Your original PC folder is read-only from this script''s point of view.)'

& robocopy $Source $Mirror /MIR /COPY:DAT /DCOPY:DAT /R:5 /W:3 /XJ /XD .git /NP /NJH /NJS

$RoboCode = $LASTEXITCODE
if ($RoboCode -ge 8) {
    throw "ROBOCOPY failed with exit code $RoboCode."
}

# A Git worktree/submodule can use a .git FILE rather than a .git directory.
# Do not upload that metadata pointer.
Get-ChildItem -LiteralPath $Mirror -File -Filter '.git' -Recurse -Force -ErrorAction SilentlyContinue |
    ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
    }

Write-Host '[5/9] Preserving completely empty PC folders...'

$SourcePrefix = $Source.TrimEnd('\') + '\'

Get-ChildItem -LiteralPath $Source -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' } |
    ForEach-Object {
        $Children = @(
            Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -ne '.git' }
        )

        if ($Children.Count -eq 0) {
            $Rel = $_.FullName.Substring($SourcePrefix.Length)
            $DestinationDir = Join-Path $Mirror $Rel

            if (-not (Test-Path -LiteralPath $DestinationDir)) {
                New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
            }

            $KeepFile = Join-Path $DestinationDir '.gitkeep'
            if (-not (Test-Path -LiteralPath $KeepFile)) {
                New-Item -ItemType File -Path $KeepFile -Force | Out-Null
            }
        }
    }

Write-Host '[6/9] Detecting files that require Git LFS...'

& git lfs version *> $null
$HasLfs = ($LASTEXITCODE -eq 0)

$AllFiles = @(
    Get-ChildItem -LiteralPath $Mirror -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' }
)

$LargeFiles = @($AllFiles | Where-Object { $_.Length -ge 90MB })

if ($LargeFiles.Count -gt 0) {
    Write-Host ("    Large files found: {0}" -f $LargeFiles.Count)

    if (-not $HasLfs) {
        Write-Host ''
        Write-Host 'Git LFS is required for these large files:'
        foreach ($File in $LargeFiles | Select-Object -First 20) {
            Write-Host ("    {0:N1} MB  {1}" -f ($File.Length / 1MB), (Get-RelativeGitPath $File.FullName))
        }
        if ($LargeFiles.Count -gt 20) {
            Write-Host "    ...and $($LargeFiles.Count - 20) more."
        }
        throw 'Git LFS is not installed. Install Git LFS once and run this BAT again.'
    }

    & git -C $Mirror lfs install --local
    if ($LASTEXITCODE -ne 0) {
        throw 'git lfs install failed.'
    }

    foreach ($File in $LargeFiles) {
        $Rel = Get-RelativeGitPath $File.FullName
        & git -C $Mirror lfs track -- $Rel *> $null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not enable Git LFS for: $Rel"
        }
    }

    & git -C $Mirror add -f -- .gitattributes
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stage .gitattributes.'
    }

    [void](Commit-And-Push -Branch $Branch -Message 'Automatic backup: configure Git LFS')
}
else {
    Write-Host '    No 90 MB+ files found.'
}

Write-Host '[7/9] Converting any old Git submodule/gitlink entries...'

$StageLines = @(& git -C $Mirror ls-files --stage)
$GitLinkCount = 0

foreach ($Line in $StageLines) {
    if ($Line -match '^160000\s+[0-9a-f]+\s+\d+\t(.+)$') {
        $GitLinkCount++
        $GitLink = $Matches[1]
        Write-Host "    Converting: $GitLink"
        & git -C $Mirror rm -f --cached --ignore-unmatch -- $GitLink *> $null
    }
}

if ($GitLinkCount -eq 0) {
    Write-Host '    No Git links need conversion.'
}

Write-Host '[8/9] Uploading files in safe small batches...'

# Re-read files because .gitkeep/.gitattributes may now exist.
$Files = @(
    Get-ChildItem -LiteralPath $Mirror -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' } |
    Sort-Object FullName
)

$Batch = New-Object System.Collections.Generic.List[string]
[long]$BatchBytes = 0
$BatchNumber = 0
$CommitCount = 0
$PathFile = Join-Path $env:TEMP ("rnd_backup_paths_{0}.bin" -f $PID)

function Flush-CurrentBatch {
    if ($script:Batch.Count -eq 0) {
        return
    }

    $script:BatchNumber++

    Write-Host ("    Batch {0}: {1} files, approx. {2:N1} MB" -f `
        $script:BatchNumber,
        $script:Batch.Count,
        ($script:BatchBytes / 1MB)
    )

    Write-NullSeparatedPathFile -Paths $script:Batch.ToArray() -FileName $script:PathFile

    & git -C $Mirror add -A -f --pathspec-from-file=$script:PathFile --pathspec-file-nul
    if ($LASTEXITCODE -ne 0) {
        throw "git add failed in batch $script:BatchNumber."
    }

    $DidCommit = Commit-And-Push `
        -Branch $Branch `
        -Message ("Automatic backup batch {0} - {1}" -f $script:BatchNumber, (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))

    if ($DidCommit) {
        $script:CommitCount++
    }

    $script:Batch.Clear()
    $script:BatchBytes = 0
}

foreach ($File in $Files) {
    $Rel = Get-RelativeGitPath $File.FullName
    [long]$EffectiveSize = $File.Length

    # LFS stores only a small pointer inside normal Git.
    if ($File.Length -ge 90MB -and $HasLfs) {
        $EffectiveSize = 1MB
    }

    if (
        $Batch.Count -gt 0 -and
        (
            ($BatchBytes + $EffectiveSize) -gt $BatchLimit -or
            $Batch.Count -ge $MaxFilesPerBatch
        )
    ) {
        Flush-CurrentBatch
    }

    $Batch.Add($Rel)
    $BatchBytes += $EffectiveSize
}

Flush-CurrentBatch

if (Test-Path -LiteralPath $PathFile) {
    Remove-Item -LiteralPath $PathFile -Force -ErrorAction SilentlyContinue
}

Write-Host '[9/9] Applying deletions and doing final synchronization...'

& git -C $Mirror add -A -f -- .
if ($LASTEXITCODE -ne 0) {
    throw 'Final git add failed.'
}

$FinalCommit = Commit-And-Push `
    -Branch $Branch `
    -Message ("Automatic backup final sync - {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))

if ($FinalCommit) {
    $CommitCount++
}

Show-Header 'BACKUP COMPLETED SUCCESSFULLY'
Write-Host "Branch         : $Branch"
Write-Host "Backup commits : $CommitCount"
Write-Host ''
Write-Host 'Automatically synchronized:'
Write-Host '  + New files and folders'
Write-Host '  + Modified files'
Write-Host '  + Deleted files'
Write-Host '  + Files ignored by .gitignore'
Write-Host '  + Hidden/read-only source files'
Write-Host '  + Nested project source files (their .git metadata is skipped)'
Write-Host '  + Empty folders through .gitkeep'
Write-Host '  + Large files through Git LFS'
Write-Host ''
Write-Host 'The original D:\R&D Projects folder was NOT modified.'
