namespace RfidVehicleAccess.Services;

public sealed class BalanceUpdateCoordinator
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

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
