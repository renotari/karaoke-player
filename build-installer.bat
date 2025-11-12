@echo off
REM Installer build script for Karaoke Player

echo ========================================
echo Karaoke Player - Installer Build Script
echo ========================================
echo.

REM Check if Inno Setup is installed
set INNO_SETUP="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist %INNO_SETUP% (
    echo ERROR: Inno Setup 6 not found!
    echo.
    echo Please install Inno Setup 6 from:
    echo https://jrsoftware.org/isinfo.php
    echo.
    echo Default installation path: C:\Program Files (x86)\Inno Setup 6\
    echo.
    pause
    exit /b 1
)

REM Check if release build exists
if not exist "bin\Release\net8.0\win-x64\publish\KaraokePlayer.exe" (
    echo ERROR: Release build not found!
    echo.
    echo Please run build-release.bat first to create the release build.
    echo.
    pause
    exit /b 1
)

REM Create installer directory
if not exist "installer" mkdir "installer"

REM Compile installer
echo [1/1] Compiling installer with Inno Setup...
%INNO_SETUP% "Setup.iss"
if %errorlevel% neq 0 (
    echo ERROR: Installer compilation failed
    pause
    exit /b %errorlevel%
)
echo Done.
echo.

echo ========================================
echo Installer created successfully!
echo ========================================
echo.
echo Installer location: installer\KaraokePlayer-Setup-1.0.0.exe
echo.
echo Next steps:
echo 1. Test the installer on a clean Windows system
echo 2. Verify all features work correctly after installation
echo 3. Distribute the installer to users
echo.
pause
