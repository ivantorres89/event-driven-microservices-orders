using Microsoft.Extensions.Logging;
using OrderProcess.Domain.Entities;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Impl.Repositories.Base;

namespace OrderProcess.Persistence.Impl.Repositories.Command;

public sealed class ProductCommandRepository : BaseEfCommandRepository<Product>, IProductCommandRepository
{
    public ProductCommandRepository(ContosoDbContext db, ILogger<ProductCommandRepository> logger)
        : base(db, logger)
    {
    }
}
