@echo off
REM Build script for Karaoke Player Release

echo ========================================
echo Karaoke Player - Release Build Script
echo ========================================
echo.

REM Clean previous builds
echo [1/4] Cleaning previous builds...
if exist "bin\Release" rmdir /s /q "bin\Release"
if exist "obj\Release" rmdir /s /q "obj\Release"
if exist "installer" rmdir /s /q "installer"
echo Done.
echo.

REM Restore dependencies
echo [2/4] Restoring dependencies...
dotnet restore
if %errorlevel% neq 0 (
    echo ERROR: Failed to restore dependencies
    pause
    exit /b %errorlevel%
)
echo Done.
echo.

REM Build release version
echo [3/4] Building release version...
dotnet build --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo ERROR: Build failed
    pause
    exit /b %errorlevel%
)
echo Done.
echo.

REM Publish self-contained application
echo [4/4] Publishing self-contained application...
dotnet publish -c Release -r win-x64 --self-contained --no-build
if %errorlevel% neq 0 (
    echo ERROR: Publish failed
    pause
    exit /b %errorlevel%
)
echo Done.
echo.

echo ========================================
echo Build completed successfully!
echo ========================================
echo.
echo Output location: bin\Release\net8.0\win-x64\publish\
echo.
echo Next steps:
echo 1. Test the application in bin\Release\net8.0\win-x64\publish\
echo 2. Run build-installer.bat to create the installer
echo.
pause
