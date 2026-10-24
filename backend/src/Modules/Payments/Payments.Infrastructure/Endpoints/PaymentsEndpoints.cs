using FreshOrbit.Payments.Application.Common;
using FreshOrbit.Payments.Application.Payments;
using FreshOrbit.Payments.Application.Refunds;
using FreshOrbit.Payments.Application.Settlements;
using FreshOrbit.Payments.Application.Webhooks;
using FreshOrbit.Payments.Domain.Payments;
using FreshOrbit.Payments.Domain.Reconciliation;
using FreshOrbit.Payments.Domain.Settlements;
using FreshOrbit.Payments.Domain.Shared;
using FreshOrbit.Payments.Domain.Webhooks;
using FreshOrbit.Payments.Infrastructure.Providers;
using FreshOrbit.Payments.Infrastructure.Reads;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Time;
using FreshOrbit.SharedKernel.Web.Auth;
using FreshOrbit.SharedKernel.Web.Concurrency;
using FreshOrbit.SharedKernel.Web.Idempotency;
using FreshOrbit.SharedKernel.Web.Paging;
using FreshOrbit.SharedKernel.Web.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace FreshOrbit.Payments.Infrastructure.Endpoints;

/// <summary>Permission codes used by Payments endpoints (docs/api §3; <c>payments.manage</c> from decision D15).</summary>
public static class PaymentsPermissions
{
    public const string PaymentsView = "payments.view";
    public const string PaymentsManage = "payments.manage";
    public const string RefundsRequest = "refunds.request";
    public const string RefundsApprove = "refunds.approve";
    public const string CashSettle = "cash.settle";
    public const string DeliveryManage = "delivery.manage";
}

internal static class PaymentsEndpoints
{
    public static void MapCustomer(IEndpointRouteBuilder endpoints)
    {
        var orders = endpoints.MapGroup("/orders/{orderId:guid}").RequireAuthorization();

        orders.MapPost("/payments", async (Guid orderId, OnlinePaymentService service, CancellationToken ct) =>
            {
                var result = await service.InitiateAsync(orderId, ct);
                return result.Created
                    ? Results.Created($"/api/v1/orders/{orderId}/payments", result.Payment)
                    : Results.Ok(result.Payment);
            })
            .WithIdempotency(required: true)
            .RequireRateLimiting(RateLimitPolicies.Checkout);

        orders.MapGet("/payments", async (Guid orderId, OnlinePaymentService service, CancellationToken ct)
            => TypedResults.Ok(await service.ListPaymentsAsync(orderId, ct)));

        orders.MapGet("/refunds", async (Guid orderId, OnlinePaymentService service, CancellationToken ct)
            => TypedResults.Ok(await service.ListRefundsAsync(orderId, ct)));

        endpoints.MapPost("/payments/webhooks/{provider}", async (string provider, HttpRequest request, WebhookService service, CancellationToken ct) =>
            {
                var body = await ReadWebhookBodyAsync(request, ct);
                var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
                return TypedResults.Ok(await service.ReceiveAsync(provider, headers, body, ct));
            })
            .WithMetadata(new RequestSizeLimitAttribute(WebhookEvent.MaxBodyBytes))
            .AllowAnonymous();
    }

    /// <summary>
    /// SEC-6: at most <see cref="WebhookEvent.MaxBodyBytes"/>, else 413 <c>PAYLOAD_TOO_LARGE</c> before anything is stored.
    /// Checked here as well as by the Kestrel limit above, because a chunked body has no Content-Length and test hosts
    /// do not enforce the server limit.
    /// </summary>
    private static async Task<byte[]> ReadWebhookBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength > WebhookEvent.MaxBodyBytes)
        {
            throw TooLarge();
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > WebhookEvent.MaxBodyBytes)
            {
                throw TooLarge();
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();

        static BadHttpRequestException TooLarge()
            => new($"The webhook body must be at most {WebhookEvent.MaxBodyBytes} bytes.", StatusCodes.Status413PayloadTooLarge);
    }

    /// <summary>G9: Development/Testing only — makes the Fake gateway send a signed webhook for an order.</summary>
    public static void MapDevelopment(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/dev/payments/fake/{providerOrderId}/simulate", async (
                string providerOrderId,
                SimulateFakePaymentRequest request,
                FakeGateway gateway,
                IOptionsMonitor<PaymentProvidersOptions> options,
                WebhookService service,
                IClock clock,
                CancellationToken ct) =>
            {
                var order = gateway.FindOrder(providerOrderId) ?? throw new NotFoundException("PROVIDER_ORDER_NOT_FOUND", "The Fake gateway has no order with this id.");
                var secret = options.CurrentValue.For(PaymentProviders.Fake)?.WebhookSecret ?? string.Empty;
                var (type, amount) = request.Outcome switch
                {
                    "CAPTURED" => ("payment.captured", request.AmountPaise ?? order.AmountPaise),
                    "AUTHORIZED" => ("payment.authorized", request.AmountPaise ?? order.AmountPaise),
                    "FAILED" => ("payment.failed", request.AmountPaise ?? order.AmountPaise),
                    "AMOUNT_MISMATCH" => ("payment.captured", request.AmountPaise ?? (order.AmountPaise + 100)),
                    _ => throw new RefusalException(new FieldError("outcome", PaymentsErrors.ValidationFailed, "Outcome must be CAPTURED, AUTHORIZED, FAILED or AMOUNT_MISMATCH.")),
                };
                var providerPaymentId = type == "payment.captured" ? gateway.Capture(providerOrderId, amount) : gateway.NextId("fake_pay");
                var body = FakePaymentProvider.BuildBody(gateway.NextEventId(), type, clock.UtcNow, new
                {
                    providerOrderId,
                    providerPaymentId,
                    amountPaise = amount,
                    currency = PaymentProviders.Inr,
                    method = "UPI",
                    failureCode = type == "payment.failed" ? "PAYMENT_DECLINED" : null,
                });
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [FakePaymentProvider.SignatureHeader] = FakePaymentProvider.Sign(secret, body) };
                return TypedResults.Ok(await service.ReceiveAsync(PaymentProviders.Fake, headers, body, ct));
            })
            .AllowAnonymous();
    }

    public static void MapAdmin(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin");

        // Payments
        admin.MapGet("/payments", async (
                PaymentsReads reads,
                [FromQuery] string[]? status,
                [FromQuery] string[]? method,
                string? provider,
                DateOnly? fromDate,
                DateOnly? toDate,
                Guid? fulfillmentLocationId,
                CancellationToken ct,
                int page = 1,
                int pageSize = PagedRequest.DefaultPageSize) =>
            {
                var list = new ListParameters(page, pageSize, fromDate, toDate, fulfillmentLocationId);
                list.Validate();
                return TypedResults.Ok(await reads.ListPaymentsAsync(list, QueryCodes.Parse<PaymentStatus>(status, "status"), QueryCodes.Parse<PaymentMethod>(method, "method"), provider, ct));
            })
            .RequirePermission(PaymentsPermissions.PaymentsView);

        admin.MapGet("/payments/{paymentId:guid}", async (Guid paymentId, PaymentsReads reads, HttpResponse response, CancellationToken ct) =>
            {
                var payment = await reads.GetPaymentAsync(paymentId, ct);
                SetETag(response, payment.Version);
                return TypedResults.Ok(payment);
            })
            .RequirePermission(PaymentsPermissions.PaymentsView);

        admin.MapGet("/orders/{orderId:guid}/payments", async (Guid orderId, PaymentsReads reads, CancellationToken ct)
                => TypedResults.Ok(await reads.GetOrderPaymentsAsync(orderId, ct)))
            .RequirePermission(PaymentsPermissions.PaymentsView);

        // Webhook events
        admin.MapGet("/webhook-events", async (
                PaymentsReads reads,
                string? provider,
                bool? signatureValid,
                bool? processed,
                [FromQuery] string[]? status,
                CancellationToken ct,
                int page = 1,
                int pageSize = PagedRequest.DefaultPageSize) =>
            {
                var list = new ListParameters(page, pageSize, null, null, null);
                list.Validate();
                return TypedResults.Ok(await reads.ListWebhookEventsAsync(list, provider, signatureValid, processed, QueryCodes.Parse<WebhookEventStatus>(status, "status"), ct));
            })
            .RequirePermission(PaymentsPermissions.PaymentsView);

        admin.MapPost("/webhook-events/{eventId:guid}/reprocess", async (Guid eventId, WebhookService service, CancellationToken ct)
                => TypedResults.Ok(await service.ReprocessAsync(eventId, ct)))
            .RequirePermission(PaymentsPermissions.PaymentsManage);

        // Refunds
        admin.MapPost("/orders/{orderId:guid}/refunds", async (Guid orderId, CreateRefundRequest request, RefundService service, HttpResponse response, CancellationToken ct) =>
            {
                var refund = await service.RequestAsync(orderId, request, ct);
                SetETag(response, refund.Version);
                return TypedResults.Created($"/api/v1/admin/refunds/{refund.Id}", refund);
            })
            .WithIdempotency(required: true)
            .RequirePermission(PaymentsPermissions.RefundsRequest);

        admin.MapGet("/refunds", async (
                PaymentsReads reads,
                [FromQuery] string[]? status,
                [FromQuery] string[]? reasonCode,
                DateOnly? fromDate,
                DateOnly? toDate,
                Guid? fulfillmentLocationId,
                CancellationToken ct,
                int page = 1,
                int pageSize = PagedRequest.DefaultPageSize) =>
            {
                var list = new ListParameters(page, pageSize, fromDate, toDate, fulfillmentLocationId);
                list.Validate();
                return TypedResults.Ok(await reads.ListRefundsAsync(list, QueryCodes.Parse<RefundStatus>(status, "status"), QueryCodes.Parse<RefundReason>(reasonCode, "reasonCode"), ct));
            })
            .RequirePermission(PaymentsPermissions.PaymentsView);

        admin.MapGet("/refunds/{refundId:guid}", async (Guid refundId, RefundService service, HttpResponse response, CancellationToken ct) =>
            {
                var refund = await service.GetAsync(refundId, ct);
                SetETag(response, refund.Version);
                return TypedResults.Ok(refund);
            })
            .RequirePermission(PaymentsPermissions.PaymentsView);

        admin.MapPost("/refunds/{refundId:guid}/approve", async (Guid refundId, HttpRequest http, RefundService service, CancellationToken ct) =>
                WithETag(http, await service.ApproveAsync(refundId, IfMatch.GetRequiredVersion(http), ct)))
            .RequirePermission(PaymentsPermissions.RefundsApprove);

        admin.MapPost("/refunds/{refundId:guid}/reject", async (Guid refundId, RejectRefundRequest request, HttpRequest http, RefundService service, CancellationToken ct) =>
                WithETag(http, await service.RejectAsync(refundId, IfMatch.GetRequiredVersion(http), request, ct)))
            .RequirePermission(PaymentsPermissions.RefundsApprove);

        admin.MapPost("/refunds/{refundId:guid}/mark-paid", async (Guid refundId, MarkRefundPaidRequest request, HttpRequest http, RefundService service, CancellationToken ct) =>
                WithETag(http, await service.MarkPaidAsync(refundId, IfMatch.GetRequiredVersion(http), request, ct)))
            .RequirePermission(PaymentsPermissions.RefundsApprove);

        // Cash collected (decision D14: Payments owns this Delivery-looking route)
        admin.MapPost("/delivery/shipments/{shipmentId:guid}/cash-collected", async (Guid shipmentId, CashCollectedRequest request, CashCollectionService service, HttpResponse response, CancellationToken ct) =>
            {
                var payment = await service.RecordAsync(shipmentId, request, ct);
                SetETag(response, payment.Version);
                return TypedResults.Ok(payment);
            })
            .WithIdempotency(required: true)
            .RequirePermission(PaymentsPermissions.DeliveryManage);

        // Cash settlements
        admin.MapGet("/cash-settlements", async (
                PaymentsReads reads,
                Guid? riderId,
                DateOnly? date,
                [FromQuery] string[]? status,
                DateOnly? fromDate,
                DateOnly? toDate,
                Guid? fulfillmentLocationId,
                CancellationToken ct,
                int page = 1,
                int pageSize = PagedRequest.DefaultPageSize) =>
            {
                var list = new ListParameters(page, pageSize, fromDate, toDate, fulfillmentLocationId);
                list.Validate();
                return TypedResults.Ok(await reads.ListSettlementsAsync(list, riderId, date, QueryCodes.Parse<CashSettlementStatus>(status, "status"), ct));
            })
            .RequirePermission(PaymentsPermissions.CashSettle);

        admin.MapGet("/cash-settlements/{settlementId:guid}", async (Guid settlementId, CashSettlementService service, HttpResponse response, CancellationToken ct) =>
            {
                var settlement = await service.GetAsync(settlementId, ct);
                SetETag(response, settlement.Version);
                return TypedResults.Ok(settlement);
            })
            .RequirePermission(PaymentsPermissions.CashSettle);

        admin.MapPost("/cash-settlements", async (CreateCashSettlementRequest request, CashSettlementService service, HttpResponse response, CancellationToken ct) =>
            {
                var settlement = await service.CreateAsync(request, ct);
                SetETag(response, settlement.Version);
                return TypedResults.Created($"/api/v1/admin/cash-settlements/{settlement.Id}", settlement);
            })
            .WithIdempotency(required: true)
            .RequirePermission(PaymentsPermissions.CashSettle);

        admin.MapPost("/cash-settlements/{settlementId:guid}/dispute", async (Guid settlementId, DisputeCashSettlementRequest request, HttpRequest http, CashSettlementService service, CancellationToken ct) =>
                WithETag(http, await service.DisputeAsync(settlementId, IfMatch.GetRequiredVersion(http), request, ct)))
            .RequirePermission(PaymentsPermissions.CashSettle);

        admin.MapPost("/cash-settlements/{settlementId:guid}/settle", async (Guid settlementId, SettleCashSettlementRequest request, HttpRequest http, CashSettlementService service, CancellationToken ct) =>
                WithETag(http, await service.SettleAsync(settlementId, IfMatch.GetRequiredVersion(http), request, ct)))
            .RequirePermission(PaymentsPermissions.CashSettle);

        // Reconciliation issues (G3)
        admin.MapGet("/payment-issues", async (
                PaymentsReads reads,
                [FromQuery] string[]? kind,
                [FromQuery] string[]? status,
                Guid? fulfillmentLocationId,
                CancellationToken ct,
                int page = 1,
                int pageSize = PagedRequest.DefaultPageSize) =>
            {
                var list = new ListParameters(page, pageSize, null, null, fulfillmentLocationId);
                list.Validate();
                return TypedResults.Ok(await reads.ListIssuesAsync(list, QueryCodes.Parse<IssueKind>(kind, "kind"), QueryCodes.Parse<IssueStatus>(status, "status"), ct));
            })
            .RequirePermission(PaymentsPermissions.PaymentsView);

        admin.MapPost("/payment-issues/{issueId:guid}/resolve", async (Guid issueId, ResolveIssueRequest request, HttpRequest http, PaymentIssueService service, CancellationToken ct) =>
                WithETag(http, await service.ResolveAsync(issueId, IfMatch.GetRequiredVersion(http), request, ct)))
            .RequirePermission(PaymentsPermissions.PaymentsManage);
    }

    private static void SetETag(HttpResponse response, string version) => response.SetETag(uint.Parse(version, System.Globalization.CultureInfo.InvariantCulture));

    private static Microsoft.AspNetCore.Http.HttpResults.Ok<T> WithETag<T>(HttpRequest http, T body)
    {
        var version = body switch
        {
            RefundResponse refund => refund.Version,
            CashSettlementResponse settlement => settlement.Version,
            PaymentIssueResponse issue => issue.Version,
            _ => null,
        };
        if (version is not null)
        {
            SetETag(http.HttpContext.Response, version);
        }

        return TypedResults.Ok(body);
    }
}
