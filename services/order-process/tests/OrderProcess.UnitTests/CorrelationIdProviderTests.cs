using FluentAssertions;
using OrderProcess.Infrastructure.Correlation;
using OrderProcess.Shared.Correlation;
using CorrelationIdProvider = OrderProcess.Infrastructure.Services.CorrelationIdProvider;

namespace OrderProcess.UnitTests;

public sealed class CorrelationIdProviderTests
{
    [Fact]
    public void GetCorrelationId_WhenCorrelationContextIsSet_ReturnsSameValue()
    {
        // Arrange
        var expected = new CorrelationId(Guid.NewGuid());
        CorrelationContext.Current = expected;

        var provider = new CorrelationIdProvider();

        try
        {
            // Act
            var result = provider.GetCorrelationId();

            // Assert
            result.Should().Be(expected);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }

    [Fact]
    public void GetCorrelationId_WhenCorrelationContextIsNotSet_Throws()
    {
        // Arrange
        CorrelationContext.Current = null;
        var provider = new CorrelationIdProvider();

        // Act
        var act = () => provider.GetCorrelationId();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("CorrelationContext.Current is not set*");
    }
}
