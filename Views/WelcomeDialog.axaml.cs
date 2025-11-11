using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Threading.Tasks;
using KaraokePlayer.Services;

namespace KaraokePlayer.Views;

public partial class WelcomeDialog : Window
{
    private readonly IMediaLibraryManager? _mediaLibraryManager;
    private readonly IMetadataExtractor? _metadataExtractor;
    private readonly IThumbnailGenerator? _thumbnailGenerator;
    private string? _selectedDirectory;
    private bool _isScanning;

    public string? SelectedMediaDirectory => _selectedDirectory;
    public bool ScanCompleted { get; private set; }

    public WelcomeDialog()
    {
        InitializeComponent();
        SetDefaultDirectory();
    }

    public WelcomeDialog(
        IMediaLibraryManager mediaLibraryManager,
        IMetadataExtractor metadataExtractor,
        IThumbnailGenerator thumbnailGenerator) : this()
    {
        _mediaLibraryManager = mediaLibraryManager ?? throw new ArgumentNullException(nameof(mediaLibraryManager));
        _metadataExtractor = metadataExtractor ?? throw new ArgumentNullException(nameof(metadataExtractor));
        _thumbnailGenerator = thumbnailGenerator ?? throw new ArgumentNullException(nameof(thumbnailGenerator));

        // Subscribe to scan progress events
        if (_mediaLibraryManager != null)
        {
            _mediaLibraryManager.ScanProgress += OnScanProgress;
        }
    }

    private void SetDefaultDirectory()
    {
        // Default to Windows user music directory
        var defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (Directory.Exists(defaultDirectory))
        {
            _selectedDirectory = defaultDirectory;
            var textBox = this.FindControl<TextBox>("MediaDirectoryTextBox");
            if (textBox != null)
            {
                textBox.Text = defaultDirectory;
            }
            
            var continueButton = this.FindControl<Button>("ContinueButton");
            if (continueButton != null)
            {
                continueButton.IsEnabled = true;
            }
        }
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Media Directory",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var folder = folders[0];
            _selectedDirectory = folder.Path.LocalPath;
            
            var textBox = this.FindControl<TextBox>("MediaDirectoryTextBox");
            if (textBox != null)
            {
                textBox.Text = _selectedDirectory;
            }

            var continueButton = this.FindControl<Button>("ContinueButton");
            if (continueButton != null)
            {
                continueButton.IsEnabled = true;
            }
        }
    }

    private async void OnContinueClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedDirectory) || _isScanning)
            return;

        if (!Directory.Exists(_selectedDirectory))
        {
            // Show error
            var errorDialog = new Window
            {
                Title = "Error",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock 
            { 
                Text = "The selected directory does not exist. Please choose a valid directory.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });
            
            var okButton = new Button 
            { 
                Content = "OK", 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Width = 100
            };
            okButton.Click += (s, args) => errorDialog.Close();
            panel.Children.Add(okButton);
            
            errorDialog.Content = panel;
            await errorDialog.ShowDialog(this);
            return;
        }

        await PerformInitialScan();
    }

    private async Task PerformInitialScan()
    {
        if (_mediaLibraryManager == null)
        {
            // No services available, just close
            ScanCompleted = true;
            Close();
            return;
        }

        _isScanning = true;

        // Disable buttons and show progress
        var continueButton = this.FindControl<Button>("ContinueButton");
        var cancelButton = this.FindControl<Button>("CancelButton");
        var browseButton = this.FindControl<Button>("BrowseButton");
        var progressPanel = this.FindControl<StackPanel>("ProgressPanel");

        if (continueButton != null) continueButton.IsEnabled = false;
        if (cancelButton != null) cancelButton.IsEnabled = false;
        if (browseButton != null) browseButton.IsEnabled = false;
        if (progressPanel != null) progressPanel.IsVisible = true;

        try
        {
            // Perform the scan
            await _mediaLibraryManager.ScanDirectoryAsync(_selectedDirectory!);

            // Start monitoring for auto-refresh
            _mediaLibraryManager.StartMonitoring();

            // Start background processing for metadata and thumbnails
            _ = Task.Run(async () =>
            {
                try
                {
                    var files = await _mediaLibraryManager.GetMediaFilesAsync();
                    
                    // Queue files for background processing
                    foreach (var file in files)
                    {
                        if (!file.MetadataLoaded && _metadataExtractor != null)
                        {
                            _metadataExtractor.QueueForExtraction(file);
                        }

                        if (!file.ThumbnailLoaded && _thumbnailGenerator != null)
                        {
                            _thumbnailGenerator.QueueForGeneration(file);
                        }
                    }

                    // Start the background processors
                    _metadataExtractor?.StartProcessing();
                    _thumbnailGenerator?.StartProcessing();
                }
                catch
                {
                    // Background processing errors are non-critical
                }
            });

            ScanCompleted = true;
            
            // Update UI to show completion
            var progressText = this.FindControl<TextBlock>("ProgressText");
            if (progressText != null)
            {
                progressText.Text = "Scan complete! Starting application...";
            }

            // Wait a moment to show completion message
            await Task.Delay(1000);

            Close();
        }
        catch (Exception ex)
        {
            _isScanning = false;

            // Show error dialog
            var errorDialog = new Window
            {
                Title = "Scan Error",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock 
            { 
                Text = $"An error occurred while scanning the directory:\n\n{ex.Message}",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });
            
            var okButton = new Button 
            { 
                Content = "OK", 
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Width = 100
            };
            okButton.Click += (s, args) => errorDialog.Close();
            panel.Children.Add(okButton);
            
            errorDialog.Content = panel;
            await errorDialog.ShowDialog(this);

            // Re-enable buttons
            if (continueButton != null) continueButton.IsEnabled = true;
            if (cancelButton != null) cancelButton.IsEnabled = true;
            if (browseButton != null) browseButton.IsEnabled = true;
            if (progressPanel != null) progressPanel.IsVisible = false;
        }
    }

    private void OnScanProgress(object? sender, ScanProgressEventArgs e)
    {
        // Update progress on UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var progressBar = this.FindControl<ProgressBar>("ScanProgressBar");
            var progressDetails = this.FindControl<TextBlock>("ProgressDetails");

            if (progressBar != null && e.TotalFiles > 0)
            {
                progressBar.Value = (double)e.FilesProcessed / e.TotalFiles * 100;
            }

            if (progressDetails != null)
            {
                progressDetails.Text = $"Processing: {e.CurrentFile} ({e.FilesProcessed} of {e.TotalFiles})";
            }
        });
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (!_isScanning)
        {
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from events
        if (_mediaLibraryManager != null)
        {
            _mediaLibraryManager.ScanProgress -= OnScanProgress;
        }

        base.OnClosed(e);
    }
}
