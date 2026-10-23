using AwesomeAssertions;
using FreshOrbit.SharedKernel.Codes;

namespace FreshOrbit.SharedKernel.Tests.Unit;

public sealed class EnumCodesTests
{
    public enum Sample
    {
        Whole,
        CurryCut,
        PerKg,
        UpiOnDelivery,
        HTTPServer,
        Level2,
        [Code("FISH_STEAK")]
        Steak,
    }

    [Theory]
    [InlineData(Sample.Whole, "WHOLE")]
    [InlineData(Sample.CurryCut, "CURRY_CUT")]
    [InlineData(Sample.PerKg, "PER_KG")]
    [InlineData(Sample.UpiOnDelivery, "UPI_ON_DELIVERY")]
    [InlineData(Sample.HTTPServer, "HTTP_SERVER")]
    [InlineData(Sample.Level2, "LEVEL2")]
    [InlineData(Sample.Steak, "FISH_STEAK")]
    public void ToCode_converts_member_names_to_upper_snake(Sample value, string expected)
    {
        value.ToCode().Should().Be(expected);
        EnumCodes.ToCode((Enum)value).Should().Be(expected);
    }

    [Theory]
    [InlineData("CURRY_CUT", Sample.CurryCut)]
    [InlineData("FISH_STEAK", Sample.Steak)]
    public void ToEnum_parses_codes(string code, Sample expected) => code.ToEnum<Sample>().Should().Be(expected);

    [Theory]
    [InlineData("curry_cut")]
    [InlineData("CurryCut")]
    [InlineData("STEAK")]
    [InlineData("")]
    public void ToEnum_rejects_unknown_codes(string code)
    {
        var parse = () => code.ToEnum<Sample>();
        parse.Should().Throw<ArgumentException>();
        EnumCodes.IsCode<Sample>(code).Should().BeFalse();
    }

    [Fact]
    public void IsCode_accepts_every_listed_code()
    {
        EnumCodes.AllCodes<Sample>().Should().HaveCount(7).And.OnlyContain(c => EnumCodes.IsCode<Sample>(c));
        EnumCodes.IsCode<Sample>(null).Should().BeFalse();
    }

    [Fact]
    public void ToCode_rejects_undefined_values()
    {
        var convert = () => ((Sample)42).ToCode();
        convert.Should().Throw<ArgumentOutOfRangeException>();
    }

    public enum Duplicated
    {
        [Code("SAME")]
        First,
        [Code("SAME")]
        Second,
    }

    [Fact]
    public void Duplicate_codes_are_rejected()
    {
        var convert = () => Duplicated.First.ToCode();
        convert.Should().Throw<InvalidOperationException>();
    }
}
