# ElBruno.Connectors.SqliteVec

[![NuGet](https://img.shields.io/nuget/v/ElBruno.Connectors.SqliteVec.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Connectors.SqliteVec)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.Connectors.SqliteVec.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.Connectors.SqliteVec)
[![Build Status](https://github.com/elbruno/ElBruno.Connectors.SqliteVec/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/ElBruno.Connectors.SqliteVec/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.Connectors.SqliteVec?style=social)](https://github.com/elbruno/ElBruno.Connectors.SqliteVec)
[![Twitter Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://twitter.com/elbruno)

A **MEAI-native** `VectorStoreCollection` implementation for [sqlite-vec](https://github.com/asg017/sqlite-vec), based on the original [Semantic Kernel](https://github.com/microsoft/semantic-kernel) implementation. Store, query, and search vector embeddings using SQLite with full support for the [Microsoft.Extensions.VectorData](https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions) abstractions.

## Features

- ✅ **VectorStoreCollection implementation** — full CRUD + vector similarity search
- ✅ **Dependency injection** — register collections with `AddSqliteVecCollection<TKey, TRecord>`
- ✅ **Attribute-based mapping** — use `[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]`
- ✅ **Filtered retrieval** — query records with LINQ-style expressions
- ✅ **Text splitting** — built-in `TextSplitter.SplitParagraphs` for chunking documents
- ✅ **Optional embedding generation** — works with `IEmbeddingGenerator<string, Embedding<float>>` or raw float vectors
- ✅ **Multi-target** — supports .NET 9 and .NET 10

## Installation

```bash
dotnet add package ElBruno.Connectors.SqliteVec
```

## Quick Start

### Define your record type

```csharp
using Microsoft.Extensions.VectorData;

public class GlossaryEntry
{
    [VectorStoreKey]
    public string Key { get; set; } = string.Empty;

    [VectorStoreData]
    public string Term { get; set; } = string.Empty;

    [VectorStoreData]
    public string Definition { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 4)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

### Register and use the collection

```csharp
using ElBruno.Connectors.SqliteVec;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

var services = new ServiceCollection();
services.AddSqliteVecCollection<string, GlossaryEntry>(
    collectionName: "glossary",
    connectionString: "Data Source=mydata.db");

var provider = services.BuildServiceProvider();
var collection = provider.GetRequiredService<VectorStoreCollection<string, GlossaryEntry>>();

// Create the collection
await collection.EnsureCollectionExistsAsync();

// Upsert a record
await collection.UpsertAsync(new GlossaryEntry
{
    Key = "1",
    Term = "Embedding",
    Definition = "A numerical representation of data in a vector space.",
    Embedding = new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f, 0.4f])
});

// Get by key
var entry = await collection.GetAsync("1");

// Vector similarity search
var query = new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f, 0.4f]);
await foreach (var result in collection.SearchAsync(query, top: 5))
{
    Console.WriteLine($"{result.Record.Term}: {result.Score:F4}");
}
```

### Text splitting

```csharp
using ElBruno.Connectors.SqliteVec;

var chunks = TextSplitter.SplitParagraphs(
    new[] { "Your long document text goes here..." },
    maxWordsPerChunk: 100);
```

## API Reference

| Type | Description |
|------|-------------|
| `SqliteVecVectorStoreCollection<TKey, TRecord>` | Core collection class — implements `VectorStoreCollection<TKey, TRecord>` |
| `SqliteVecServiceCollectionExtensions` | DI extension method `AddSqliteVecCollection` |
| `TextSplitter` | Static helper to split text into word-based chunks |

### Key Methods

| Method | Description |
|--------|-------------|
| `EnsureCollectionExistsAsync()` | Creates the SQLite table and vec virtual table |
| `EnsureCollectionDeletedAsync()` | Drops the tables |
| `UpsertAsync(record)` | Insert or replace a single record |
| `UpsertAsync(records)` | Batch insert or replace (transactional) |
| `GetAsync(key)` | Retrieve a record by key |
| `GetAsync(filter, top)` | Retrieve records matching a filter expression |
| `SearchAsync(vector, top)` | Vector similarity search |
| `DeleteAsync(key)` | Delete a record by key |

## Samples

- [**BasicSample**](samples/BasicSample) — CRUD operations and vector search with raw float vectors
- [**IntermediateSample**](samples/IntermediateSample) — TextSplitter, multiple collections, filtered retrieval, and embedding generation

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

## 👨‍💻 About the Author

**Bruno Capuano** (aka El Bruno) is a passionate developer, AI enthusiast, and content creator who loves building practical solutions and sharing knowledge with the community.

### 🌐 Connect & Explore

- 🐙 **GitHub**: [github.com/elbruno](https://github.com/elbruno/) - More cool projects and open-source contributions
- 📝 **Blog**: [elbruno.com](https://elbruno.com) - Technical articles, tutorials, and insights
- 🎙️ **Podcast**: [No Tiene Nombre](https://notienenombre.com) - Spanish tech podcast
- 🎥 **YouTube**: [youtube.com/elbruno](https://www.youtube.com/elbruno) - Video tutorials and demos
- 💼 **LinkedIn**: [linkedin.com/in/elbruno](https://www.linkedin.com/in/elbruno/) - Professional updates and articles
- 🐦 **Twitter/X**: [@elbruno](https://www.x.com/elbruno/) - Quick tips and tech discussions

### 💡 Support This Project

If you find this library useful:

- ⭐ **Star this repo** on GitHub
- 📢 **Share it** with your network
- 🐛 **Report issues** or suggest features
- 🤝 **Contribute** via pull requests

Happy coding! 🚀
