using FreshOrbit.Ordering.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;

namespace FreshOrbit.Ordering.Application.Shared;

/// <summary>
/// Access refusals of Ordering as the shared D31 exceptions: 401 <c>LOGIN_REQUIRED</c>, 403 <c>CUSTOMER_DELETED</c>,
/// 403 <c>LOCATION_ACCESS_DENIED</c>.
/// </summary>
public static class OrderingAccess
{
    public static UnauthorizedException LoginRequired() => new(OrderingErrors.LoginRequired, "Sign in to continue.");

    public static ForbiddenException CustomerDeleted() => new(OrderingErrors.CustomerDeleted, "This account is closed.");

    public static ForbiddenException LocationDenied() => new(OrderingErrors.LocationAccessDenied, "You do not have access to this location's orders.");
}
