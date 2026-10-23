using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.SharedKernel.Codes;
using FreshOrbit.Payments.Application.Payments;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Payments.Infrastructure.Public;

/// <summary><see cref="IPaymentsApi"/> over the payments tables (no tracking: callers share the DbContext).</summary>
internal sealed class PaymentsApi(FreshOrbitDbContext db, IPaymentProviderCatalog providers) : IPaymentsApi
{
    public Task<IReadOnlyList<string>> GetAvailablePaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        List<string> methods = [PaymentMethod.Cod.ToCode(), PaymentMethod.UpiOnDelivery.ToCode()];
        try
        {
            providers.GetOnlineProvider();
            methods.Add(PaymentMethod.Online.ToCode());
        }
        catch (PaymentProviderUnavailableException)
        {
            // No online provider configured or it is disabled: ONLINE is not offered.
        }

        return Task.FromResult<IReadOnlyList<string>>(methods);
    }

    public async Task<OrderPaymentStatus> GetPaymentStatusAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PaymentSummaries.BuildStatus(orderId, await LoadAsync([orderId], cancellationToken));

    public async Task<OrderPaymentSummary> GetOrderPaymentSummaryAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PaymentSummaries.Build(orderId, await LoadAsync([orderId], cancellationToken));

    public async Task<IReadOnlyDictionary<Guid, OrderPaymentSummary>> GetOrderPaymentSummariesAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        var attempts = await LoadAsync(orderIds, cancellationToken);
        return orderIds.Distinct().ToDictionary(id => id, id => PaymentSummaries.Build(id, attempts));
    }

    private async Task<List<Payment>> LoadAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
        => await db.Set<Payment>().AsNoTracking().Include(p => p.Refunds).Where(p => orderIds.Contains(p.OrderId)).ToListAsync(cancellationToken);
}
