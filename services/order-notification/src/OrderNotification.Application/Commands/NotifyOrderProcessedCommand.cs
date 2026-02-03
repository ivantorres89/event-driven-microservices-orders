using OrderNotification.Application.Contracts.Events;

namespace OrderNotification.Application.Commands;

public sealed record NotifyOrderProcessedCommand(OrderProcessedEvent Event);
