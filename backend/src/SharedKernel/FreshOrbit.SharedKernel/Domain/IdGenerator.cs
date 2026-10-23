namespace FreshOrbit.SharedKernel.Domain;

/// <summary>Generates time-ordered UUIDv7 keys.</summary>
public static class IdGenerator
{
    public static Guid NewId() => Guid.CreateVersion7();

    public static Guid NewId(DateTimeOffset timestamp) => Guid.CreateVersion7(timestamp);
}
