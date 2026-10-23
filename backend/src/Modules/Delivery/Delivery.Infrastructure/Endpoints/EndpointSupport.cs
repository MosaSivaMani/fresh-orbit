using FreshOrbit.Delivery.Domain.Shared;
using FreshOrbit.Delivery.Infrastructure.Persistence;
using FreshOrbit.SharedKernel.EntityFramework;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreshOrbit.Delivery.Infrastructure.Endpoints;

/// <summary>
/// Turns <see cref="LocationAccessDeniedException"/> into 403 Problem Details with <c>code</c> (the shared exception
/// handler maps every DomainException to 422; a shared 403 exception is proposed for the SharedKernel).
/// </summary>
internal sealed class LocationAccessFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        try
        {
            return await next(context);
        }
        catch (LocationAccessDeniedException denied)
        {
            var problem = new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Forbidden.", Detail = denied.Message };
            problem.Extensions["code"] = denied.Code;
            problem.Extensions["errors"] = new Dictionary<string, object[]> { [denied.Field!] = [new { code = denied.Code, message = denied.Message }] };
            return TypedResults.Problem(problem);
        }
    }
}

internal static class SaveSupport
{
    private const int MaxAttempts = 3;
    private const string ExclusionViolation = "23P01";
    private const string UniqueViolation = "23505";

    /// <summary>
    /// DEL-R20: commands without If-Match retry from Read on a lost race (max 3, then 409 CONCURRENT_SAVE);
    /// DB backstops map to their codes (template EXCLUDE → SLOT_TEMPLATE_OVERLAP, rider phone → DUPLICATE).
    /// </summary>
    public static async Task<T> RetryAsync<T>(FreshOrbitDbContext db, Func<Task<T>> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await MapAsync(action);
            }
            catch (ConcurrentSaveException) when (attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Maps DB constraint backstops of a command to their API codes.</summary>
    public static async Task<T> MapAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return await action();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: ExclusionViolation, ConstraintName: DeliveryModuleModel.TemplateOverlapConstraint })
        {
            throw new ConflictException(DeliveryErrors.SlotTemplateOverlap, "The template overlaps another active template on the same day; change its times.", "startTime");
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation, TableName: "riders" })
        {
            throw new ConflictException(DeliveryErrors.Duplicate, "Another active rider has this phone number.", "phone");
        }
    }

    public static void Page(int page, int pageSize)
    {
        var errors = new List<FieldError>();
        if (page < 1)
        {
            errors.Add(new FieldError("page", DeliveryErrors.ValidationFailed, "Page must be 1 or more."));
        }

        if (pageSize is < 1 or > 100)
        {
            errors.Add(new FieldError("pageSize", DeliveryErrors.ValidationFailed, "Page size must be between 1 and 100."));
        }

        if (errors.Count > 0)
        {
            throw new RefusalException(errors);
        }
    }
}
