using Xunit;

namespace OrderProcess.IntegrationTests.Fixtures;

/// <summary>
/// Single shared collection for all integration tests.
///
/// This guarantees:
/// - one shared fixture instance (DB is created + migrated once per test run)
/// - fully sequential execution inside this collection
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<OrderProcessLocalInfraFixture>
{
    public const string Name = "OrderProcess.Integration";
}
