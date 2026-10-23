namespace FreshOrbit.SharedKernel.Web.Auth;

/// <summary>JWT claim names issued by Identity and read by <see cref="HttpCurrentActor"/>.</summary>
public static class FreshOrbitClaimTypes
{
    public const string Subject = "sub";
    public const string ActorType = "actor_type";
    public const string CustomerId = "customer_id";
    public const string StaffMemberId = "staff_id";

    /// <summary>One claim per permission code, e.g. <c>orders.pack</c>.</summary>
    public const string Permission = "perm";

    /// <summary>One claim per fulfillment location id the staff member may act on.</summary>
    public const string Location = "loc";

    /// <summary><c>true</c> when the staff member may act on every location.</summary>
    public const string AllLocations = "all_loc";
}
