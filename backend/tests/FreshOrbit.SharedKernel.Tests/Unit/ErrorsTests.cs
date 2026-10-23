using AwesomeAssertions;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Web.Validation;

namespace FreshOrbit.SharedKernel.Tests.Unit;

public sealed class ErrorsTests
{
    [Theory]
    [InlineData("CONCURRENCY_CONFLICT", true)]
    [InlineData("A1", true)]
    [InlineData("lower", false)]
    [InlineData("DOUBLE__UNDERSCORE", false)]
    [InlineData("_LEADING", false)]
    [InlineData("1ST", false)]
    public void Codes_must_be_upper_snake(string code, bool valid) => ErrorCodes.IsUpperSnake(code).Should().Be(valid);

    [Fact]
    public void DomainException_rejects_invalid_codes()
    {
        var create = () => new DomainException("badCode", "Message.");
        create.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("NotEmptyValidator", "NOT_EMPTY")]
    [InlineData("GreaterThanOrEqualValidator", "GREATER_THAN_OR_EQUAL")]
    [InlineData("WIDGET_NAME_TOO_LONG", "WIDGET_NAME_TOO_LONG")]
    [InlineData(null, "VALIDATION_FAILED")]
    public void Validation_codes_are_upper_snake(string? errorCode, string expected) => ValidationErrors.ToCode(errorCode).Should().Be(expected);

    [Theory]
    [InlineData("Lines[0].Quantity", "lines[0].quantity")]
    [InlineData("Name", "name")]
    public void Validation_fields_are_camel_case_paths(string property, string expected) => ValidationErrors.ToFieldPath(property).Should().Be(expected);
}
