using System.Diagnostics;
using FluentAssertions;
using OrderAccept.Api.Observability;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.UnitTests.API.Observability;

public sealed class CorrelationIdActivityProcessorTests
{
    [Fact]
    public void OnStart_WhenCorrelationContextExists_SetsTag()
    {
        var original = CorrelationContext.Current;
        try
        {
            var correlation = new CorrelationId(Guid.NewGuid());
            CorrelationContext.Current = correlation;

            var activity = new Activity("test");
            activity.Start();

            var processor = new CorrelationIdActivityProcessor();
            processor.OnStart(activity);

            activity.GetTagItem("correlation_id").Should().Be(correlation.ToString());
            activity.Stop();
        }
        finally
        {
            CorrelationContext.Current = original;
        }
    }

    [Fact]
    public void OnStart_WhenTagAlreadyExists_DoesNotOverwrite()
    {
        var original = CorrelationContext.Current;
        try
        {
            CorrelationContext.Current = new CorrelationId(Guid.NewGuid());

            var activity = new Activity("test");
            activity.Start();
            activity.SetTag("correlation_id", "existing");

            var processor = new CorrelationIdActivityProcessor();
            processor.OnStart(activity);

            activity.GetTagItem("correlation_id").Should().Be("existing");
            activity.Stop();
        }
        finally
        {
            CorrelationContext.Current = original;
        }
    }

    [Fact]
    public void OnStart_WhenNoContext_UsesBaggage()
    {
        var original = CorrelationContext.Current;
        try
        {
            CorrelationContext.Current = null;

            var activity = new Activity("test");
            activity.AddBaggage("correlation_id", "baggage-1");
            activity.Start();

            var processor = new CorrelationIdActivityProcessor();
            processor.OnStart(activity);

            activity.GetTagItem("correlation_id").Should().Be("baggage-1");
            activity.Stop();
        }
        finally
        {
            CorrelationContext.Current = original;
        }
    }

    [Fact]
    public void OnStart_WhenNoCorrelation_DoesNotSetTag()
    {
        var original = CorrelationContext.Current;
        try
        {
            CorrelationContext.Current = null;

            var activity = new Activity("test");
            activity.Start();

            var processor = new CorrelationIdActivityProcessor();
            processor.OnStart(activity);

            activity.GetTagItem("correlation_id").Should().BeNull();
            activity.Stop();
        }
        finally
        {
            CorrelationContext.Current = original;
        }
    }
}