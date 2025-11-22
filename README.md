# EFCore.NoLock

![.NET Build & Test](https://github.com/TahaZahit/EFCore.NoLock/actions/workflows/dotnet.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/EFCore.NoLock.svg)](https://www.nuget.org/packages/EFCore.NoLock)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Downloads](https://img.shields.io/nuget/dt/EFCore.NoLock.svg)](https://www.nuget.org/packages/EFCore.NoLock)

**EFCore.NoLock** is a professional Entity Framework Core extension that allows you to apply the `WITH (NOLOCK)` table hint to specific LINQ queries using a fluent API.

Unlike simple regex-based solutions, this library uses the official **Microsoft.SqlServer.TransactSql.ScriptDom** parser to safely modify the SQL syntax tree. This ensures that hints are applied correctly even in complex queries involving Joins, Subqueries, or CTEs, without breaking the SQL structure.

## 🚀 Features

- **🛡️ Safe Parsing:** Uses Microsoft's `ScriptDom` to parse and reconstruct SQL, ensuring 100% valid syntax.
- **⚡ High Performance:** Implements smart caching (`ConcurrentDictionary`) to avoid re-parsing identical queries. The overhead is negligible after the first execution.
- **📦 Easy to Use:** Simple `.WithNoLock()` extension method for `IQueryable`.
- **🔄 Async Support:** Fully supports `ToListAsync`, `FirstOrDefaultAsync`, and other async operations.
- **✅ Compatibility:** Works seamlessly with .NET 6, .NET 8, .NET 9 and .NET 10

## 📦 Installation

Install the package via NuGet Package Manager:

```bash
Install-Package EFCore.NoLock
````

Or via .NET CLI:

```bash
dotnet add package EFCore.NoLock
```

## 💻 Usage

### 1\. Register the Interceptor

Add the `WithNoLockInterceptor` to your DbContext configuration in `Program.cs` or `Startup.cs`.

```csharp
using EFCore.NoLock;

// ...

services.AddDbContext<MyDbContext>(options =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(new WithNoLockInterceptor())); // <--- Register the interceptor here
```

### 2\. Apply to Queries

Simply append `.WithNoLock()` to any `IQueryable` chain where you want to allow dirty reads.

```csharp
using EFCore.NoLock;

public async Task<List<Order>> GetActiveOrdersAsync()
{
    var orders = await _context.Orders
        .Include(o => o.OrderLines)
        .Where(o => o.IsActive)
        .WithNoLock() // <--- Apply NOLOCK hint to Orders and OrderLines
        .ToListAsync();
        
    return orders;
}
```

## 🔍 How It Works (Before & After)

When you use `.WithNoLock()`, the interceptor captures the generated SQL before it hits the database. It parses the SQL into an Abstract Syntax Tree (AST), identifies the physical tables, and injects the `WITH (NOLOCK)` hint.

**Example Scenario:**
Fetching an `Order` and its related `OrderLines`.

### \--- ORIGINAL SQL OUTPUT (EF Core Generated) ---

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

### \--- GENERATED SQL OUTPUT (EFCore.NoLock Transformed) ---

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

This project is licensed under the [MIT License](https://www.google.com/search?q=LICENSE).
