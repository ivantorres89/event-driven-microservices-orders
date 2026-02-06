using Xunit;

namespace OrderAccept.IntegrationTests.Fixtures;

/// <summary>
/// Single shared collection for all integration tests.
///
/// This guarantees:
/// - one shared fixture instance (DB is created + migrated once per test run)
/// - fully sequential execution inside this collection
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<OrderAcceptApiFixture>
{
    public const string Name = "OrderAccept.Integration";
}
