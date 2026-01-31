using Microsoft.Extensions.Hosting;

namespace OrderProcess.Application.Abstractions.Messaging;

/// <summary>
/// Abstraction over the inbound queue listener for OrderAccepted messages.
/// In Development we use RabbitMQ, in non-Development environments we use Azure Service Bus.
/// </summary>
public interface IOrderAcceptedMessageListener : IHostedService
{
}
