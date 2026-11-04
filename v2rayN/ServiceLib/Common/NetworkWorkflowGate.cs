namespace ServiceLib.Common;

internal sealed class NetworkWorkflowGate(Action<bool> setRunning)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsBusy => _gate.CurrentCount == 0;

    public async Task RunAsync(Func<Task> work, bool wait)
    {
        if (!await _gate.WaitAsync(wait ? Timeout.Infinite : 0))
        {
            return;
        }

        try
        {
            setRunning(true);
            await work();
        }
        finally
        {
            try
            {
                setRunning(false);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
