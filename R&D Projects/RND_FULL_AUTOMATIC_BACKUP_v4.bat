@echo off
setlocal EnableExtensions
title R&D Projects - FULL Automatic GitHub Backup

set "SELF=%~f0"
set "PS1=%TEMP%\rnd_full_backup_%RANDOM%_%RANDOM%.ps1"

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
    echo ========================================================================
    echo BACKUP FAILED
    echo ========================================================================
    echo The original D:\R^&D Projects files were not intentionally deleted.
    echo Send me a screenshot of the error shown above.
    pause
    exit /b %RC%
)

echo.
echo Backup finished. This window will close in 8 seconds...
timeout /t 8 /nobreak >nul
exit /b 0

#==POWERSHELL==
$ErrorActionPreference = 'Stop'

$Source = 'D:\R&D Projects'
$RepoUrl = 'https://github.com/KrishnaRealTech1/R-D_Krishna_Projects.git'
$Mirror = Join-Path $env:LOCALAPPDATA 'RND_GitHub_Full_Backup_Mirror'
$BatchLimit = 500MB
$MaxFilesPerBatch = 500
$PushDelaySeconds = 11

function Header([string]$Text) {
    Write-Host ''
    Write-Host ('=' * 72)
    Write-Host $Text
    Write-Host ('=' * 72)
}

function Run-Git {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
    & git -C $Mirror @Args
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($Args -join ' ')"
    }
}

function Push-WithRetry {
    param([string]$Branch)
    for ($Try = 1; $Try -le 3; $Try++) {
        Write-Host "Pushing to GitHub (attempt $Try/3)..."
        & git -C $Mirror push -u origin $Branch
        if ($LASTEXITCODE -eq 0) {
            Start-Sleep -Seconds $PushDelaySeconds
            return
        }
        if ($Try -lt 3) {
            Write-Host 'Push failed temporarily. Retrying in 20 seconds...'
            Start-Sleep -Seconds 20
        }
    }
    throw 'GitHub push failed after 3 attempts.'
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

    Push-WithRetry $Branch
    return $true
}

function Get-RelativePath {
    param([string]$FullPath, [string]$Root)
    $prefix = $Root.TrimEnd('\') + '\'
    if ($FullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullPath.Substring($prefix.Length).Replace('\','/')
    }
    throw "Path is outside mirror: $FullPath"
}

function Write-PathspecFile {
    param([string[]]$Paths, [string]$FileName)

    $enc = New-Object System.Text.UTF8Encoding($false)
    $stream = New-Object System.IO.FileStream($FileName, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        foreach ($p in $Paths) {
            $bytes = $enc.GetBytes($p)
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.WriteByte(0)
        }
    }
    finally {
        $stream.Dispose()
    }
}

Header 'R&D PROJECTS - FULL AUTOMATIC GITHUB BACKUP'
Write-Host "Source : $Source"
Write-Host "GitHub : $RepoUrl"
Write-Host "Mirror : $Mirror"
Write-Host ''

if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
    throw "Source folder does not exist: $Source"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git for Windows was not found in PATH.'
}
if (-not (Get-Command robocopy -ErrorAction SilentlyContinue)) {
    throw 'ROBOCOPY was not found.'
}

Write-Host '[1/8] Preparing private Git mirror...'
if (-not (Test-Path -LiteralPath (Join-Path $Mirror '.git') -PathType Container)) {
    if (Test-Path -LiteralPath $Mirror) {
        Remove-Item -LiteralPath $Mirror -Recurse -Force
    }
    & git clone $RepoUrl $Mirror
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not clone the GitHub repository.'
    }
}
else {
    Write-Host 'Existing mirror found.'
}

& git -C $Mirror remote set-url origin $RepoUrl
& git -C $Mirror config core.longpaths true

$name = (& git -C $Mirror config user.name 2>$null)
if (-not $name) {
    & git -C $Mirror config user.name 'R&D Automatic Backup'
}
$email = (& git -C $Mirror config user.email 2>$null)
if (-not $email) {
    & git -C $Mirror config user.email 'backup@localhost'
}

Write-Host '[2/8] Synchronizing with the latest GitHub version...'
Run-Git fetch origin --prune

$headRef = (& git -C $Mirror symbolic-ref refs/remotes/origin/HEAD 2>$null)
$Branch = $null
if ($LASTEXITCODE -eq 0 -and $headRef) {
    $Branch = ($headRef.Trim() -split '/')[-1]
}
if (-not $Branch) {
    & git -C $Mirror show-ref --verify --quiet refs/remotes/origin/main
    if ($LASTEXITCODE -eq 0) { $Branch = 'main' }
}
if (-not $Branch) {
    & git -C $Mirror show-ref --verify --quiet refs/remotes/origin/master
    if ($LASTEXITCODE -eq 0) { $Branch = 'master' }
}
if (-not $Branch) { $Branch = 'main' }

Write-Host "Branch : $Branch"

& git -C $Mirror show-ref --verify --quiet "refs/remotes/origin/$Branch"
if ($LASTEXITCODE -eq 0) {
    Run-Git checkout -B $Branch "origin/$Branch"
}
else {
    Run-Git checkout -B $Branch
}

Run-Git reset --hard
Run-Git clean -fdx

# Remove unreachable objects left behind by the earlier failed 6+ GiB push.
& git -C $Mirror reflog expire --expire=now --all 2>$null
& git -C $Mirror gc --prune=now 2>$null

Write-Host '[3/8] Mirroring the PC folder...'
& robocopy $Source $Mirror /MIR /COPY:DAT /DCOPY:DAT /R:2 /W:2 /XJ /XD .git /NP /NJH /NJS
$rc = $LASTEXITCODE
if ($rc -ge 8) {
    throw "ROBOCOPY failed with exit code $rc."
}

Write-Host '[4/8] Preserving empty folders...'
$sourceDirs = Get-ChildItem -LiteralPath $Source -Directory -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' }

foreach ($dir in $sourceDirs) {
    $visibleChildren = @(Get-ChildItem -LiteralPath $dir.FullName -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne '.git' })
    if ($visibleChildren.Count -eq 0) {
        $rel = $dir.FullName.Substring($Source.TrimEnd('\').Length).TrimStart('\')
        $targetDir = Join-Path $Mirror $rel
        if (-not (Test-Path -LiteralPath $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }
        $keep = Join-Path $targetDir '.gitkeep'
        if (-not (Test-Path -LiteralPath $keep)) {
            New-Item -ItemType File -Path $keep -Force | Out-Null
        }
    }
}

Write-Host '[5/8] Configuring Git LFS automatically for large files...'
& git lfs version *> $null
$HasLfs = ($LASTEXITCODE -eq 0)

$allMirrorFiles = @(Get-ChildItem -LiteralPath $Mirror -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' })

$largeFiles = @($allMirrorFiles | Where-Object { $_.Length -ge 90MB })

if ($largeFiles.Count -gt 0) {
    Write-Host ("Large files found (90 MB+): {0}" -f $largeFiles.Count)

    if (-not $HasLfs) {
        Write-Host ''
        Write-Host 'These files require Git LFS:'
        foreach ($f in $largeFiles | Select-Object -First 20) {
            Write-Host ("  {0:N1} MB  {1}" -f ($f.Length / 1MB), (Get-RelativePath $f.FullName $Mirror))
        }
        if ($largeFiles.Count -gt 20) {
            Write-Host "  ...and $($largeFiles.Count - 20) more."
        }
        throw 'Git LFS is not installed. Install Git LFS once, then run this BAT again.'
    }

    & git -C $Mirror lfs install --local
    if ($LASTEXITCODE -ne 0) {
        throw 'git lfs install failed.'
    }

    foreach ($f in $largeFiles) {
        $rel = Get-RelativePath $f.FullName $Mirror
        & git -C $Mirror lfs track -- $rel *> $null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not configure Git LFS for: $rel"
        }
    }

    & git -C $Mirror add -f -- .gitattributes
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stage .gitattributes.'
    }
    [void](Commit-And-Push $Branch 'Automatic backup: configure Git LFS')
}
else {
    Write-Host 'No files require Git LFS.'
}

Write-Host '[6/8] Converting any old nested-repository links to normal folders...'
$stageLines = @(& git -C $Mirror ls-files --stage)
foreach ($line in $stageLines) {
    if ($line -match '^160000\s+[0-9a-f]+\s+\d+\t(.+)$') {
        $gitlink = $Matches[1]
        Write-Host "Converting Git link: $gitlink"
        & git -C $Mirror rm -f --cached --ignore-unmatch -- $gitlink *> $null
    }
}

Write-Host '[7/8] Uploading files in safe batches below GitHub push limits...'

# Re-read after .gitkeep/.gitattributes creation.
$files = @(Get-ChildItem -LiteralPath $Mirror -File -Recurse -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '(?i)\\\.git(\\|$)' } |
    Sort-Object FullName)

$batch = New-Object System.Collections.Generic.List[string]
[long]$batchBytes = 0
$batchNumber = 0
$totalCommits = 0
$manifest = Join-Path $env:TEMP ("rnd_backup_paths_{0}.bin" -f $PID)

function Flush-Batch {
    if ($script:batch.Count -eq 0) { return }

    $script:batchNumber++
    Write-Host ("  Batch {0}: {1} files, {2:N1} MB raw data" -f $script:batchNumber, $script:batch.Count, ($script:batchBytes / 1MB))

    Write-PathspecFile -Paths ($script:batch.ToArray()) -FileName $script:manifest

    & git -C $Mirror add -A -f "--pathspec-from-file=$script:manifest" --pathspec-file-nul
    if ($LASTEXITCODE -ne 0) {
        throw "git add failed in batch $script:batchNumber."
    }

    $changed = Commit-And-Push $Branch ("Automatic backup batch {0} - {1}" -f $script:batchNumber, (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
    if ($changed) { $script:totalCommits++ }

    $script:batch.Clear()
    $script:batchBytes = 0
}

foreach ($file in $files) {
    $rel = Get-RelativePath $file.FullName $Mirror
    [long]$size = $file.Length

    # Files handled by LFS create tiny Git pointer objects. Treat them as 1 MB
    # for Git push batching, while Git LFS uploads the real content separately.
    [long]$effectiveSize = $size
    if ($size -ge 90MB -and $HasLfs) {
        $effectiveSize = 1MB
    }

    if ($batch.Count -gt 0 -and (($batchBytes + $effectiveSize) -gt $BatchLimit -or $batch.Count -ge $MaxFilesPerBatch)) {
        Flush-Batch
    }

    $batch.Add($rel)
    $batchBytes += $effectiveSize
}
Flush-Batch

if (Test-Path -LiteralPath $manifest) {
    Remove-Item -LiteralPath $manifest -Force -ErrorAction SilentlyContinue
}

Write-Host '[8/8] Synchronizing deletions and final metadata...'
& git -C $Mirror add -u -- .
if ($LASTEXITCODE -ne 0) {
    throw 'Could not stage deletions.'
}

# Also stage .gitattributes/.gitkeep or any final generated metadata.
& git -C $Mirror add -A -f -- .
if ($LASTEXITCODE -ne 0) {
    throw 'Could not stage the final changes.'
}

$finalChanged = Commit-And-Push $Branch ("Automatic backup final sync - {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
if ($finalChanged) { $totalCommits++ }

Header 'BACKUP COMPLETED SUCCESSFULLY'
Write-Host "GitHub branch : $Branch"
Write-Host "Backup commits: $totalCommits"
Write-Host ''
Write-Host 'Synchronized:'
Write-Host '  + New files'
Write-Host '  + Modified files'
Write-Host '  + Deleted files'
Write-Host '  + Files ignored by .gitignore'
Write-Host '  + Nested project source files (nested .git metadata is skipped)'
Write-Host '  + Empty folders (represented by .gitkeep)'
Write-Host '  + Large files via Git LFS, when Git LFS is available'
Write-Host ''
Write-Host 'Your original D:\R&D Projects folder was not modified.'
