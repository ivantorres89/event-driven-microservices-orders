using System.Diagnostics;

public static class Observability
{
    public const string ActivitySourceName = "Contoso.Orders.OrderNotification";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
