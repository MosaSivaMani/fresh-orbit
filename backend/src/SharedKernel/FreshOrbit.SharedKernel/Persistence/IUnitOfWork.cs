using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.SharedKernel.Persistence;

/// <summary>
/// The single save of a command. Implemented by <c>FreshOrbitDbContext</c>: it stamps audit columns, writes
/// raised domain events to the outbox and translates a lost race into <c>ConcurrentSaveException</c>.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the version the client quoted in If-Match against the loaded row version and throws
    /// <c>ConcurrencyException</c> (412) when they differ.
    /// </summary>
    void SetExpectedVersion(IAggregateRoot aggregate, uint expectedVersion);
}
