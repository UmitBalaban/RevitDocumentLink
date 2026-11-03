using System.Collections.Concurrent;
using Autodesk.Revit.UI;

namespace DocumentLink.Infrastructure;

/// <summary>
/// Runs code in a valid Revit API context from a modeless WPF window.
/// Usage: var n = await RevitTask.RunAsync(app => new FilteredElementCollector(app.ActiveUIDocument.Document)...Count());
/// </summary>
public static class RevitTask
{
    private static ExternalEvent? _event;
    private static readonly ConcurrentQueue<Action<UIApplication>> Queue = new();

    /// <summary>Must be called from IExternalApplication.OnStartup (API context).</summary>
    public static void Initialize()
    {
        _event ??= ExternalEvent.Create(new Handler());
    }

    public static Task<T> RunAsync<T>(Func<UIApplication, T> func)
    {
        if (_event is null) throw new InvalidOperationException("RevitTask not initialized.");
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Enqueue(app =>
        {
            try { tcs.SetResult(func(app)); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        var req = _event.Raise();
        if (req is ExternalEventRequest.Denied or ExternalEventRequest.TimedOut)
            Log.Info($"ExternalEvent raise returned {req}; request stays queued.");
        return tcs.Task;
    }

    public static Task RunAsync(Action<UIApplication> action) =>
        RunAsync<bool>(app => { action(app); return true; });

    private sealed class Handler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            while (Queue.TryDequeue(out var work))
            {
                try { work(app); }
                catch (Exception ex) { Log.Error("RevitTask work item failed", ex); }
            }
        }

        public string GetName() => "DocumentLink.RevitTask";
    }
}
