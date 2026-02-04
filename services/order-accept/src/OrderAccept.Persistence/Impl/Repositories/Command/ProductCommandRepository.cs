using Microsoft.Extensions.Logging;
using OrderAccept.Domain.Entities;
using OrderAccept.Persistence.Abstractions.Repositories;
using OrderAccept.Persistence.Abstractions.Repositories.Command;
using OrderAccept.Persistence.Impl.Repositories.Base;

namespace OrderAccept.Persistence.Impl.Repositories.Command;

public sealed class ProductCommandRepository : BaseEfCommandRepository<Product>, IProductCommandRepository
{
    public ProductCommandRepository(ContosoDbContext db, ILogger<ProductCommandRepository> logger)
        : base(db, logger)
    {
    }
}
