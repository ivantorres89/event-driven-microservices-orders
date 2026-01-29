using FluentAssertions;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.UnitTests;

public sealed class CorrelationIdProviderTests
{
    [Fact]
    public void GetCorrelationId_WhenCalled_ReturnsNonEmptyValue()
    {
        // Arrange
        var provider = new CorrelationIdProvider();

        // Act
        var result = provider.GetCorrelationId();

        // Assert
        result.Should().NotBe(new CorrelationId(Guid.Empty));
        result.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void GetCorrelationId_WhenCalledTwiceOnSameCorrelationContext_ReturnsSameValue()
    {
        // Arrange
        var provider = new CorrelationIdProvider();

        // Act
        var first = provider.GetCorrelationId();
        var second = provider.GetCorrelationId();

        // Assert
        first.Should().Be(second);
    }
}