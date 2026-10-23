using FreshOrbit.Ordering.Domain.Shared;

namespace FreshOrbit.Ordering.Application.Shared;

/// <summary>
/// The caller may not use the endpoint: 401 <c>LOGIN_REQUIRED</c>, 403 <c>CUSTOMER_DELETED</c> or 403
/// <c>LOCATION_ACCESS_DENIED</c>. The shared kernel has no 401/403 exception, so Ordering's endpoint filter turns this
/// into a Problem Details response.
/// </summary>
public sealed class OrderingAccessException : Exception
{
    public OrderingAccessException()
        : this(403, OrderingErrors.LocationAccessDenied, "Access denied.")
    {
    }

    public OrderingAccessException(string message)
        : this(403, OrderingErrors.LocationAccessDenied, message)
    {
    }

    public OrderingAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = 403;
        Code = OrderingErrors.LocationAccessDenied;
    }

    public OrderingAccessException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    public string Code { get; }

    public static OrderingAccessException LoginRequired() => new(401, OrderingErrors.LoginRequired, "Sign in to continue.");

    public static OrderingAccessException CustomerDeleted() => new(403, OrderingErrors.CustomerDeleted, "This account is closed.");

    public static OrderingAccessException LocationDenied() => new(403, OrderingErrors.LocationAccessDenied, "You do not have access to this location's orders.");
}
