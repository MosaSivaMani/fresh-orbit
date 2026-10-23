using System.Net;
using AwesomeAssertions;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.Customers.Public;
using FreshOrbit.E1Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FreshOrbit.Customers.Tests.Integration;

/// <summary>Ordering does not implement <see cref="ICustomerDeletionCheck"/> yet, so a substitute stands in.</summary>
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
}
