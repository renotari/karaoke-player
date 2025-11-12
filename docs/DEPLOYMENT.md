# Deployment Guide

This guide covers building, packaging, and deploying the Karaoke Player application.

## Prerequisites

### Development Environment
- Windows 10/11 (64-bit)
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
- Git (for version control)
- Visual Studio 2022 or JetBrains Rider (optional, for development)

### Deployment Tools
- Inno Setup 6: https://jrsoftware.org/isinfo.php (for creating installers)

## Build Process

### Option 1: Using Build Scripts (Recommended)

#### Step 1: Build Release Version

Run the automated build script:
```cmd
build-release.bat
```

This script will:
1. Clean previous builds
2. Restore NuGet dependencies
3. Build the Release configuration
4. Publish a self-contained Windows x64 application

Output location: `bin\Release\net8.0\win-x64\publish\`

#### Step 2: Create Installer

Run the installer build script:
```cmd
build-installer.bat
```

This script will:
1. Verify Inno Setup is installed
2. Check that the release build exists
3. Compile the installer using `Setup.iss`

Output location: `installer\KaraokePlayer-Setup-1.0.0.exe`

### Option 2: Manual Build

#### Build Release

```cmd
dotnet restore
dotnet build --configuration Release
dotnet publish -c Release -r win-x64 --self-contained
```

#### Create Installer

1. Open `Setup.iss` in Inno Setup
2. Click **Build** > **Compile**
3. Installer will be created in `installer\` directory

## Configuration Files

### KaraokePlayer.csproj

Key configuration settings:

```xml
<PropertyGroup>
  <!-- Application Metadata -->
  <Version>1.0.0</Version>
  <Product>Karaoke Player</Product>
  <Description>Professional karaoke and media player</Description>
  
  <!-- Publishing -->
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

**Note**: `PublishSingleFile` is set to `false` because LibVLC requires separate native DLL files.

### Setup.iss (Inno Setup Script)

Key configuration:

```ini
#define MyAppVersion "1.0.0"
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
DefaultDirName={autopf}\Karaoke Player
OutputBaseFilename=KaraokePlayer-Setup-{#MyAppVersion}
```

**Important**: Change the `AppId` GUID if you fork this project to avoid conflicts.

## Testing the Build

### Pre-Installation Testing

1. Navigate to `bin\Release\net8.0\win-x64\publish\`
2. Run `KaraokePlayer.exe` directly
3. Verify all features work:
   - Media library scanning
   - Video/audio playback
   - Playlist management
   - Settings persistence
   - Window modes (single/dual screen)
   - Crossfade transitions
   - Audio visualizations

### Installation Testing

**Test on a Clean System** (recommended):

1. Use a clean Windows VM or test machine
2. Ensure .NET 8 Runtime is NOT pre-installed (to test the installer check)
3. Run `KaraokePlayer-Setup-1.0.0.exe`
4. Follow the installation wizard
5. Verify:
   - .NET 8 Runtime check works
   - Application installs to correct location
   - Desktop/Start Menu shortcuts are created
   - Application launches successfully
   - First-run experience works correctly
   - All features function as expected

### Post-Installation Verification

Check these locations:

- **Installation**: `C:\Program Files\Karaoke Player\`
- **Application Data**: `%APPDATA%\KaraokePlayer\`
- **Desktop Shortcut**: Desktop icon present
- **Start Menu**: Start Menu entry present

## Versioning

### Updating Version Numbers

When releasing a new version, update these files:

1. **KaraokePlayer.csproj**:
   ```xml
   <Version>1.1.0</Version>
   <AssemblyVersion>1.1.0.0</AssemblyVersion>
   <FileVersion>1.1.0.0</FileVersion>
   ```

2. **Setup.iss**:
   ```ini
   #define MyAppVersion "1.1.0"
   ```

3. **README.md**:
   ```markdown
   ![Version](https://img.shields.io/badge/version-1.1.0-blue)
   ```

### Version Numbering Scheme

Follow Semantic Versioning (SemVer): `MAJOR.MINOR.PATCH`

- **MAJOR**: Breaking changes or major feature additions
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

## Distribution

### Release Checklist

Before distributing a new version:

- [ ] All tests pass
- [ ] Version numbers updated in all files
- [ ] README.md updated with new features/changes
- [ ] CHANGELOG.md updated (if you maintain one)
- [ ] Release build tested on clean system
- [ ] Installer tested on clean system
- [ ] All features verified working
- [ ] Performance benchmarks met
- [ ] Documentation updated

### Distribution Channels

1. **GitHub Releases**:
   - Create a new release tag (e.g., `v1.0.0`)
   - Upload `KaraokePlayer-Setup-1.0.0.exe`
   - Include release notes

2. **Direct Download**:
   - Host installer on your website
   - Provide SHA256 checksum for verification

3. **Package Managers** (future):
   - Chocolatey
   - WinGet

### Generating Checksums

For security verification:

```cmd
certutil -hashfile installer\KaraokePlayer-Setup-1.0.0.exe SHA256
```

Include the checksum in release notes.

## Troubleshooting Build Issues

### Common Build Errors

**Error: NuGet packages not restored**
```cmd
dotnet restore --force
```

**Error: LibVLC binaries not copied**
- Verify `VideoLAN.LibVLC.Windows` package is installed
- Check the `<None Include>` section in `.csproj` for LibVLC files

**Error: Inno Setup not found**
- Install Inno Setup 6 from https://jrsoftware.org/isinfo.php
- Verify installation path in `build-installer.bat`

**Error: .NET 8 SDK not found**
- Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0
- Restart command prompt after installation

### Build Performance

**Slow publish times**:
- Use `--no-build` flag if already built: `dotnet publish -c Release -r win-x64 --self-contained --no-build`
- Enable parallel builds: `dotnet build -m`

**Large output size**:
- Self-contained builds include .NET runtime (~70MB)
- LibVLC binaries add ~50MB
- Total installer size: ~150-200MB (compressed)

## Continuous Integration (CI)

### GitHub Actions Example

Create `.github/workflows/build.yml`:

```yaml
name: Build Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Publish
      run: dotnet publish -c Release -r win-x64 --self-contained --no-build
    
    - name: Upload artifact
      uses: actions/upload-artifact@v3
      with:
        name: karaoke-player-release
        path: bin/Release/net8.0/win-x64/publish/
```

## Security Considerations

### Code Signing (Recommended for Production)

For production releases, sign the executable and installer:

1. Obtain a code signing certificate
2. Sign the executable:
   ```cmd
   signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com bin\Release\net8.0\win-x64\publish\KaraokePlayer.exe
   ```
3. Sign the installer:
   ```cmd
   signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com installer\KaraokePlayer-Setup-1.0.0.exe
   ```

**Benefits**:
- Prevents Windows SmartScreen warnings
- Verifies publisher identity
- Increases user trust

### Security Best Practices

- Never commit certificates or private keys to version control
- Use environment variables for sensitive data in CI/CD
- Scan builds with antivirus before distribution
- Provide checksums for download verification

## Support and Maintenance

### Log Files

Users can find logs at: `%APPDATA%\KaraokePlayer\logs\`

Request these files when troubleshooting user issues.

### Crash Reports

The application logs errors to:
- Application logs: `%APPDATA%\KaraokePlayer\logs\app.log`
- Windows Event Viewer: Application logs

### Update Strategy

For future versions:
1. Maintain backward compatibility for settings files
2. Provide migration scripts for database schema changes
3. Test upgrades from previous versions
4. Consider implementing auto-update functionality

## Additional Resources

- **.NET Publishing**: https://learn.microsoft.com/en-us/dotnet/core/deploying/
- **Inno Setup Documentation**: https://jrsoftware.org/ishelp/
- **Avalonia Deployment**: https://docs.avaloniaui.net/docs/deployment/
- **LibVLC Documentation**: https://www.videolan.org/developers/vlc/doc/

---

**Last Updated**: November 2024  
**Version**: 1.0.0
