using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace KaraokePlayer.Services;

/// <summary>
/// Performance monitoring service for tracking operation timings and metrics
/// Useful for profiling and optimizing the application
/// </summary>
public class PerformanceMonitor
{
    private readonly ConcurrentDictionary<string, PerformanceMetric> _metrics;
    private readonly ILoggingService? _loggingService;

    public PerformanceMonitor(ILoggingService? loggingService = null)
    {
        _metrics = new ConcurrentDictionary<string, PerformanceMetric>();
        _loggingService = loggingService;
    }

    /// <summary>
    /// Start timing an operation
    /// </summary>
    public IDisposable MeasureOperation(string operationName)
    {
        return new OperationTimer(this, operationName);
    }

    /// <summary>
    /// Record an operation timing
    /// </summary>
    public void RecordTiming(string operationName, TimeSpan duration)
    {
        var metric = _metrics.GetOrAdd(operationName, _ => new PerformanceMetric(operationName));
        metric.RecordTiming(duration);

        // Log slow operations (> 300ms for search, > 100ms for UI operations)
        var threshold = operationName.Contains("Search") ? 300 : 100;
        if (duration.TotalMilliseconds > threshold)
        {
            _loggingService?.LogWarning($"Slow operation: {operationName} took {duration.TotalMilliseconds:F2}ms");
        }
    }

    /// <summary>
    /// Get metrics for an operation
    /// </summary>
    public PerformanceMetric? GetMetrics(string operationName)
    {
        _metrics.TryGetValue(operationName, out var metric);
        return metric;
    }

    /// <summary>
    /// Get all recorded metrics
    /// </summary>
    public ConcurrentDictionary<string, PerformanceMetric> GetAllMetrics()
    {
        return _metrics;
    }

    /// <summary>
    /// Clear all metrics
    /// </summary>
    public void ClearMetrics()
    {
        _metrics.Clear();
    }

    /// <summary>
    /// Log performance summary
    /// </summary>
    public void LogSummary()
    {
        if (_loggingService == null)
            return;

        _loggingService.LogInformation("=== Performance Summary ===");
        foreach (var kvp in _metrics)
        {
            var metric = kvp.Value;
            _loggingService.LogInformation(
                $"{metric.OperationName}: " +
                $"Count={metric.Count}, " +
                $"Avg={metric.AverageMs:F2}ms, " +
                $"Min={metric.MinMs:F2}ms, " +
                $"Max={metric.MaxMs:F2}ms");
        }
    }

    private class OperationTimer : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;

        public OperationTimer(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.RecordTiming(_operationName, _stopwatch.Elapsed);
        }
    }
}

/// <summary>
/// Performance metric for tracking operation timings
/// </summary>
public class PerformanceMetric
{
    private readonly object _lock = new object();
    private long _count;
    private double _totalMs;
    private double _minMs = double.MaxValue;
    private double _maxMs;

    public string OperationName { get; }
    public long Count => _count;
    public double AverageMs => _count > 0 ? _totalMs / _count : 0;
    public double MinMs => _minMs == double.MaxValue ? 0 : _minMs;
    public double MaxMs => _maxMs;

    public PerformanceMetric(string operationName)
    {
        OperationName = operationName;
    }

    public void RecordTiming(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;

        lock (_lock)
        {
            _count++;
            _totalMs += ms;
            _minMs = Math.Min(_minMs, ms);
            _maxMs = Math.Max(_maxMs, ms);
        }
    }
}
