using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Customers.Public;
using FreshOrbit.SharedKernel.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FreshOrbit.Payments.Tests.Integration;

/// <summary>G12 (payment created date) and the open-refund account deletion check.</summary>
public sealed class PaymentsGapsTests(PaymentsDatabase database) : PaymentsTestBase(database)
{
    private static readonly string[] RefundPermissions = ["payments.view", "refunds.request", "refunds.approve"];

    [Fact]
    public async Task G12_payment_responses_carry_the_created_date_of_the_attempt()
    {
        var locationId = Guid.CreateVersion7();
        var (order, paymentId) = await GivenCapturedOnlineOrderAsync(10000, locationId);
        var staff = Factory.CreateStaffClient(["payments.view"], [locationId]);
        DateTimeOffset stored;
        await using (var db = Database.CreateDbContext())
        {
            stored = await db.Set<FreshOrbit.Payments.Domain.Payments.Payment>().AsNoTracking()
                .Where(p => p.Id == paymentId).Select(p => EF.Property<DateTimeOffset>(p, AuditColumns.CreatedAt)).SingleAsync(Ct);
        }

        var detail = await ReadJsonAsync(await staff.GetAsync($"/api/v1/admin/payments/{paymentId}", Ct));
        var forOrder = await ReadJsonAsync(await staff.GetAsync($"/api/v1/admin/orders/{order.OrderId}/payments", Ct));

        stored.Should().BeAfter(DateTimeOffset.UnixEpoch);
        detail.GetProperty("createdAt").GetDateTimeOffset().Should().Be(stored);
        forOrder.GetProperty("payments")[0].GetProperty("createdAt").GetDateTimeOffset().Should().Be(stored);
    }

    [Fact]
    public async Task Open_refund_blocks_account_deletion_until_it_is_closed()
    {
        var locationId = Guid.CreateVersion7();
        var (order, _) = await GivenCapturedOnlineOrderAsync(10000, locationId);
        (await BlockingItemsAsync(order.CustomerId)).Should().BeEmpty();

        var requester = Factory.CreateStaffClient(RefundPermissions, [locationId]);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/orders/{order.OrderId}/refunds")
        {
            Content = JsonContent.Create(new { amountPaise = 2500, reasonCode = "GOODWILL", method = "ORIGINAL" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var created = await requester.SendAsync(request, Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var refundId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        (await BlockingItemsAsync(order.CustomerId)).Should().Equal(CustomerOpenMatters.OpenRefund);

        var approver = Factory.CreateStaffClient(RefundPermissions, [locationId]);
        using var reject = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/refunds/{refundId}/reject") { Content = JsonContent.Create(new { reason = "Duplicate request" }) };
        reject.Headers.TryAddWithoutValidation("If-Match", created.Headers.ETag!.Tag);
        (await approver.SendAsync(reject, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await BlockingItemsAsync(order.CustomerId)).Should().BeEmpty();
    }

    private async Task<List<string>> BlockingItemsAsync(Guid customerId)
        => await InScopeAsync(async sp =>
        {
            var items = new List<string>();
            foreach (var check in sp.GetServices<ICustomerDeletionCheck>())
            {
                items.AddRange(await check.GetBlockingItemsAsync(customerId, Ct));
            }

            return items;
        });
}
