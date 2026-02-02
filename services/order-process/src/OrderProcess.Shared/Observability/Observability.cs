using System.Diagnostics;

namespace OrderProcess.Shared.Observability;

public static class Observability
{
    public const string ActivitySourceName = "Contoso.Orders.OrderProcess";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}