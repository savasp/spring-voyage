// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hosted service that applies any pending EF Core migrations to the
/// <see cref="SpringDbContext"/> on host startup. Gated by
/// <see cref="DatabaseOptions.AutoMigrate"/> so operators who run
/// migrations out-of-band (CI/CD, scripted SQL dumps) can disable it.
/// </summary>
/// <remarks>
/// Only runs when the configured provider is relational. Non-relational
/// providers (for example <c>UseInMemoryDatabase</c> in tests) do not
/// support migrations; for those the schema is managed by the test
/// harness itself via <see cref="DatabaseFacade.EnsureCreatedAsync(System.Threading.CancellationToken)"/>.
/// </remarks>
public class DatabaseMigrator(
    IServiceProvider services,
    IConfiguration configuration,
    IOptions<DatabaseOptions> options,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    private readonly DatabaseOptions _options = options.Value;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoMigrate)
        {
            logger.LogInformation(
                "Database:AutoMigrate disabled — skipping automatic EF Core migration. Operators must apply migrations out-of-band.");
            return;
        }

        // When no connection string is set the host is running without
        // a database (for example under build-time OpenAPI tooling).
        // There is nothing to migrate — and resolving the DbContext
        // and probing the provider can trip up assembly resolution in
        // the tooling process.
        var connectionString = configuration.GetConnectionString("SpringDb");
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogDebug(
                "ConnectionStrings:SpringDb is not configured; skipping MigrateAsync.");
            return;
        }

        await MigrateCoreAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Isolated so the JIT only pulls in Microsoft.EntityFrameworkCore.Relational
    // when we've already decided to run migrations. Build-time tooling that
    // loads the host without a connection string (for example
    // dotnet-getdocument during OpenAPI snapshot) never enters this method,
    // avoiding assembly-resolution issues in the tooling process.
    private async Task MigrateCoreAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SpringDbContext>();

        if (!context.Database.IsRelational())
        {
            // In-memory / test providers don't support migrations. Test
            // harnesses seed schema via EnsureCreatedAsync themselves.
            logger.LogDebug(
                "SpringDbContext is configured with a non-relational provider; skipping MigrateAsync.");
            return;
        }

        logger.LogInformation("Applying pending EF Core migrations to SpringDbContext.");
        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("EF Core migrations applied successfully.");
    }
}