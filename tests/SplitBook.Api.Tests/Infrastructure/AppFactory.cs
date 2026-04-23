using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Infrastructure.Persistence;
using Xunit;

namespace SplitBook.Api.Tests.Infrastructure;

/// <summary>
/// Test factory that wires up a unique SQLite database per test class instance.
/// Each test class that uses this factory gets its own isolated database.
/// </summary>
public class AppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString = $"Data Source={Guid.NewGuid()}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the original DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register SQLite with a unique connection string per test class
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connectionString);
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Ensure the database schema is created
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public new async Task DisposeAsync()
    {
        // Clean up the database file
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}
