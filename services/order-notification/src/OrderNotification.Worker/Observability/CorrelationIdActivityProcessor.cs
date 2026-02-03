using System.Diagnostics;
using OpenTelemetry;
using OrderNotification.Shared.Correlation;

namespace OrderNotification.Worker.Observability;

/// <summary>
/// Adds the current CorrelationId (business) as a span attribute so every span can be queried by it.
/// We read from the business CorrelationContext (AsyncLocal) and also support the W3C baggage key.
/// </summary>
internal sealed class CorrelationIdActivityProcessor : BaseProcessor<Activity>
{
    private const string TagName = "correlation_id";
    private const string BaggageKey = "correlation_id";

    public override void OnStart(Activity activity) => TrySet(activity);

    public override void OnEnd(Activity activity) => TrySet(activity);

    private static void TrySet(Activity activity)
    {
        if (activity is null)
            return;

        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key == TagName)
                return;
        }

        var correlation = CorrelationContext.Current?.Value.ToString();
        correlation ??= activity.GetBaggageItem(BaggageKey);

        if (string.IsNullOrWhiteSpace(correlation))
            return;

        activity.SetTag(TagName, correlation);
    }
}
