using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderNotification.Infrastructure;

namespace OrderNotification.UnitTests.Infraestructure;

public sealed class DependencyInjectionTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = default!;
        public string ApplicationName { get; set; } = default!;
        public string ContentRootPath { get; set; } = default!;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = default!;
    }

    [Fact]
    public void AddOrderNotificationInfrastructure_WhenRedisMissing_Throws()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Development };

        var act = () => services.AddOrderNotificationInfrastructure(configuration, environment);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("ConnectionStrings:Redis is required");
    }
}
