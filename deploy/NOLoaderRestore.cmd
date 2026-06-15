@echo off
setlocal
cd /d "%~dp0"
if not exist "NOLoaderRestore.exe" (
    echo NOLoaderRestore.exe not found in game folder.
    echo Run NOLoader deploy once, or use Steam verify integrity.
    pause
    exit /b 1
)
echo Restoring vanilla managed DLLs (close Nuclear Option first)...
"NOLoaderRestore.exe" restore-vanilla "%~dp0"
if errorlevel 1 (
    echo Restore failed. Close the game and try Steam verify integrity.
    pause
    exit /b 1
)
echo Restore OK. You can launch Nuclear Option without NOLoader.
pause
