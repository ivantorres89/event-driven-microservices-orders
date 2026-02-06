using Xunit;

namespace OrderNotification.IntegrationTests.Fixtures;

/// <summary>
/// Single shared collection for all integration tests.
///
/// This guarantees:
/// - one shared fixture instance
/// - fully sequential execution inside this collection
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<OrderNotificationLocalInfraFixture>
{
    public const string Name = "OrderNotification.Integration";
}
