namespace FreshOrbit.SharedKernel.EntityFramework.Seeding;

/// <summary>
/// Loads a module's real reference data (roles, permissions, preparation methods, templates, tax categories)
/// from <c>backend/data/reference</c>. Runs on every deploy after migrations, so it must be idempotent
/// (insert missing, update changed). Loaders run in ascending <see cref="Order"/>; each saves its own changes.
/// </summary>
public interface IReferenceDataLoader
{
    int Order { get; }

    Task LoadAsync(ReferenceDataContext context, CancellationToken cancellationToken);
}

public sealed record ReferenceDataContext(string DirectoryPath);
