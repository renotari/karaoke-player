# Karaoke Player

A professional karaoke and media player application built with .NET 8 and Avalonia UI.

## Features

- Support for multiple media formats (MP4, MKV, WEBM, MP3)
- Advanced crossfade transitions between tracks
- Audio visualizations for MP3 playback
- Flexible single/dual-screen configurations
- Playlist management with save/load functionality
- Fast search across large media libraries (up to 30,000 files)
- Customizable settings and keyboard shortcuts

## Technology Stack

- **.NET 8** - Cross-platform application framework
- **Avalonia UI** - Modern XAML-based UI framework
- **LibVLCSharp** - Professional media playback engine
- **Entity Framework Core + SQLite** - Fast indexed media library
- **TagLibSharp** - Audio metadata extraction
- **SkiaSharp** - Hardware-accelerated graphics
- **ReactiveUI** - Reactive MVVM pattern

## Project Structure

```
KaraokePlayer/
├── Models/          # Data models and entities
├── ViewModels/      # MVVM view models
├── Views/           # XAML views and windows
├── Services/        # Application services
└── Assets/          # Images, icons, and resources
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Windows OS (primary target)

### Building

```bash
dotnet restore
dotnet build
```

### Running

```bash
dotnet run
```

## Development

This project follows the MVVM (Model-View-ViewModel) pattern with:
- **Models**: Data structures and business entities
- **ViewModels**: Presentation logic and state management
- **Views**: UI components and layouts
- **Services**: Reusable business logic and infrastructure

## License

TBD
