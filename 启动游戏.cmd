@echo off
cd /d "%~dp0"
if exist "%~dp0artifacts\releases\InfiniteDao-Godot-Windows\InfiniteDao.exe" (
    start "" "%~dp0artifacts\releases\InfiniteDao-Godot-Windows\InfiniteDao.exe" -- --data-root "%~dp0."
    exit /b 0
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\start-game.ps1"
if errorlevel 1 pause
