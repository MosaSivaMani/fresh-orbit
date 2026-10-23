using FreshOrbit.Locations.Application.Common;
using FreshOrbit.Locations.Domain.Cities;
using FreshOrbit.Locations.Domain.Shared;
using FreshOrbit.Locations.Infrastructure.Persistence.Configurations;
using FreshOrbit.SharedKernel.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Locations.Infrastructure.Endpoints;

/// <summary>
/// Problem Details the shared exception handler cannot produce: CITY_NOT_READY with <c>missing</c>,
/// CITY_RETIREMENT_BLOCKED with <c>blocking</c>, 403 LOCATION_ACCESS_DENIED, and unique-index violations (the last
/// line behind the command's own checks) as 409.
/// </summary>
public sealed class LocationsProblemFilter : IEndpointFilter
{
    private const string UniqueViolation = "23505";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (CityNotReadyException exception)
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, "City cannot be activated.", exception.Code, exception.Message, new()
            {
                ["missing"] = exception.Missing.Select(m => new { m.Code, m.FulfillmentLocationId, m.Message }).ToList(),
                ["errors"] = new Dictionary<string, ProblemError[]>(),
            });
        }
        catch (CityRetirementBlockedException exception)
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, "City cannot be retired.", exception.Code, exception.Message, new()
            {
                ["blocking"] = exception.Blocking,
                ["errors"] = new Dictionary<string, ProblemError[]>(),
            });
        }
        catch (LocationAccessDeniedException exception)
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden.", exception.Code, exception.Message, []);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            var (code, field, message) = postgres.ConstraintName switch
            {
                PincodeConfiguration.ActivePincodeIndex => (LocationsErrors.PincodeAlreadyAssigned, "pincodes", "A pincode was assigned to another service area at the same time; reload and retry."),
                "ux_location_closures_location_date" => (LocationsErrors.Duplicate, "closedOn", "The location already has a closure on this date."),
                _ => (LocationsErrors.Duplicate, "code", "Another record already uses this code."),
            };
            return Problem(StatusCodes.Status409Conflict, "Conflict.", code, message, new()
            {
                ["errors"] = new Dictionary<string, ProblemError[]> { [field] = [new ProblemError(code, message)] },
            });
        }
    }

    internal static IResult Problem(int status, string title, string code, string detail, Dictionary<string, object?> extensions)
    {
        extensions["code"] = code;
        return TypedResults.Problem(detail: detail, statusCode: status, title: title, extensions: extensions);
    }
}
