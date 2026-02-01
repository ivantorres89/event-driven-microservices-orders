using OrderProcess.Application.Abstractions.Persistence;
using OrderProcess.Persistence.Abstractions.Repositories;
using OrderProcess.Persistence.Impl.Repositories;

namespace OrderProcess.Persistence.Impl;

public sealed class ContosoUnitOfWork : IContosoUnitOfWork
{
    private readonly ContosoDbContext _db;

    public ContosoUnitOfWork(ContosoDbContext db)
    {
        _db = db;
        Customers = new CustomerRepository(db);
        Orders = new OrderRepository(db);
        Products = new ProductRepository(db);
        OrderItems = new OrderItemRepository(db);
    }

    public ICustomerRepository Customers { get; }
    public IOrderRepository Orders { get; }
    public IProductRepository Products { get; }
    public IOrderItemRepository OrderItems { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
