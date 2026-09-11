namespace RfidVehicleAccess.Services;

/// <summary>
/// Serializes background trip synchronization and destructive pending-data maintenance.
/// This prevents a pending transaction from being published while the operator is clearing it.
/// </summary>
public sealed class TripSyncCoordinator
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task RunAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await operation();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<T> RunAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            _mutex.Release();
        }
    }
}
