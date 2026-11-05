namespace PricingExceptions.API.Services;

// Coordinates workflow reads and writes within this process because EF InMemory has no transactions.
// Replace with database transactions/constraints when adopting relational storage.
public sealed class InMemoryWriteLock : IDisposable
{
    // Registered as a singleton so separate request scopes use the same gate.
    // This does not coordinate separate API instances or provide database rollback.
    internal SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
