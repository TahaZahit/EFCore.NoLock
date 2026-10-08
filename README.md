# EFCore.NoLock

![.NET Build & Test](https://github.com/TahaZahit/EFCore.NoLock/actions/workflows/dotnet.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/EFCore.NoLock.svg)](https://www.nuget.org/packages/EFCore.NoLock)
[![NuGet LinqToDB](https://img.shields.io/nuget/v/EFCore.NoLock.LinqToDb.svg?label=nuget%20%7C%20LinqToDb)](https://www.nuget.org/packages/EFCore.NoLock.LinqToDb)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Downloads](https://img.shields.io/nuget/dt/EFCore.NoLock.svg)](https://www.nuget.org/packages/EFCore.NoLock)

**EFCore.NoLock** is a professional extension that allows you to apply the `WITH (NOLOCK)` table hint to specific LINQ queries using a fluent `.WithNoLock()` API. Supports both **Entity Framework Core** and **LinqToDB**.

Unlike simple regex-based solutions, this library uses the official **Microsoft.SqlServer.TransactSql.ScriptDom** parser to safely modify the SQL syntax tree. This ensures that hints are applied correctly even in complex queries involving Joins, Subqueries, or CTEs, without breaking the SQL structure.

## 🚀 Features

- **🛡️ Safe Parsing:** Uses Microsoft's `ScriptDom` to parse and reconstruct SQL, ensuring 100% valid syntax.
- **⚡ High Performance:** Implements smart caching (`ConcurrentDictionary`) to avoid re-parsing identical queries. The overhead is negligible after the first execution.
- **📦 Easy to Use:** Simple `.WithNoLock()` extension method for `IQueryable`.
- **🔄 Async Support:** Fully supports `ToListAsync`, `FirstOrDefaultAsync`, and other async operations.
- **🔌 Multi-ORM:** Works with both Entity Framework Core and LinqToDB.
- **✅ Compatibility:** .NET 6, .NET 7, .NET 8, .NET 9 and .NET 10.

## 📦 Packages

| Package | Description | NuGet |
|---|---|---|
| `EFCore.NoLock` | EF Core interceptor | [![NuGet](https://img.shields.io/nuget/v/EFCore.NoLock.svg)](https://www.nuget.org/packages/EFCore.NoLock) |
| `EFCore.NoLock.LinqToDb` | LinqToDB interceptor | [![NuGet](https://img.shields.io/nuget/v/EFCore.NoLock.LinqToDb.svg)](https://www.nuget.org/packages/EFCore.NoLock.LinqToDb) |
| `EFCore.NoLock.Core` | Shared engine (auto-installed) | [![NuGet](https://img.shields.io/nuget/v/EFCore.NoLock.Core.svg)](https://www.nuget.org/packages/EFCore.NoLock.Core) |

## 📦 Installation

**For Entity Framework Core:**

```bash
dotnet add package EFCore.NoLock
```

**For LinqToDB:**

```bash
dotnet add package EFCore.NoLock.LinqToDb
```

> Both packages automatically include `EFCore.NoLock.Core` as a transitive dependency.

## 💻 Usage — Entity Framework Core

### 1\. Register

Two mechanisms are available; pick one.

**`UseNoLock()` — recommended.** Emits the hint while the SQL is generated, so `WITH (NOLOCK)` is part of the SQL EF Core produces and **appears in EF Core's own command logs** — no capturing interceptor needed to verify it. No SQL re-parsing at execution time.

```csharp
using EFCore.NoLock;

services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString)
           .UseNoLock());
```

**`WithNoLockInterceptor` — deprecated.** Rewrites the SQL at execution time via the ScriptDom parser. Kept only for cases where replacing the query SQL generator is not an option; prefer `UseNoLock()`. Note: the hint is applied after EF Core captures the command text for logging, so it will **not** show up in EF Core's command logs (the actual SQL sent to the database still carries it).

```csharp
using EFCore.NoLock;

services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(new WithNoLockInterceptor()));
```

> Both are per-query: the hint is applied only to queries marked with `.WithNoLock()`. Register only one.

### 2\. Apply to Queries

```csharp
using EFCore.NoLock.Core;

public async Task<List<Order>> GetActiveOrdersAsync()
{
    var orders = await _context.Orders
        .Include(o => o.OrderLines)
        .Where(o => o.IsActive)
        .WithNoLock()
        .ToListAsync();

    return orders;
}
```

## 💻 Usage — LinqToDB

### 1\. Register the Interceptor

```csharp
using EFCore.NoLock.LinqToDb;
using LinqToDB;
using LinqToDB.DataProvider.SqlServer;

var options = new DataOptions()
    .UseSqlServer(connectionString)
    .UseInterceptor(new LinqToDbWithNoLockInterceptor());

using var db = new DataConnection(options);
```

### 2\. Apply to Queries

```csharp
using EFCore.NoLock.Core;

var products = db.GetTable<Product>()
    .Where(p => p.IsActive)
    .WithNoLock()
    .ToList();
```

## 🔍 How It Works (Before & After)

When you use `.WithNoLock()`, the hint is bound to the query itself via a tag marker (EF Core `TagWith` / LinqToDB `TagQuery`). The interceptor detects that tag in the generated SQL before it hits the database, parses the SQL into an Abstract Syntax Tree (AST), identifies the physical tables, and injects the `WITH (NOLOCK)` hint into **every table** in the query.

Because the tag travels with the query, the hint is preserved through composition (`Select`, `SelectMany`, `Where`, …), through repeated executions of the same `IQueryable`, and through **every** command of an `AsSplitQuery` — not just the first one.

**Example Scenario:**
Fetching an `Order` and its related `OrderLines`.

### --- ORIGINAL SQL OUTPUT (ORM Generated) ---

```sql
SELECT   [o].[Id],
         [o].[CustomerName],
         [o0].[Id],
         [o0].[OrderId],
         [o0].[Product]
FROM     [Orders] AS [o]
             LEFT OUTER JOIN
         [OrderLines] AS [o0]
         ON [o].[Id] = [o0].[OrderId]
WHERE    [o].[Id] = 1
ORDER BY [o].[Id];
```

### --- TRANSFORMED SQL OUTPUT (WITH NOLOCK) ---

```sql
SELECT   [o].[Id],
         [o].[CustomerName],
         [o0].[Id],
         [o0].[OrderId],
         [o0].[Product]
FROM     [Orders] AS [o] WITH (NOLOCK)
         LEFT OUTER JOIN
         [OrderLines] AS [o0] WITH (NOLOCK)
         ON [o].[Id] = [o0].[OrderId]
WHERE    [o].[Id] = 1
ORDER BY [o].[Id];
```

## 🏗️ Architecture

```
EFCore.NoLock.Core              ← Shared SQL transformation engine (ScriptDom + Cache)
├── EFCore.NoLock               ← EF Core DbCommandInterceptor
└── EFCore.NoLock.LinqToDb      ← LinqToDB CommandInterceptor
```

The core engine is ORM-agnostic. Each ORM package provides a thin interceptor that delegates SQL transformation to `EFCore.NoLock.Core`.

## ⚡ Performance

### `UseNoLock()` — no parsing, no cache

The generator emits `WITH (NOLOCK)` **while EF Core is already generating the SQL**: it appends the hint inline as each table is visited (`VisitTable`). Two consequences follow:

- **Nothing is re-parsed.** The hint is produced during EF Core's single, existing SQL-generation pass — `ScriptDom` is never invoked on this path. The cost is a few `Append(" WITH (NOLOCK)")` string writes.
- **There is nothing to cache.** The generator runs as part of query *compilation*, and EF Core already caches compiled queries. The hint is baked into the SQL that EF Core caches, so the generator runs **once per unique query**, not once per execution — every repeated execution reuses the cached SQL for free.

### `WithNoLockInterceptor` (deprecated) — why it needed a cache

The interceptor runs on **every command execution** (`ReaderExecuting`), after EF Core has produced the final SQL string. It parses that string into a T-SQL syntax tree with `ScriptDom`, injects the hints, and regenerates the SQL. EF Core's compiled-query cache doesn't help here — it caches the SQL *before* the interceptor mutates it — so the interceptor fires on every execution and must keep **its own** thread-safe `ConcurrentDictionary` to avoid re-parsing identical SQL:

1.  **Key:** a unique key per SQL string, with leading comments stripped so per-request tags (e.g. `cid`/`uid`) don't inflate the cache.
2.  **Lookup:** previously transformed SQL is served straight from the cache.
3.  **Result:** `ScriptDom` parses each unique query only once; subsequent executions hit the cache.

`UseNoLock()` is preferred precisely because it does the work at the SQL-generation layer: it removes both the per-execution parse **and** the extra cache the interceptor had to carry.

## ⚠️ Important Considerations

Using `WITH (NOLOCK)` is equivalent to using the `READ UNCOMMITTED` isolation level for the specific tables in the query.

* **Dirty Reads:** You may read data that is currently being modified by another transaction but has not yet been committed.
* **Use Cases:** Ideal for heavy reporting queries, analytics dashboards, or scenarios where slight data inconsistency is acceptable in exchange for performance and avoiding deadlocks.
* **Avoid For:** Do not use this for financial transactions, stock inventory updates, or critical business logic requiring strict data consistency.

## 🤝 Contributing

Contributions are welcome\! Please feel free to submit a Pull Request or open an issue on GitHub.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
