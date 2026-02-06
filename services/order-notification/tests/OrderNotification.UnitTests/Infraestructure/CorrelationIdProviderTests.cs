using FluentAssertions;
using OrderNotification.Infrastructure.Services;
using OrderNotification.Shared.Correlation;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class CorrelationIdProviderTests
{
    [Fact]
    public void GetCorrelationId_WhenContextMissing_Throws()
    {
        CorrelationContext.Current = null;
        var provider = new CorrelationIdProvider();

        var act = () => provider.GetCorrelationId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetCorrelationId_WhenContextPresent_ReturnsValue()
    {
        var expected = CorrelationId.New();
        CorrelationContext.Current = expected;
        var provider = new CorrelationIdProvider();

        try
        {
            var actual = provider.GetCorrelationId();
            actual.Should().Be(expected);
        }
        finally
        {
            CorrelationContext.Current = null;
        }
    }
}
