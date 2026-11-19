using System.Diagnostics;
using Npgsql;

namespace OrdersApi.Tests;

// The numbers used in the article. Run with:
// dotnet test -- --filter-trait "Category=Audit"
[Collection(nameof(OrdersApiCollection))]
[Trait("Category", "Audit")]
public class AuditMeasurements(OrdersApiFactory factory)
{
    private const int WarmupRuns = 5;
    private const int MeasuredRuns = 50;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Measure_first_draft_against_audited_endpoint()
    {
        var output = new AuditOutput();
        var cases = new[]
        {
            ("first page", "pageSize=20"),
            ("Shipped, page 10", "status=Shipped&page=10&pageSize=20")
        };

        foreach (var (name, query) in cases)
        {
            foreach (var version in new[] { "v1", "v2" })
            {
                var url = $"/{version}/customers/{TestData.BigCustomerId}/orders?{query}";
                var (median, p95, commands) = await Measure(url);
                output.WriteLine($"{name,-18} {version}: median {median,7:F2} ms | p95 {p95,7:F2} ms | {commands,3} SQL commands");
            }
        }
    }

    [Fact]
    public async Task Show_query_plan_with_and_without_the_composite_index()
    {
        var output = new AuditOutput();
        var sql = $"""
            EXPLAIN ANALYZE
            SELECT "Id", "CreatedAt", "Status" FROM "Orders"
            WHERE "CustomerId" = {TestData.BigCustomerId}
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT 20 OFFSET 180
            """;

        await using var connection = new NpgsqlConnection(factory.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await Execute(connection, """DROP INDEX "IX_Orders_CustomerId_CreatedAt";""");
        await Execute(connection, "ANALYZE \"Orders\";");
        output.WriteLine("Without the composite index:");
        foreach (var line in await Query(connection, sql)) output.WriteLine("  " + line);

        await Execute(connection, """CREATE INDEX "IX_Orders_CustomerId_CreatedAt" ON "Orders" ("CustomerId", "CreatedAt");""");
        await Execute(connection, "ANALYZE \"Orders\";");
        output.WriteLine("With the composite index:");
        foreach (var line in await Query(connection, sql)) output.WriteLine("  " + line);
    }

    // Writes to the test output and to audit-results.txt next to the test binaries,
    // because the test runner hides output from passing tests.
    private sealed class AuditOutput
    {
        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "audit-results.txt");

        public void WriteLine(string line)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(line);
            File.AppendAllLines(FilePath, [line]);
        }
    }

    private async Task<(double Median, double P95, int Commands)> Measure(string url)
    {
        var ct = TestContext.Current.CancellationToken;
        for (var i = 0; i < WarmupRuns; i++)
        {
            (await _client.GetAsync(url, ct)).EnsureSuccessStatusCode();
        }

        factory.SqlCommands.Reset();
        (await _client.GetAsync(url, ct)).EnsureSuccessStatusCode();
        var commands = factory.SqlCommands.Count;

        var timings = new List<double>();
        for (var i = 0; i < MeasuredRuns; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            (await _client.GetAsync(url, ct)).EnsureSuccessStatusCode();
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        return (timings[timings.Count / 2], timings[(int)(timings.Count * 0.95) - 1], commands);
    }

    private static async Task Execute(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<string>> Query(NpgsqlConnection connection, string sql)
    {
        var lines = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            lines.Add(reader.GetString(0));
        }
        return lines;
    }
}
