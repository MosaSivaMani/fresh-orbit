namespace FreshOrbit.Locations.Domain.Cities;

public interface ICityRepository
{
    Task<City?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    void Add(City city);
}
