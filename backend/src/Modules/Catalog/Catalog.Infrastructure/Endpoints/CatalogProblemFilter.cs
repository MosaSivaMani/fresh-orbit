using FreshOrbit.Catalog.Domain.Shared;
using FreshOrbit.Catalog.Infrastructure.Persistence.Configurations;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Catalog.Infrastructure.Endpoints;

/// <summary>
/// Unique-index violations (the last line behind the commands' own checks: two writers racing for one slug or SKU)
/// become 409 SLUG_TAKEN / SKU_TAKEN; a deferred-constraint race becomes 409 CONCURRENT_SAVE.
/// </summary>
public sealed class CatalogProblemFilter : IEndpointFilter
{
    private const string UniqueViolation = "23505";
    private const string ExclusionViolation = "23P01";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation } postgres)
        {
            var (code, field, message) = postgres.ConstraintName switch
            {
                ProductVariantConfiguration.SkuIndex => (CatalogErrors.SkuTaken, "sku", "Another variant already uses this SKU."),
                ProductConfiguration.SlugIndex or CategoryConfiguration.SlugIndex => (CatalogErrors.SlugTaken, "slug", "Another record already uses this slug."),
                _ => (ConcurrentSaveException.DefaultCode, "id", "Another change was saved at the same time; retry the request."),
            };
            return Problem(StatusCodes.Status409Conflict, code, field, message);
        }
        catch (Exception exception) when (HasSqlState(exception, ExclusionViolation))
        {
            return Problem(StatusCodes.Status409Conflict, ConcurrentSaveException.DefaultCode, "preparations", "Another change was saved at the same time; retry the request.");
        }
    }

    private static bool HasSqlState(Exception exception, string sqlState)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres && postgres.SqlState == sqlState)
            {
                return true;
            }
        }

        return false;
    }

    private static ProblemHttpResult Problem(int status, string code, string field, string message)
        => TypedResults.Problem(detail: message, statusCode: status, title: "Conflict.", extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["errors"] = new Dictionary<string, ProblemError[]> { [field] = [new ProblemError(code, message)] },
        });
}
