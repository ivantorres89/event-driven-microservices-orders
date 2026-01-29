using System.Diagnostics;
using OpenTelemetry;
using OrderAccept.Shared.Correlation;

namespace OrderAccept.Api.Observability;

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

        // If already set, don't overwrite.
        foreach (var tag in activity.TagObjects)
        {
            if (tag.Key == TagName)
                return;
        }

        // Prefer the business correlation context.
        var correlation = CorrelationContext.Current?.Value.ToString();

        // Fallback to baggage (propagated across services).
        correlation ??= activity.GetBaggageItem(BaggageKey);

        if (string.IsNullOrWhiteSpace(correlation))
            return;

        activity.SetTag(TagName, correlation);
    }
}
