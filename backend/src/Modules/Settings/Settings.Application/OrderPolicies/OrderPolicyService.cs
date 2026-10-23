using System.Globalization;
using FreshOrbit.Settings.Application.Shared;
using FreshOrbit.Settings.Domain.OrderPolicies;
using FreshOrbit.Settings.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Persistence;

namespace FreshOrbit.Settings.Application.OrderPolicies;

public sealed record SaveOrderPolicyRequest(
    int ReservationMinutes,
    int MaxOverweightBp,
    string CancelAllowedUntilStatus,
    long CodMaxOrderPaise,
    IReadOnlyList<string> EnabledPaymentMethods,
    int SubstitutionResponseMinutes,
    int ComplaintWindowHours,
    int MaxDeliveryAttempts);

/// <param name="CityId">Null for the default policy.</param>
public sealed record OrderPolicyResponse(
    Guid Id,
    Guid? CityId,
    bool IsDefault,
    int ReservationMinutes,
    int MaxOverweightBp,
    string CancelAllowedUntilStatus,
    long CodMaxOrderPaise,
    IReadOnlyList<string> EnabledPaymentMethods,
    int SubstitutionResponseMinutes,
    int ComplaintWindowHours,
    int MaxDeliveryAttempts,
    string Version);

public sealed record OrderPolicyListResponse(IReadOnlyList<OrderPolicyResponse> Items);

public interface IOrderPolicyService
{
    Task<OrderPolicyListResponse> ListAsync(CancellationToken cancellationToken);

    /// <param name="cityId">Null saves the default policy.</param>
    Task<OrderPolicyResponse> SaveAsync(Guid? cityId, SaveOrderPolicyRequest request, WriteCondition condition, CancellationToken cancellationToken);
}

public sealed class OrderPolicyService(ISettingsRepository repository, IUnitOfWork unitOfWork) : IOrderPolicyService
{
    public async Task<OrderPolicyListResponse> ListAsync(CancellationToken cancellationToken)
    {
        var policies = await repository.ListOrderPoliciesAsync(cancellationToken);
        return new OrderPolicyListResponse([.. policies.OrderBy(p => p.CityId is not null).ThenBy(p => p.CityId).Select(ToResponse)]);
    }

    public async Task<OrderPolicyResponse> SaveAsync(Guid? cityId, SaveOrderPolicyRequest request, WriteCondition condition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(condition);
        var data = new OrderPolicyData(
            request.ReservationMinutes,
            request.MaxOverweightBp,
            request.CancelAllowedUntilStatus ?? string.Empty,
            request.CodMaxOrderPaise,
            request.EnabledPaymentMethods ?? [],
            request.SubstitutionResponseMinutes,
            request.ComplaintWindowHours,
            request.MaxDeliveryAttempts);
        var existing = await repository.GetOrderPolicyAsync(cityId, cancellationToken);
        OrderPolicy policy;
        if (condition.ShouldCreate(existing, unitOfWork, "order policy"))
        {
            policy = OrderPolicy.Create(cityId, data);
            repository.Add(policy);
        }
        else
        {
            policy = existing!;
            policy.Update(data);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(policy);
    }

    public static OrderPolicyResponse ToResponse(OrderPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new OrderPolicyResponse(
            policy.Id,
            policy.CityId,
            policy.CityId is null,
            policy.ReservationMinutes,
            policy.MaxOverweightBp,
            policy.CancelAllowedUntilStatus,
            policy.CodMaxOrderPaise,
            [.. policy.EnabledPaymentMethods],
            policy.SubstitutionResponseMinutes,
            policy.ComplaintWindowHours,
            policy.MaxDeliveryAttempts,
            policy.Version.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Refusal used when no policy applies.</summary>
    public static DomainException Missing() => new(SettingsErrors.ConfigurationMissing, "No default order policy is configured; an admin must save one in settings.");
}
