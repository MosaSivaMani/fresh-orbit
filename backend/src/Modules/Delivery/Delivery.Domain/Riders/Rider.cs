using FreshOrbit.SharedKernel.Domain;

namespace FreshOrbit.Delivery.Domain.Riders;

/// <summary>A delivery rider of one fulfillment location (no login in v1, D12).</summary>
public sealed class Rider : AggregateRoot
{
    public const int MaxFullNameLength = 120;
    public const int MaxPhoneLength = 16;

    private Rider()
    {
        FullName = string.Empty;
        Phone = string.Empty;
    }

    private Rider(Guid fulfillmentLocationId, string fullName, string phone)
    {
        FulfillmentLocationId = fulfillmentLocationId;
        FullName = fullName;
        Phone = phone;
        IsActive = true;
    }

    public Guid FulfillmentLocationId { get; private set; }

    public string FullName { get; private set; }

    /// <summary>E.164.</summary>
    public string Phone { get; private set; }

    public bool IsActive { get; private set; }

    public static Rider Create(Guid fulfillmentLocationId, string fullName, string phone)
        => new(fulfillmentLocationId, fullName.Trim(), phone);

    public void Update(string fullName, string phone, bool isActive)
    {
        FullName = fullName.Trim();
        Phone = phone;
        IsActive = isActive;
    }
}

public interface IRiderRepository
{
    Task<Rider?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ActivePhoneExistsAsync(string phone, Guid? exceptRiderId, CancellationToken cancellationToken);

    void Add(Rider rider);
}
