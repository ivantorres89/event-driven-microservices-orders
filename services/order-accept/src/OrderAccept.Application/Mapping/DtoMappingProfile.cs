using AutoMapper;
using OrderAccept.Application.Contracts.Responses;
using OrderAccept.Domain.Entities;

namespace OrderAccept.Application.Mapping;

public sealed class DtoMappingProfile : Profile
{
    public DtoMappingProfile()
    {
        CreateMap<Product, ProductDto>()
            .ForCtorParam(nameof(ProductDto.ExternalProductId), o => o.MapFrom(s => s.ExternalProductId))
            .ForCtorParam(nameof(ProductDto.Name), o => o.MapFrom(s => s.Name))
            .ForCtorParam(nameof(ProductDto.Category), o => o.MapFrom(s => s.Category))
            .ForCtorParam(nameof(ProductDto.Vendor), o => o.MapFrom(s => s.Vendor))
            .ForCtorParam(nameof(ProductDto.ImageUrl), o => o.MapFrom(s => s.ImageUrl))
            .ForCtorParam(nameof(ProductDto.Discount), o => o.MapFrom(s => s.Discount))
            .ForCtorParam(nameof(ProductDto.BillingPeriod), o => o.MapFrom(s => s.BillingPeriod))
            .ForCtorParam(nameof(ProductDto.IsSubscription), o => o.MapFrom(s => s.IsSubscription))
            .ForCtorParam(nameof(ProductDto.Price), o => o.MapFrom(s => s.Price));

        CreateMap<OrderItem, OrderItemDto>()
            .ForCtorParam(nameof(OrderItemDto.ProductId), o => o.MapFrom(s => s.Product != null ? s.Product.ExternalProductId : string.Empty))
            .ForCtorParam(nameof(OrderItemDto.ProductName), o => o.MapFrom(s => s.Product != null ? s.Product.Name : string.Empty))
            .ForCtorParam(nameof(OrderItemDto.ImageUrl), o => o.MapFrom(s => s.Product != null ? s.Product.ImageUrl : string.Empty))
            .ForCtorParam(nameof(OrderItemDto.UnitPrice), o => o.MapFrom(s => s.Product != null ? s.Product.Price : 0m))
            .ForCtorParam(nameof(OrderItemDto.Quantity), o => o.MapFrom(s => s.Quantity));

        CreateMap<Order, OrderDto>()
            .ForCtorParam(nameof(OrderDto.Id), o => o.MapFrom(s => s.Id))
            .ForCtorParam(nameof(OrderDto.CorrelationId), o => o.MapFrom(s => s.CorrelationId))
            .ForCtorParam(nameof(OrderDto.CreatedAt), o => o.MapFrom(s => s.CreatedAt))
            .ForCtorParam(nameof(OrderDto.Items), o => o.MapFrom(s => s.Items));
    }
}
