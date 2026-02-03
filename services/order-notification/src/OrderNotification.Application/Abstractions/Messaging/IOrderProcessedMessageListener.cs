namespace OrderNotification.Application.Abstractions.Messaging;

/// <summary>
/// Abstraction over the inbound queue listener for OrderProcessed messages.
/// In Development we use RabbitMQ, in non-Development environments we use Azure Service Bus.
/// </summary>
/// <remarks>
/// This interface is intentionally transport-agnostic and does NOT depend on hosting primitives.
/// The concrete listener implementations are hosted by the Worker host (IHostedService) in the Infrastructure/Worker layer.
/// </remarks>
public interface IOrderProcessedMessageListener
{
}
