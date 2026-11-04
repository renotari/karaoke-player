using System.Reactive.Concurrency;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KaraokePlayer.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Scheduler that runs on the UI thread. Must be read on the UI thread
    /// (e.g. in a ViewModel constructor), where Avalonia's SynchronizationContext is current.
    /// Used instead of RxApp.MainThreadScheduler, which is not bound to Avalonia's
    /// dispatcher unless the ReactiveUI.Avalonia integration is installed.
    /// </summary>
    protected static IScheduler UiScheduler =>
        SynchronizationContext.Current is { } context
            ? new SynchronizationContextScheduler(context)
            : CurrentThreadScheduler.Instance;
}
