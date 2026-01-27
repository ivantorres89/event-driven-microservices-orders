using OrderAccept.Application.Contracts.Requests;

namespace OrderAccept.Application.Commands
{
    public sealed record AcceptOrderCommand(CreateOrderRequest Order);
}
