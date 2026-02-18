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

### 1\. Register the Interceptor

Add the `WithNoLockInterceptor` to your DbContext configuration in `Program.cs` or `Startup.cs`.

```csharp
using EFCore.NoLock;

services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(new WithNoLockInterceptor()));
```

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

When you use `.WithNoLock()`, the interceptor captures the generated SQL before it hits the database. It parses the SQL into an Abstract Syntax Tree (AST), identifies the physical tables, and injects the `WITH (NOLOCK)` hint to **every table** in the query.

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

## ⚙️ Performance & Architecture

Parsing SQL is an expensive operation. To ensure high performance in production environments:

1.  **Caching:** The library generates a unique key for every SQL query.
2.  **Lookup:** If the query has been processed before, the transformed SQL is retrieved from a thread-safe `ConcurrentDictionary` (Cache).
3.  **Result:** The heavy parsing logic (`ScriptDom`) runs **only once** per unique query. Subsequent calls are virtually instantaneous.

## ⚠️ Important Considerations

Using `WITH (NOLOCK)` is equivalent to using the `READ UNCOMMITTED` isolation level for the specific tables in the query.

* **Dirty Reads:** You may read data that is currently being modified by another transaction but has not yet been committed.
* **Use Cases:** Ideal for heavy reporting queries, analytics dashboards, or scenarios where slight data inconsistency is acceptable in exchange for performance and avoiding deadlocks.
* **Avoid For:** Do not use this for financial transactions, stock inventory updates, or critical business logic requiring strict data consistency.

## 🤝 Contributing

Contributions are welcome\! Please feel free to submit a Pull Request or open an issue on GitHub.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
