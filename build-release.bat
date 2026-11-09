@echo off
REM Build script for Karaoke Player Release

echo ========================================
echo Karaoke Player - Release Build Script
echo ========================================
echo.

REM Clean previous builds
echo [1/3] Cleaning previous builds...
if exist "bin\Release" rmdir /s /q "bin\Release"
if exist "obj\Release" rmdir /s /q "obj\Release"
if exist "installer" rmdir /s /q "installer"
echo Done.
echo.

REM Restore dependencies
echo [2/3] Restoring dependencies...
dotnet restore
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore dependencies
    pause
    exit /b %errorlevel%
)
echo Done.
echo.

REM Publish (builds too): self-contained, with LibVLC
echo [3/3] Building and publishing self-contained application...
dotnet publish -c Release -r win-x64 --self-contained --no-restore
if %errorlevel% neq 0 (
    echo ERROR: Publish failed
    pause
    exit /b %errorlevel%
)
if not exist "bin\Release\net10.0\win-x64\publish\libvlc\win-x64\libvlc.dll" (
    echo ERROR: LibVLC is missing from the publish folder
    pause
    exit /b 1
)
echo Done.
echo.

echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output location: bin\Release\net10.0\win-x64\publish\
echo.
echo Next steps:
echo 1. Test the application in bin\Release\net10.0\win-x64\publish\
echo 2. Run build-installer.bat to create the installer
echo.
pause
