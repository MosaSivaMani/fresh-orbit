using System.Net;
using AwesomeAssertions;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Public;
using FreshOrbit.E1Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FreshOrbit.Customers.Tests.Integration;

/// <summary>A substitute <see cref="ICustomerDeletionCheck"/> stands in for Ordering and Payments (their checks have their own tests).</summary>
public sealed class AccountDeletionTests(E1Database database) : E1ApiTestBase(database)
{
    [Fact]
    public async Task Deletion_is_refused_while_another_module_reports_a_blocking_reason()
    {
        var check = Substitute.For<ICustomerDeletionCheck>();
        check.GetBlockingItemsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(["ACTIVE_ORDERS_EXIST"]);
        Factory.AdditionalServices = services => services.AddSingleton(check);

        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var me = await client.GetAsync("/api/v1/me", Ct);
        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me");
        delete.Headers.IfMatch.Add(me.Headers.ETag!);

        var response = await client.SendAsync(delete, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("INVALID_STATUS_TRANSITION");
        var customer = await QueryAsync(db => db.Set<Customer>().SingleAsync(c => c.UserId == session.UserId, Ct));
        customer.Status.Should().Be(FreshOrbit.Customers.Domain.Customers.CustomerStatus.Active);
        await check.Received(1).GetBlockingItemsAsync(customer.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Open_complaint_or_refund_refuses_deletion_with_the_kinds_until_they_close()
    {
        IReadOnlyList<string> blocking = [CustomerOpenMatters.OpenComplaint, CustomerOpenMatters.OpenRefund];
        var check = Substitute.For<ICustomerDeletionCheck>();
        check.GetBlockingItemsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(blocking));
        Factory.AdditionalServices = services => services.AddSingleton(check);
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);

        var refused = await DeleteAsync(client);

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadJsonAsync(refused);
        problem.GetProperty("code").GetString().Should().Be("ACCOUNT_HAS_OPEN_MATTERS");
        problem.GetProperty("errors").GetProperty("account").EnumerateArray().Select(e => e.GetProperty("code").GetString())
            .Should().BeEquivalentTo(["OPEN_COMPLAINT", "OPEN_REFUND"]);

        blocking = [CustomerOpenMatters.OpenRefund];
        var refundOnly = await ReadJsonAsync(await DeleteAsync(client));
        refundOnly.GetProperty("errors").GetProperty("account").EnumerateArray().Select(e => e.GetProperty("code").GetString()).Should().Equal("OPEN_REFUND");

        blocking = [];
        var deleted = await DeleteAsync(client);

        deleted.IsSuccessStatusCode.Should().BeTrue(await deleted.Content.ReadAsStringAsync(Ct));
        var customer = await QueryAsync(db => db.Set<Customer>().SingleAsync(c => c.UserId == session.UserId, Ct));
        customer.Status.Should().Be(FreshOrbit.Customers.Domain.Customers.CustomerStatus.Deleted);
    }

    [Fact]
    public async Task Active_orders_still_answer_409_even_with_an_open_matter()
    {
        var check = Substitute.For<ICustomerDeletionCheck>();
        check.GetBlockingItemsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(["ACTIVE_ORDERS_EXIST", CustomerOpenMatters.OpenComplaint]);
        Factory.AdditionalServices = services => services.AddSingleton(check);
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);

        var response = await DeleteAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("INVALID_STATUS_TRANSITION");
    }

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client)
    {
        var me = await client.GetAsync("/api/v1/me", Ct);
        using var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me");
        delete.Headers.IfMatch.Add(me.Headers.ETag!);
        return await client.SendAsync(delete, Ct);
    }
}
