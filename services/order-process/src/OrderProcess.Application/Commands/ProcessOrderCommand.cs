using OrderProcess.Application.Contracts.Events;

namespace OrderProcess.Application.Commands;

public sealed record ProcessOrderCommand(OrderAcceptedEvent Event);
