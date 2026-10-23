namespace FreshOrbit.Catalog.Domain.Preparations;

public interface IPreparationMethodRepository
{
    /// <summary>Every preparation method (16 reference rows), for code ↔ id mapping.</summary>
    Task<IReadOnlyList<PreparationMethod>> ListAsync(CancellationToken cancellationToken);
}
