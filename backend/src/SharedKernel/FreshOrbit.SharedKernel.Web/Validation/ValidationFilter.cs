using FluentValidation;
using FluentValidation.Results;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.SharedKernel.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.SharedKernel.Web.Validation;

/// <summary>
/// Validates the <typeparamref name="TRequest"/> argument with its registered <see cref="IValidator{T}"/> and
/// refuses with 422 and field-keyed errors (camelCase paths, UPPER_SNAKE codes). A rule's
/// <c>WithErrorCode("…")</c> wins; FluentValidation's built-in codes are converted
/// (<c>NotEmptyValidator → NOT_EMPTY</c>).
/// </summary>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        var validator = context.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
        if (request is not null && validator is not null)
        {
            var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new RefusalException([.. result.Errors.Select(ValidationErrors.ToFieldError)]);
            }
        }

        return await next(context);
    }
}

/// <summary>Converts FluentValidation failures to field errors.</summary>
public static class ValidationErrors
{
    public static FieldError ToFieldError(ValidationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new FieldError(ToFieldPath(failure.PropertyName), ToCode(failure.ErrorCode), failure.ErrorMessage);
    }

    public static string ToFieldPath(string propertyName)
        => string.IsNullOrEmpty(propertyName)
            ? string.Empty
            : string.Join('.', propertyName.Split('.').Select(s => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..]));

    public static string ToCode(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
        {
            return RefusalException.DefaultCode;
        }

        if (ErrorCodes.IsUpperSnake(errorCode))
        {
            return errorCode;
        }

        var name = errorCode.EndsWith("Validator", StringComparison.Ordinal) ? errorCode[..^"Validator".Length] : errorCode;
        var code = EnumCodes.ToUpperSnake(name);
        return ErrorCodes.IsUpperSnake(code) ? code : RefusalException.DefaultCode;
    }
}

public static class ValidationEndpointExtensions
{
    /// <summary>Validates the <typeparamref name="TRequest"/> argument before the handler runs.</summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
}
