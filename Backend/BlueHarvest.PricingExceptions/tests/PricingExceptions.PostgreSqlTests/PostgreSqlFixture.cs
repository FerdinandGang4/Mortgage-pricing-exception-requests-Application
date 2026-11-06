using System.Diagnostics;
using Npgsql;

namespace PricingExceptions.PostgreSqlTests;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>;

// A disposable server, separate from Compose, with a loopback-only ephemeral port.
// Each test creates its own migrated database on this server.
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string containerName = "pricing-tests-" + Guid.NewGuid().ToString("N");
    private bool started;
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        try
        {
            await Docker("run", "--detach", "--rm", "--name", containerName,
                "--publish", "127.0.0.1::5432", "--env", "POSTGRES_PASSWORD=isolated-test-password",
                "postgres:17-alpine");
            started = true;
            var port = await Docker("port", containerName, "5432/tcp");
            ConnectionString = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1", Port = int.Parse(port.Trim().Split(':')[^1]),
                Database = "postgres", Username = "postgres", Password = "isolated-test-password",
                Pooling = false, Timeout = 2
            }.ConnectionString;

            using var ready = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (true)
            {
                try
                {
                    await using var connection = new NpgsqlConnection(ConnectionString);
                    await connection.OpenAsync(ready.Token);
                    break;
                }
                catch (Exception exception) when (exception is NpgsqlException or IOException)
                {
                    await Task.Delay(250, ready.Token);
                }
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "pricing_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        // Only this generated identifier is interpolated, never user input.
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    public async Task DropDatabaseAsync(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (started)
        {
            // Remove only the uniquely named test container and its anonymous volume.
            await Docker("rm", "--force", "--volumes", containerName);
            started = false;
        }
    }

    private static async Task<string> Docker(params string[] arguments)
    {
        var info = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Docker must be installed and running for PostgreSQL tests.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Docker did not finish within three minutes.");
        }
        var stdout = await output;
        var stderr = await error;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Docker {arguments[0]} failed: {stderr}");
        return stdout;
    }
}
