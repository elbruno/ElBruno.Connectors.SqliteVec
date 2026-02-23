using ElBruno.Connectors.SqliteVec;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

const string connectionString = "Data Source=intermediate_sample.db";

// ========================================
// Part 1: TextSplitter usage
// ========================================
Console.WriteLine("=== Part 1: TextSplitter ===\n");

var document = new[]
{
    "Vector databases are specialized systems designed to store and query high-dimensional vector embeddings efficiently.",
    "They enable similarity search by finding the nearest neighbors in a vector space.",
    "Common use cases include recommendation systems, semantic search, and retrieval-augmented generation (RAG).",
    "sqlite-vec is a lightweight SQLite extension that adds vector search capabilities to any SQLite database."
};

var chunks = TextSplitter.SplitParagraphs(document, maxWordsPerChunk: 15);
Console.WriteLine($"Split document into {chunks.Count} chunks:");
for (int i = 0; i < chunks.Count; i++)
    Console.WriteLine($"  Chunk {i}: {chunks[i]}");

// ========================================
// Part 2: Multiple collections
// ========================================
Console.WriteLine("\n=== Part 2: Multiple Collections ===\n");

var embeddingGen = new FakeEmbeddingGenerator();

var docCollection = new SqliteVecVectorStoreCollection<string, DocumentChunk>(
    "documents", connectionString, embeddingGen);
var faqCollection = new SqliteVecVectorStoreCollection<string, FaqEntry>(
    "faqs", connectionString, embeddingGen);

await docCollection.EnsureCollectionExistsAsync();
await faqCollection.EnsureCollectionExistsAsync();
Console.WriteLine("Created 'documents' and 'faqs' collections.");

// Upsert document chunks with embeddings from TextSplitter output
for (int i = 0; i < chunks.Count; i++)
{
    var embedding = await embeddingGen.GenerateAsync([chunks[i]]);
    await docCollection.UpsertAsync(new DocumentChunk
    {
        Id = $"doc-{i}",
        Source = "vector-db-intro.md",
        Content = chunks[i],
        Embedding = embedding[0].Vector
    });
}
Console.WriteLine($"Upserted {chunks.Count} document chunks.");

// Upsert FAQ entries
var faqs = new[]
{
    new FaqEntry { Id = "faq-1", Category = "setup", Question = "How do I install sqlite-vec?", Answer = "Use the NuGet package." },
    new FaqEntry { Id = "faq-2", Category = "setup", Question = "What .NET versions are supported?", Answer = ".NET 9 and .NET 10." },
    new FaqEntry { Id = "faq-3", Category = "usage", Question = "How do I search for similar vectors?", Answer = "Use SearchAsync with a query vector." },
    new FaqEntry { Id = "faq-4", Category = "usage", Question = "Can I filter search results?", Answer = "Yes, use VectorSearchOptions with a Filter." },
};

foreach (var faq in faqs)
{
    var embedding = await embeddingGen.GenerateAsync([faq.Question]);
    faq.Embedding = embedding[0].Vector;
}
await faqCollection.UpsertAsync(faqs);
Console.WriteLine($"Upserted {faqs.Length} FAQ entries.");

// ========================================
// Part 3: SearchAsync with vector
// ========================================
Console.WriteLine("\n=== Part 3: Vector Search ===\n");

var queryEmbedding = await embeddingGen.GenerateAsync(["vector similarity search"]);
var queryVector = queryEmbedding[0].Vector;

Console.WriteLine("Searching documents for 'vector similarity search':");
await foreach (var result in docCollection.SearchAsync(queryVector, top: 3))
{
    Console.WriteLine($"  Score={result.Score:F4} | {result.Record.Content[..Math.Min(80, result.Record.Content.Length)]}...");
}

Console.WriteLine("\nSearching FAQs for 'vector similarity search':");
await foreach (var result in faqCollection.SearchAsync(queryVector, top: 2))
{
    Console.WriteLine($"  Score={result.Score:F4} | Q: {result.Record.Question}");
}

// ========================================
// Part 4: Filtered search
// ========================================
Console.WriteLine("\n=== Part 4: Filtered Retrieval ===\n");

Console.WriteLine("FAQs in 'setup' category:");
await foreach (var entry in faqCollection.GetAsync(f => f.Category == "setup", top: 10))
{
    Console.WriteLine($"  [{entry.Id}] Q: {entry.Question} A: {entry.Answer}");
}

Console.WriteLine("\nFAQs in 'usage' category:");
await foreach (var entry in faqCollection.GetAsync(f => f.Category == "usage", top: 10))
{
    Console.WriteLine($"  [{entry.Id}] Q: {entry.Question} A: {entry.Answer}");
}

// --- Clean up ---
await docCollection.EnsureCollectionDeletedAsync();
await faqCollection.EnsureCollectionDeletedAsync();
Console.WriteLine("\nAll collections deleted. Done!");

// --- Record types ---
public class DocumentChunk
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Source { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 8)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

public class FaqEntry
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    [VectorStoreData]
    public string Question { get; set; } = string.Empty;

    [VectorStoreData]
    public string Answer { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 8)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

// --- Fake embedding generator for demo purposes ---
public class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata { get; } = new("FakeEmbedding");

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new GeneratedEmbeddings<Embedding<float>>();
        foreach (var text in values)
        {
            var hash = text.GetHashCode();
            var rng = new Random(hash);
            var vector = new float[8];
            for (int i = 0; i < 8; i++)
                vector[i] = (float)rng.NextDouble();
            results.Add(new Embedding<float>(vector));
        }
        return Task.FromResult(results);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
    public TService? GetService<TService>(object? key = null) where TService : class => null;
}
