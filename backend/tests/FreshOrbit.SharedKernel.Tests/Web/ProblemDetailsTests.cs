using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;

namespace FreshOrbit.SharedKernel.Tests.Web;

public sealed class ProblemDetailsTests(SharedKernelDatabase database) : ApiTestBase(database)
{
    [Theory]
    [InlineData("refusal", HttpStatusCode.UnprocessableEntity, "VALIDATION_FAILED")]
    [InlineData("domain", HttpStatusCode.UnprocessableEntity, "COUPON_EXPIRED")]
    [InlineData("not-found", HttpStatusCode.NotFound, "ORDER_NOT_FOUND")]
    [InlineData("conflict", HttpStatusCode.Conflict, "ORDER_ALREADY_PACKED")]
    [InlineData("stale", HttpStatusCode.PreconditionFailed, "CONCURRENCY_CONFLICT")]
    [InlineData("race", HttpStatusCode.Conflict, "CONCURRENT_SAVE")]
    [InlineData("unauthorized", HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "LOCATION_ACCESS_DENIED")]
    [InlineData("too-many", HttpStatusCode.TooManyRequests, "OTP_RESEND_TOO_SOON")]
    [InlineData("unavailable", HttpStatusCode.ServiceUnavailable, "PAYMENT_PROVIDER_UNAVAILABLE")]
    [InlineData("unexpected", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    public async Task Exceptions_map_to_problem_details_with_code_and_trace_id(string kind, HttpStatusCode status, string code)
    {
        var response = await Factory.CreateClient().GetAsync($"/api/v1/test/errors/{kind}", Ct);

        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var problem = await ReadJsonAsync(response);
        problem.GetProperty("status").GetInt32().Should().Be((int)status);
        problem.GetProperty("code").GetString().Should().Be(code);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refusal_lists_errors_by_field()
    {
        var problem = await ReadJsonAsync(await Factory.CreateClient().GetAsync("/api/v1/test/errors/refusal", Ct));

        var errors = problem.GetProperty("errors").GetProperty("lines[0].quantity");
        errors.EnumerateArray().Select(e => e.GetProperty("code").GetString())
            .Should().Equal("QUANTITY_BELOW_MINIMUM", "QUANTITY_NOT_A_STEP");
    }

    [Fact]
    public async Task Domain_exception_with_a_field_is_keyed_by_that_field()
    {
        var problem = await ReadJsonAsync(await Factory.CreateClient().GetAsync("/api/v1/test/errors/domain", Ct));

        problem.GetProperty("errors").GetProperty("couponCode")[0].GetProperty("code").GetString().Should().Be("COUPON_EXPIRED");
    }

    [Fact]
    public async Task Too_many_requests_carries_retry_after()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/test/errors/too-many", Ct);

        response.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(42));
        (await ReadJsonAsync(response)).GetProperty("retryAfterSeconds").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task Forbidden_with_a_field_is_keyed_by_that_field()
    {
        var problem = await ReadJsonAsync(await Factory.CreateClient().GetAsync("/api/v1/test/errors/forbidden", Ct));

        problem.GetProperty("errors").GetProperty("fulfillmentLocationId")[0].GetProperty("code").GetString().Should().Be("LOCATION_ACCESS_DENIED");
    }

    [Fact]
    public async Task Unexpected_errors_do_not_leak_details()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/test/errors/unexpected", Ct);

        (await response.Content.ReadAsStringAsync(Ct)).Should().NotContain("Boom");
    }

    [Fact]
    public async Task Unknown_route_is_404_problem()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/test/nothing-here", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Unreadable_body_is_400_malformed_request()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");
        var response = await Factory.CreateClient().PostAsync("/api/v1/test/widgets", content, Ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("code").GetString().Should().Be("MALFORMED_REQUEST");
    }

    [Fact]
    public async Task Validation_failure_is_422_with_field_keyed_codes()
    {
        var client = Factory.CreateClient();

        var empty = await client.PostAsJsonAsync("/api/v1/test/widgets", new { name = "" }, Ct);
        var tooLong = await client.PostAsJsonAsync("/api/v1/test/widgets", new { name = new string('x', 101) }, Ct);

        empty.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await ReadJsonAsync(empty);
        problem.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
        problem.GetProperty("errors").GetProperty("name")[0].GetProperty("code").GetString().Should().Be("NOT_EMPTY");
        (await ReadJsonAsync(tooLong)).GetProperty("errors").GetProperty("name")[0].GetProperty("code").GetString().Should().Be("WIDGET_NAME_TOO_LONG");
    }

    [Fact]
    public async Task Enums_are_serialized_as_upper_snake_codes()
    {
        var created = await ReadJsonAsync(await Factory.CreateClient().PostAsJsonAsync("/api/v1/test/widgets", new { name = "json" }, Ct));

        created.GetProperty("status").GetString().Should().Be("DRAFT");
    }
}
