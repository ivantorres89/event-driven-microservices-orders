using OrderProcess.Persistence.Abstractions.Entities;

namespace OrderProcess.Persistence.Abstractions.Repositories.Command;

public interface IProductCommandRepository : ICommandRepository<Product>
{
}