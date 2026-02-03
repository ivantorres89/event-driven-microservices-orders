using OrderNotification.Worker.Hubs.Models;

namespace OrderNotification.Worker.Hubs;

public interface IOrderStatusClient
{
    Task Notification(OrderStatusNotification message);
}
