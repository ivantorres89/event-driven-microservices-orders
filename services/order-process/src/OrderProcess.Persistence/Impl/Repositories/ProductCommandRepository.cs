using Microsoft.Extensions.Logging;
using OrderProcess.Persistence.Abstractions.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;

namespace OrderProcess.Persistence.Impl.Repositories;

public sealed class ProductCommandRepository : BaseEfCommandRepository<Product>, IProductCommandRepository
{
    public ProductCommandRepository(ContosoDbContext db, ILogger<ProductCommandRepository> logger)
        : base(db, logger)
    {
    }
}
