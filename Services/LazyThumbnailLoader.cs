using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KaraokePlayer.Models;

namespace KaraokePlayer.Services;

/// <summary>
/// Lazy thumbnail loader that loads thumbnails on-demand as items scroll into view
/// Implements a queue-based loading system with priority for visible items
/// </summary>
public class LazyThumbnailLoader : IDisposable
{
    private readonly IThumbnailGenerator _thumbnailGenerator;
    private readonly ConcurrentQueue<MediaFile> _loadQueue;
    private readonly HashSet<string> _loadedThumbnails;
    private readonly HashSet<string> _queuedThumbnails;
    private readonly SemaphoreSlim _semaphore;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _processingTask;
    private bool _disposed;

    public LazyThumbnailLoader(IThumbnailGenerator thumbnailGenerator)
    {
        _thumbnailGenerator = thumbnailGenerator ?? throw new ArgumentNullException(nameof(thumbnailGenerator));
        _loadQueue = new ConcurrentQueue<MediaFile>();
        _loadedThumbnails = new HashSet<string>();
        _queuedThumbnails = new HashSet<string>();
        _semaphore = new SemaphoreSlim(0);
        _cancellationTokenSource = new CancellationTokenSource();
        
        // Start background processing task
        _processingTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Request thumbnail loading for a media file
    /// Only queues if not already loaded or queued
    /// </summary>
    public void RequestThumbnail(MediaFile mediaFile)
    {
        if (mediaFile == null || string.IsNullOrEmpty(mediaFile.Id))
            return;

        // Skip if already loaded or queued
        lock (_loadedThumbnails)
        {
            if (_loadedThumbnails.Contains(mediaFile.Id) || _queuedThumbnails.Contains(mediaFile.Id))
                return;

            _queuedThumbnails.Add(mediaFile.Id);
        }

        // Add to queue
        _loadQueue.Enqueue(mediaFile);
        _semaphore.Release();
    }

    /// <summary>
    /// Request thumbnails for multiple media files (batch request)
    /// </summary>
    public void RequestThumbnails(IEnumerable<MediaFile> mediaFiles)
    {
        foreach (var file in mediaFiles)
        {
            RequestThumbnail(file);
        }
    }

    /// <summary>
    /// Clear the loading queue (useful when scrolling quickly)
    /// </summary>
    public void ClearQueue()
    {
        while (_loadQueue.TryDequeue(out _)) { }
        
        lock (_loadedThumbnails)
        {
            _queuedThumbnails.Clear();
        }
    }

    /// <summary>
    /// Background task that processes the thumbnail loading queue
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Wait for items in queue
                await _semaphore.WaitAsync(_cancellationTokenSource.Token);

                if (_loadQueue.TryDequeue(out var mediaFile))
                {
                    try
                    {
                        // Generate thumbnail if not already loaded
                        if (!mediaFile.ThumbnailLoaded)
                        {
                            if (mediaFile.Type == MediaType.Video)
                            {
                                var thumbnailPath = await _thumbnailGenerator.GenerateVideoThumbnailAsync(mediaFile);
                                mediaFile.ThumbnailPath = thumbnailPath;
                            }
                            else if (mediaFile.Type == MediaType.Audio)
                            {
                                var thumbnailPath = await _thumbnailGenerator.ExtractAudioArtworkAsync(mediaFile);
                                mediaFile.ThumbnailPath = thumbnailPath ?? _thumbnailGenerator.CreateDefaultThumbnail(mediaFile.Type);
                            }

                            mediaFile.ThumbnailLoaded = true;
                        }

                        // Mark as loaded
                        lock (_loadedThumbnails)
                        {
                            _loadedThumbnails.Add(mediaFile.Id);
                            _queuedThumbnails.Remove(mediaFile.Id);
                        }
                    }
                    catch (Exception)
                    {
                        // Failed to load thumbnail - mark as loaded anyway to prevent retries
                        lock (_loadedThumbnails)
                        {
                            _loadedThumbnails.Add(mediaFile.Id);
                            _queuedThumbnails.Remove(mediaFile.Id);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Continue processing on error
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cancellationTokenSource.Cancel();
        _semaphore.Dispose();
        _cancellationTokenSource.Dispose();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore timeout
        }
    }
}
