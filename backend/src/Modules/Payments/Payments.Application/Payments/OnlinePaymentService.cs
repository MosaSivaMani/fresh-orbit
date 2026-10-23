using FreshOrbit.Ordering.Public;
using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Providers;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.SharedKernel.Actors;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using OrderingPaymentMethod = FreshOrbit.Ordering.Public.PaymentMethod;

namespace FreshOrbit.Payments.Application.Payments;

/// <summary>Customer-facing payment commands and reads (PAY-R5, docs/api §4.9).</summary>
public sealed class OnlinePaymentService(
    IOrderingApi ordering,
    IPaymentRepository payments,
    IPaymentProviderCatalog providers,
    IPaymentsSession session,
    ICurrentActor actor,
    IClock clock,
    PaymentsOptions options)
{
    /// <summary>
    /// Read: order facts and attempts. Decide: PAY-R5 (reuse a PENDING attempt still valid for the reuse margin).
    /// Provider call first (no transaction open), then Commit the PENDING attempt with <c>PaymentInitiated</c>.
    /// </summary>
    public async Task<InitiatePaymentResult> InitiateAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await GetOwnOrderAsync(orderId, cancellationToken);
        var attempts = await payments.ListByOrderAsync(orderId, cancellationToken);
        if (order.PaymentMethod != OrderingPaymentMethod.Online || attempts.Any(p => p.IsPaid))
        {
            throw new DomainException(PaymentsErrors.PaymentNotRequired, "This order is paid on delivery or already paid; no online payment is needed.");
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new ConflictException(PaymentsErrors.InvalidStatusTransition, "Only an order waiting for payment can be paid online.");
        }

        var now = clock.UtcNow;
        var amount = order.AmountDuePaise;
        var reusable = attempts
            .Where(p => p.Method == Domain.Payments.PaymentMethod.Online && p.Status == PaymentStatus.Pending && p.AmountPaise == amount)
            .Where(p => p.ExpiresAt is null || p.ExpiresAt > now.AddMinutes(options.ReuseMarginMinutes))
            .OrderByDescending(p => p.ExpiresAt)
            .FirstOrDefault();
        var provider = providers.GetOnlineProvider();
        if (reusable is not null)
        {
            return new InitiatePaymentResult(ToResponse(reusable, provider, null), Created: false);
        }

        var paymentId = SharedKernel.Domain.IdGenerator.NewId();
        var expiresAt = now.AddMinutes(options.OnlineAttemptMinutes);
        var providerOrder = await provider.CreateOrderAsync(
            new CreateProviderOrder(paymentId, order.OrderNumber, amount, PaymentProviders.Inr, null, expiresAt),
            cancellationToken);
        var payment = Payment.CreateOnlineAttempt(
            paymentId,
            order.OrderId,
            order.CustomerId,
            order.FulfillmentLocationId,
            provider.Key,
            amount,
            providerOrder.ProviderOrderId,
            providerOrder.ExpiresAt ?? expiresAt);
        payments.Add(payment);
        await session.SaveChangesAsync(cancellationToken);
        return new InitiatePaymentResult(ToResponse(payment, provider, providerOrder.KeyId), Created: true);
    }

    public async Task<IReadOnlyList<CustomerPaymentResponse>> ListPaymentsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await GetOwnOrderAsync(orderId, cancellationToken);
        var attempts = await payments.ListByOrderAsync(orderId, cancellationToken);
        return [.. attempts.Select(p => p.ToCustomerResponse())];
    }

    public async Task<IReadOnlyList<CustomerRefundResponse>> ListRefundsAsync(Guid orderId, CancellationToken cancellationToken)
    {
        await GetOwnOrderAsync(orderId, cancellationToken);
        var attempts = await payments.ListByOrderAsync(orderId, cancellationToken);
        return [.. attempts.SelectMany(p => p.Refunds).OrderBy(r => r.RequestedAt).Select(r => r.ToCustomerResponse())];
    }

    private async Task<OrderSummary> GetOwnOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await ordering.GetOrderSummaryAsync(orderId, cancellationToken);
        if (order is null || actor.CustomerId is null || order.CustomerId != actor.CustomerId)
        {
            throw new NotFoundException("ORDER_NOT_FOUND", "No order has this id.");
        }

        return order;
    }

    private static InitiatePaymentResponse ToResponse(Payment payment, IPaymentProvider provider, string? keyId)
        => new(payment.Id, payment.Provider, payment.ProviderOrderId!, payment.AmountPaise, keyId ?? ProviderKeyId(provider), payment.ExpiresAt);

    private static string? ProviderKeyId(IPaymentProvider provider) => provider is IHasPublicKeyId withKey ? withKey.PublicKeyId : null;
}

/// <summary>An adapter that exposes its public checkout key id (for reused attempts).</summary>
public interface IHasPublicKeyId
{
    string? PublicKeyId { get; }
}
