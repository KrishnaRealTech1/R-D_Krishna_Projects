@echo off
cd /d "D:\R&D Projects"

git add -A

git diff --cached --quiet
if %errorlevel%==0 (
    exit /b 0
)

git commit -m "Automatic project backup"
git push origin master