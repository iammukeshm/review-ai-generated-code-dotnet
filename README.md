# How I Review AI-Written C# Code: Tests First, Then Audits

Source code for the article on [codewithmukesh.com](https://codewithmukesh.com/blog/review-ai-generated-code-dotnet/).

A small Orders API on .NET 10 with two versions of the same endpoint:

- `GET /v1/customers/{id}/orders` is the first draft. It returns the right data and passes every behavior test, but it loads every order into memory, pages in memory and runs one extra query per order.
- `GET /v2/customers/{id}/orders` is the same endpoint after the audit pass. It filters, sorts, pages and sums in SQL, caps the page size, flows the `CancellationToken` and uses a composite index.

## Stack

- .NET 10 (SDK 10.0.404, ASP.NET Core 10.0.15)
- EF Core 10 with Npgsql 10.0.3
- PostgreSQL 17 through Testcontainers 4.17.0
- xUnit v3 on Microsoft.Testing.Platform (see `global.json`)

## Run it

Docker must be running, because the tests start a real PostgreSQL container.

```bash
dotnet test
```

- `OrderListTests` are the behavior tests. Both versions pass all of them.
- `OrderListBudgetTests` are the audit findings turned into tests. The `v1` cases are skipped. Remove `Skip` to watch the first draft fail.
- `AuditMeasurements` prints the timings and the PostgreSQL query plans used in the article:

```bash
dotnet test -c Release -- --filter-trait "Category=Audit"
```

The results are also written to `tests/OrdersApi.Tests/bin/Release/net10.0/audit-results.txt`.

## Results on my machine

| Case | Version | Median | SQL commands |
|---|---|---|---|
| First page | v1 | 74.62 ms | 22 |
| First page | v2 | 2.73 ms | 2 |
| Shipped, page 10 | v1 | 29.52 ms | 22 |
| Shipped, page 10 | v2 | 1.80 ms | 2 |
