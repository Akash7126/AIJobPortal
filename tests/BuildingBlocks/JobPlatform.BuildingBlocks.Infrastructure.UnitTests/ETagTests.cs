using JobPlatform.SharedKernel.Application.Concurrency;
using JobPlatform.SharedKernel.Application.Results;

namespace JobPlatform.BuildingBlocks.Infrastructure.UnitTests;

public class ETagTests
{
    private static readonly byte[] Version = { 1, 2, 3, 4, 5, 6, 7, 8 };

    [Fact]
    public void From_IsAQuotedBase64OfTheRowVersion() => ETag.From(Version).Should().Be("\"AQIDBAUGBwg=\"");

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("*", true)]
    [InlineData("\"AQIDBAUGBwg=\"", true)]
    [InlineData("\"other\", \"AQIDBAUGBwg=\"", true)]
    [InlineData("\"other\"", false)]
    [InlineData("AQIDBAUGBwg=", false)]
    [InlineData("W/\"AQIDBAUGBwg=\"", false)]
    public void Matches_ImplementsStrongIfMatchSemantics(string? ifMatch, bool expected) =>
        ETag.Matches(ifMatch, Version).Should().Be(expected);

    [Fact]
    public void PreconditionFailed_IsAnErrorOfItsOwnType()
    {
        var error = Error.PreconditionFailed("E-X", "changed");

        error.Type.Should().Be(ErrorType.PreconditionFailed);
        error.Code.Should().Be("E-X");
    }
}
