namespace FreshOrbit.SharedKernel.Actors;

/// <summary>Who is acting. Stored as CUSTOMER / STAFF / SYSTEM / ANONYMOUS.</summary>
public enum ActorType
{
    /// <summary>A caller without a valid token (guest browsing, login).</summary>
    Anonymous,
    Customer,
    Staff,
    System,
}
