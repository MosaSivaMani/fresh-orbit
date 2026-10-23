using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FreshOrbit.Customers.Domain.Addresses;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Customers.Tests.Integration;

public sealed class CustomerApiTests(E1Database database) : E1ApiTestBase(database)
{
    private static object AddressBody(string recipient, bool? isDefault = null) => new
    {
        label = "HOME",
        recipientName = recipient,
        phone = "+919876543210",
        line1 = "12 MG Road",
        locality = "Anna Nagar",
        cityName = "Chennai",
        stateCode = "TN",
        pincode = "600040",
        isDefault,
    };

    [Fact]
    public async Task Exactly_one_active_address_is_default()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);

        var first = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody("A"), Ct));
        first.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        var second = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody("B"), Ct));
        second.GetProperty("isDefault").GetBoolean().Should().BeFalse();
        var third = await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody("C", isDefault: true), Ct));
        third.GetProperty("isDefault").GetBoolean().Should().BeTrue();
        (await DefaultsAsync(session.UserId)).Should().Equal("C");

        var makeDefault = await client.PostAsync($"/api/v1/me/addresses/{second.GetProperty("addressId").GetGuid()}/default", null, Ct);
        makeDefault.StatusCode.Should().Be(HttpStatusCode.OK);
        (await DefaultsAsync(session.UserId)).Should().Equal("B");

        // Deleting the default promotes the newest remaining address (C).
        var delete = await client.DeleteAsync($"/api/v1/me/addresses/{second.GetProperty("addressId").GetGuid()}", Ct);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DefaultsAsync(session.UserId)).Should().Equal("C");

        var list = await ReadJsonAsync(await client.GetAsync("/api/v1/me/addresses", Ct));
        list.EnumerateArray().Select(a => a.GetProperty("recipientName").GetString()).Should().Equal("C", "A");
    }

    [Fact]
    public async Task Parallel_default_changes_never_leave_two_defaults()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            ids.Add((await ReadJsonAsync(await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody($"R{i}"), Ct))).GetProperty("addressId").GetGuid());
        }

        var responses = await Task.WhenAll(ids.Select(id => client.PostAsync($"/api/v1/me/addresses/{id}/default", null, Ct)));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.OK);
        (await DefaultsAsync(session.UserId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Twenty_first_address_is_refused()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        for (var i = 0; i < 20; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody($"R{i}"), Ct)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var refused = await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody("R20"), Ct);

        refused.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(refused)).Should().Be("ADDRESS_LIMIT_REACHED");
    }

    [Fact]
    public async Task Invalid_address_lists_field_errors()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/me/addresses", new { label = "HOME", recipientName = "A", phone = "123", line1 = "x", locality = "y", cityName = "z", stateCode = "TN", pincode = "12" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errors = (await ReadJsonAsync(response)).GetProperty("errors").EnumerateObject().Select(e => e.Name);
        errors.Should().Contain(["phone", "pincode"]);
    }

    [Fact]
    public async Task Profile_update_needs_if_match_and_delete_blocks_further_use()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        await client.PostAsJsonAsync("/api/v1/me/addresses", AddressBody("Home"), Ct);
        await client.PutAsync($"/api/v1/me/wishlist/{Guid.CreateVersion7()}", null, Ct);

        (await client.PatchAsJsonAsync("/api/v1/me", new { fullName = "Asha" }, Ct)).StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);

        var me = await client.GetAsync("/api/v1/me", Ct);
        var patch = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/me") { Content = JsonContent.Create(new { fullName = "Asha", email = "asha@example.in" }) };
        patch.Headers.IfMatch.Add(me.Headers.ETag!);
        var patched = await client.SendAsync(patch, Ct);
        patched.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(patched)).GetProperty("fullName").GetString().Should().Be("Asha");

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/me");
        delete.Headers.IfMatch.Add(patched.Headers.ETag!);
        (await client.SendAsync(delete, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetAsync("/api/v1/me", Ct);
        after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(after)).Should().Be("CUSTOMER_DELETED");

        var customer = await QueryAsync(db => db.Set<Customer>().SingleAsync(c => c.UserId == session.UserId, Ct));
        customer.Status.Should().Be(CustomerStatus.Deleted);
        customer.FullName.Should().BeNull();
        var user = await QueryAsync(db => db.Set<User>().SingleAsync(u => u.Id == session.UserId, Ct));
        user.Phone.Should().BeNull();
        user.Status.Should().Be(UserStatus.Disabled);
        (await QueryAsync(db => db.Set<Address>().Where(a => a.CustomerId == customer.Id).ToListAsync(Ct))).Should().OnlyContain(a => !a.IsActive && a.RecipientName == "Deleted");

        // The refresh token no longer works.
        using var anonymous = Factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wishlist_add_is_idempotent_and_remove_of_missing_item_succeeds()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient(session.AccessToken);
        var productId = Guid.CreateVersion7();

        (await client.PutAsync($"/api/v1/me/wishlist/{productId}", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsync($"/api/v1/me/wishlist/{productId}", null, Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await ReadJsonAsync(await client.GetAsync("/api/v1/me/wishlist", Ct));
        list.GetArrayLength().Should().Be(1);

        (await client.DeleteAsync($"/api/v1/me/wishlist/{productId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/v1/me/wishlist/{productId}", Ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Admin_block_disables_the_user_and_needs_customers_manage()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        var customerId = await QueryAsync(db => db.Set<Customer>().Where(c => c.UserId == session.UserId).Select(c => c.Id).SingleAsync(Ct));

        using var viewer = Factory.CreateStaffClient(["customers.view"]);
        var detail = await viewer.GetAsync($"/api/v1/admin/customers/{customerId}", Ct);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(detail)).GetProperty("phone").GetString().Should().Be(session.Phone);
        var search = await ReadJsonAsync(await viewer.GetAsync($"/api/v1/admin/customers?q={Uri.EscapeDataString(session.Phone)}", Ct));
        search.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("customerId").GetGuid()).Should().Equal(customerId);

        var forbidden = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/customers/{customerId}/block");
        forbidden.Headers.IfMatch.Add(detail.Headers.ETag!);
        (await viewer.SendAsync(forbidden, Ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var manager = Factory.CreateStaffClient(["customers.view", "customers.manage"]);
        var block = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/customers/{customerId}/block");
        block.Headers.IfMatch.Add(detail.Headers.ETag!);
        var blocked = await manager.SendAsync(block, Ct);
        blocked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(blocked)).GetProperty("isBlocked").GetBoolean().Should().BeTrue();
        (await QueryAsync(db => db.Set<User>().SingleAsync(u => u.Id == session.UserId, Ct))).Status.Should().Be(UserStatus.Disabled);

        using var anonymous = Factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }, Ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var unblock = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/customers/{customerId}/unblock");
        unblock.Headers.IfMatch.Add(blocked.Headers.ETag!);
        (await manager.SendAsync(unblock, Ct)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await QueryAsync(db => db.Set<User>().SingleAsync(u => u.Id == session.UserId, Ct))).Status.Should().Be(UserStatus.Active);
    }

    private async Task<List<string>> DefaultsAsync(Guid userId)
        => await QueryAsync(async db =>
        {
            var customerId = await db.Set<Customer>().Where(c => c.UserId == userId).Select(c => c.Id).SingleAsync(Ct);
            return await db.Set<Address>().Where(a => a.CustomerId == customerId && a.IsActive && a.IsDefault).Select(a => a.RecipientName).ToListAsync(Ct);
        });
}

internal static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string uri, T value, CancellationToken ct)
        => client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, uri) { Content = JsonContent.Create(value) }, ct);
}
