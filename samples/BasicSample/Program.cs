using ElBruno.Connectors.SqliteVec;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

// --- Set up DI ---
var services = new ServiceCollection();
services.AddSqliteVecCollection<string, GlossaryEntry>(
    collectionName: "glossary",
    connectionString: "Data Source=basic_sample.db");

var provider = services.BuildServiceProvider();
var collection = provider.GetRequiredService<VectorStoreCollection<string, GlossaryEntry>>();

// --- Ensure the collection exists ---
await collection.EnsureCollectionExistsAsync();
Console.WriteLine("Collection 'glossary' created.");

// --- Upsert records ---
var entries = new[]
{
    new GlossaryEntry
    {
        Key = "1",
        Term = "Vector Database",
        Definition = "A database optimized for storing and querying vector embeddings.",
        Embedding = new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f, 0.4f])
    },
    new GlossaryEntry
    {
        Key = "2",
        Term = "Embedding",
        Definition = "A numerical representation of data in a continuous vector space.",
        Embedding = new ReadOnlyMemory<float>([0.5f, 0.6f, 0.7f, 0.8f])
    },
    new GlossaryEntry
    {
        Key = "3",
        Term = "Cosine Similarity",
        Definition = "A metric used to measure how similar two vectors are.",
        Embedding = new ReadOnlyMemory<float>([0.9f, 0.1f, 0.2f, 0.3f])
    }
};

await collection.UpsertAsync(entries);
Console.WriteLine($"Upserted {entries.Length} records.");

// --- Get a record by key ---
var record = await collection.GetAsync("2");
if (record is not null)
{
    Console.WriteLine($"Got record: Key={record.Key}, Term={record.Term}, Definition={record.Definition}");
}

// --- Delete a record ---
await collection.DeleteAsync("3");
Console.WriteLine("Deleted record with Key='3'.");

// --- Verify deletion ---
var deleted = await collection.GetAsync("3");
Console.WriteLine($"Record '3' after deletion: {(deleted is null ? "not found (deleted)" : "still exists")}");

// --- Search by vector ---
var searchVector = new ReadOnlyMemory<float>([0.1f, 0.2f, 0.3f, 0.4f]);
Console.WriteLine("\nVector search results:");
await foreach (var result in collection.SearchAsync(searchVector, top: 5))
{
    Console.WriteLine($"  Key={result.Record.Key}, Term={result.Record.Term}, Score={result.Score:F4}");
}

// --- Clean up ---
await collection.EnsureCollectionDeletedAsync();
Console.WriteLine("\nCollection deleted. Done!");

// --- Record type ---
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
