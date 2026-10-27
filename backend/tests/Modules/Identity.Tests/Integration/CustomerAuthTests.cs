using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using FreshOrbit.Cart.Application.Carts;
using FreshOrbit.Cart.Domain.Carts;
using FreshOrbit.Customers.Domain.Customers;
using FreshOrbit.E1Testing;
using FreshOrbit.Identity.Domain.Tokens;
using FreshOrbit.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FreshOrbit.Identity.Tests.Integration;

public sealed class CustomerAuthTests(E1Database database) : E1ApiTestBase(database)
{
    [Fact]
    public async Task Otp_happy_path_creates_the_customer_and_signs_in()
    {
        var phone = E1ApiFactory.NewPhone();
        using var anonymous = Factory.CreateClient();
        var (challengeId, code) = await Factory.RequestOtpAsync(anonymous, phone, Ct);
        code.Should().HaveLength(6);

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("user").GetProperty("isNewCustomer").GetBoolean().Should().BeTrue();
        body.GetProperty("refreshToken").GetString().Should().StartWith("rt_");
        var userId = body.GetProperty("user").GetProperty("id").GetGuid();
        (await QueryAsync(db => db.Set<Customer>().AnyAsync(c => c.UserId == userId, Ct))).Should().BeTrue();
        (await QueryAsync(db => db.Set<User>().SingleAsync(u => u.Id == userId, Ct))).PhoneVerifiedAt.Should().NotBeNull();

        using var customer = Factory.CreateClient(body.GetProperty("accessToken").GetString()!);
        var me = await customer.GetAsync("/api/v1/me", Ct);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(me)).GetProperty("phone").GetString().Should().Be(phone);

        // Second sign-in (after the resend window) reuses the customer.
        Factory.Clock.Advance(TimeSpan.FromSeconds(31));
        var again = await Factory.SignInCustomerAsync(phone, Ct);
        again.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task Wrong_codes_lock_the_challenge_after_five_attempts()
    {
        var phone = E1ApiFactory.NewPhone();
        using var client = Factory.CreateClient();
        var (challengeId, code) = await Factory.RequestOtpAsync(client, phone, Ct);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 1; i <= 4; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code = wrong }, Ct);
            attempt.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await ErrorCodeAsync(attempt)).Should().Be("OTP_INVALID");
        }

        var fifth = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code = wrong }, Ct);
        fifth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ErrorCodeAsync(fifth)).Should().Be("OTP_TOO_MANY_ATTEMPTS");

        var right = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code }, Ct);
        right.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ErrorCodeAsync(right)).Should().Be("OTP_TOO_MANY_ATTEMPTS");
    }

    [Fact]
    public async Task Expired_code_is_refused()
    {
        var phone = E1ApiFactory.NewPhone();
        using var client = Factory.CreateClient();
        var (challengeId, code) = await Factory.RequestOtpAsync(client, phone, Ct);

        Factory.Clock.Advance(TimeSpan.FromMinutes(6));
        var response = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ErrorCodeAsync(response)).Should().Be("OTP_EXPIRED");
    }

    [Fact]
    public async Task Resend_within_the_window_is_refused_with_retry_after()
    {
        var phone = E1ApiFactory.NewPhone();
        using var client = Factory.CreateClient();
        await Factory.RequestOtpAsync(client, phone, Ct);

        var second = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { phone }, Ct);

        second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ErrorCodeAsync(second)).Should().Be("OTP_RESEND_TOO_SOON");
        second.Headers.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_the_family()
    {
        var session = await Factory.SignInCustomerAsync(null, Ct);
        using var client = Factory.CreateClient();

        var rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }, Ct);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var newRefresh = (await ReadJsonAsync(rotated)).GetProperty("refreshToken").GetString()!;
        newRefresh.Should().NotBe(session.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }, Ct);
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(reuse)).Should().Be("REFRESH_TOKEN_REUSED");

        // The legitimate successor is revoked too.
        var successor = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = newRefresh }, Ct);
        successor.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var tokens = await QueryAsync(db => db.Set<RefreshToken>().Where(t => t.UserId == session.UserId).ToListAsync(Ct));
        tokens.Should().HaveCount(2).And.OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task Unknown_refresh_token_is_unauthorized()
    {
        using var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "rt_not-a-real-token" }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ErrorCodeAsync(response)).Should().Be("REFRESH_TOKEN_INVALID");
    }

    [Fact]
    public async Task Otp_verify_returns_the_merged_guest_cart()
    {
        var token = GuestTokens.Create();
        var guest = ShoppingCart.ForGuest(GuestTokens.Hash(token), Factory.Clock.UtcNow);
        await QueryAsync(async db =>
        {
            db.Add(guest);
            return await db.SaveChangesAsync(Ct);
        });
        var phone = E1ApiFactory.NewPhone();
        using var anonymous = Factory.CreateClient();
        var (challengeId, code) = await Factory.RequestOtpAsync(anonymous, phone, Ct);

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code, guestCartToken = token }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = (await ReadJsonAsync(response)).GetProperty("cart");
        cart.ValueKind.Should().Be(JsonValueKind.Object);
        cart.GetProperty("mergedLineCount").GetInt32().Should().Be(0);
        cart.GetProperty("droppedLineCount").GetInt32().Should().Be(0);
        var cartId = cart.GetProperty("id").GetGuid();
        var carts = await QueryAsync(db => db.Set<ShoppingCart>().Where(c => c.Id == cartId || c.Id == guest.Id).ToListAsync(Ct));
        carts.Single(c => c.Id == cartId).Status.Should().Be(CartStatus.Active);
        carts.Single(c => c.Id == cartId).CustomerId.Should().NotBeNull();
        carts.Single(c => c.Id == guest.Id).Status.Should().Be(CartStatus.Converted);
    }

    [Fact]
    public async Task Otp_verify_without_a_known_guest_cart_returns_no_cart()
    {
        var phone = E1ApiFactory.NewPhone();
        using var anonymous = Factory.CreateClient();
        var (challengeId, code) = await Factory.RequestOtpAsync(anonymous, phone, Ct);

        var response = await anonymous.PostAsJsonAsync("/api/v1/auth/otp/verify", new { challengeId, phone, code, guestCartToken = GuestTokens.Create() }, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(response)).GetProperty("cart").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
