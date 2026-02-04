using AutoMapper;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Domain.Entities;

namespace OrderAccept.Application.Mapping;

public sealed class DtoMappingProfile : Profile
{
    public DtoMappingProfile()
    {
        CreateMap<Product, ProductDto>();
        CreateMap<OrderItem, OrderItemDto>();
        CreateMap<Order, OrderDto>();
    }
}
