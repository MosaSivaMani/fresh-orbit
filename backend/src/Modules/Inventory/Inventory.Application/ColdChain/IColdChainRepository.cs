using FreshOrbit.Inventory.Domain.ColdChain;

namespace FreshOrbit.Inventory.Application.ColdChain;

/// <summary>Cold-chain logs (insert-only) and the storage-unit ranges reference data.</summary>
public interface IColdChainRepository
{
    /// <summary>The range of a storage unit code, or null when the code is not configured.</summary>
    Task<StorageUnitRange?> GetRangeAsync(string storageUnitCode, CancellationToken cancellationToken = default);

    void Add(ColdChainLog log);
}
