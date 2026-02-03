using System.Diagnostics;

namespace OrderNotification.Shared.Observability;

public static class Observability
{
    public const string ActivitySourceName = "Contoso.Orders.OrderNotification";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
