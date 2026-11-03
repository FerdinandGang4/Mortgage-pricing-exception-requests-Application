namespace PricingExceptions.API.Services;

// Serializes writes within this API process because EF InMemory has no transactions.
// Replace with database transactions/constraints when adopting relational storage.
public sealed class InMemoryWriteLock : IDisposable
{
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
