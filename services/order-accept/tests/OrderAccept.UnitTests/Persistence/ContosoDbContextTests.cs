using FluentAssertions;
using OrderAccept.Domain.Entities;

namespace OrderAccept.UnitTests.Persistence;

public sealed class ContosoDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_WhenEntityAdded_SetsAuditColumns()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var customer = new Customer { ExternalCustomerId = "c-1" };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        customer.CreatedAt.Should().NotBe(default);
        customer.UpdatedAt.Should().NotBe(default);
        customer.IsSoftDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityModified_UpdatesUpdatedAtOnly()
    {
        using var db = PersistenceDbContextFactory.CreateDbContext();
        var customer = new Customer { ExternalCustomerId = "c-1" };

        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        var originalCreatedAt = customer.CreatedAt;
        var originalUpdatedAt = customer.UpdatedAt;

        customer.FirstName = "New";
        await db.SaveChangesAsync();

        customer.CreatedAt.Should().Be(originalCreatedAt);
        customer.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }
}
