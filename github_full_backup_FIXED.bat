@echo off
setlocal EnableExtensions EnableDelayedExpansion
title R&D Projects - Automatic GitHub Backup

REM ================================================================
REM CONFIGURATION
REM ================================================================
set "SOURCE=D:\R&D Projects"
set "REPO_URL=https://github.com/KrishnaRealTech1/R-D_Krishna_Projects.git"
set "MIRROR=%LOCALAPPDATA%\RND_GitHub_Full_Backup_Mirror"
set "LOG=%LOCALAPPDATA%\RND_GitHub_Full_Backup.log"
REM ================================================================

echo.
echo ================================================================
echo              R&D PROJECTS - AUTOMATIC GITHUB BACKUP
echo ================================================================
echo Source : "%SOURCE%"
echo GitHub : "%REPO_URL%"
echo.

REM ----- Basic checks ------------------------------------------------
if not exist "%SOURCE%\" (
    echo ERROR: Source folder was not found:
    echo        "%SOURCE%"
    goto :FAILED
)

where git >nul 2>&1
if errorlevel 1 (
    echo ERROR: Git for Windows is not installed or is not in PATH.
    goto :FAILED
)

where robocopy >nul 2>&1
if errorlevel 1 (
    echo ERROR: ROBOCOPY was not found.
    goto :FAILED
)

REM ----- Step 1: Create/reuse private Git mirror --------------------
echo [1/7] Preparing private Git mirror...

if not exist "%MIRROR%\.git\" (
    if exist "%MIRROR%\" (
        echo Removing incomplete old mirror...
        rmdir /s /q "%MIRROR%"
        if exist "%MIRROR%\" (
            echo ERROR: Could not remove the incomplete mirror:
            echo        "%MIRROR%"
            goto :FAILED
        )
    )

    git clone "%REPO_URL%" "%MIRROR%"
    if errorlevel 1 (
        echo.
        echo ERROR: Could not clone the GitHub repository.
        echo Check internet access and GitHub authentication.
        goto :FAILED
    )
) else (
    echo Existing mirror found.
)

cd /d "%MIRROR%"
if errorlevel 1 (
    echo ERROR: Could not open:
    echo        "%MIRROR%"
    goto :FAILED
)

git remote set-url origin "%REPO_URL%" >nul 2>&1
git config core.longpaths true >nul 2>&1

REM Give automated commits an identity if Git has none configured.
git config user.name >nul 2>&1
if errorlevel 1 git config user.name "R&D Automatic Backup"

git config user.email >nul 2>&1
if errorlevel 1 git config user.email "backup@localhost"

REM ----- Step 2: Start from latest GitHub state ----------------------
echo [2/7] Reading latest GitHub state...

git fetch origin --prune
if errorlevel 1 (
    echo ERROR: Could not fetch from GitHub.
    goto :FAILED
)

set "BRANCH="
for /f "tokens=3" %%B in ('git remote show origin ^| findstr /C:"HEAD branch:"') do set "BRANCH=%%B"

if /I "!BRANCH!"=="(unknown)" set "BRANCH="

if not defined BRANCH (
    git show-ref --verify --quiet "refs/remotes/origin/main"
    if not errorlevel 1 set "BRANCH=main"
)

if not defined BRANCH (
    git show-ref --verify --quiet "refs/remotes/origin/master"
    if not errorlevel 1 set "BRANCH=master"
)

if not defined BRANCH set "BRANCH=main"

echo Using branch: !BRANCH!

git show-ref --verify --quiet "refs/remotes/origin/!BRANCH!"
if errorlevel 1 (
    git checkout -B "!BRANCH!"
) else (
    git checkout -B "!BRANCH!" "origin/!BRANCH!"
)

if errorlevel 1 (
    echo ERROR: Could not prepare branch !BRANCH!.
    goto :FAILED
)

REM Make the private mirror exactly match the fetched GitHub version
REM before copying the PC backup into it.
git reset --hard >nul 2>&1
git clean -fdx >nul 2>&1

REM ----- Step 3: Copy PC folder to mirror ----------------------------
echo [3/7] Scanning the PC for new, changed and deleted files...
echo.

REM /MIR:
REM   New PC file       = copied
REM   Changed PC file   = replaced
REM   Deleted PC file   = removed from the mirror
REM
REM /XD .git:
REM   Nested project .git metadata is NOT copied.
REM   The actual project source files ARE copied.
REM
REM /XJ:
REM   Prevents junction loops.
robocopy "%SOURCE%" "%MIRROR%" /MIR /COPY:DAT /DCOPY:DAT /R:2 /W:2 /XJ /XD ".git" /NP /NJH /NJS

set "RC=!ERRORLEVEL!"
if !RC! GEQ 8 (
    echo.
    echo ERROR: ROBOCOPY failed with code !RC!.
    goto :FAILED
)

REM ----- Step 4: Convert old GitHub submodule/gitlink entries --------
echo.
echo [4/7] Converting any old nested-Git links into normal folders...

set "FOUND_GITLINK=0"
for /f "tokens=1,2,3,*" %%A in ('git ls-files --stage ^| findstr /B "160000 "') do (
    set "FOUND_GITLINK=1"
    echo Converting: %%D
    git rm -f --cached --ignore-unmatch -- "%%D" >nul 2>&1
)

if "!FOUND_GITLINK!"=="0" echo No Git links need conversion.

REM ----- Step 5: Stage EVERYTHING -----------------------------------
echo [5/7] Detecting all file changes...

REM -A = additions + modifications + deletions
REM -f = also include files ignored by .gitignore
git add -A -f -- .
if errorlevel 1 (
    echo ERROR: Git could not stage the files.
    goto :FAILED
)

echo.
git status --short
echo.

REM ----- Step 6: Commit only if something changed --------------------
echo [6/7] Creating backup commit if needed...

git diff --cached --quiet
if errorlevel 1 (
    git commit -m "Automatic full backup %DATE% %TIME%"
    if errorlevel 1 (
        echo ERROR: Git commit failed.
        goto :FAILED
    )
) else (
    echo No new local changes to commit.
)

REM ----- Step 7: Push ------------------------------------------------
echo [7/7] Uploading backup to GitHub...

git push -u origin "!BRANCH!"
if errorlevel 1 (
    echo.
    echo ERROR: GitHub push failed.
    echo.
    echo If GitHub mentions a file larger than 100 MB,
    echo that file requires Git LFS and normal Git cannot upload it.
    echo.
    echo If a GitHub login window opens, sign in and run this BAT again.
    goto :FAILED
)

echo.
echo ================================================================
echo                  BACKUP COMPLETED SUCCESSFULLY
echo ================================================================
echo.
echo New files       : uploaded
echo Modified files  : updated
echo Deleted files   : removed from GitHub
echo Ignored files   : force-added and uploaded
echo Nested projects : source files uploaded, nested .git data skipped
echo.
echo NOTE: Git/GitHub cannot store a truly empty folder by itself.
echo       Once the folder contains any file, it is backed up.
echo.
>>"%LOG%" echo [%DATE% %TIME%] SUCCESS - !BRANCH!
echo This window will close in 8 seconds...
timeout /t 8 /nobreak >nul
exit /b 0

:FAILED
echo.
echo ================================================================
echo                         BACKUP FAILED
echo ================================================================
echo.
echo Your original files in "%SOURCE%" were NOT intentionally deleted.
echo.
>>"%LOG%" echo [%DATE% %TIME%] FAILED
pause
exit /b 1
