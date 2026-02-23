using Microsoft.Extensions.VectorData;
using Xunit;

namespace ElBruno.Connectors.SqliteVec.Tests;

public class EndToEndTests
{
    private sealed class TestRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData]
        public string Name { get; set; } = string.Empty;

        [VectorStoreData]
        public int Score { get; set; }

        [VectorStoreVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    private const string ConnectionString = "Data Source=e2e_test.db";

    private static SqliteVecVectorStoreCollection<string, TestRecord> CreateCollection(string name = "e2e_items")
        => new(name, ConnectionString);

    [Fact]
    public async Task EndToEnd_FullLifecycle()
    {
        var collection = CreateCollection($"e2e_{Guid.NewGuid():N}");

        try
        {
            // 1. EnsureCollectionExistsAsync
            await collection.EnsureCollectionExistsAsync();

            // 2. CollectionExistsAsync
            Assert.True(await collection.CollectionExistsAsync());

            // 3. UpsertAsync — insert records
            var records = new[]
            {
                new TestRecord { Id = "r1", Name = "Alpha", Score = 10, Embedding = new float[] { 1, 0, 0, 0 } },
                new TestRecord { Id = "r2", Name = "Beta", Score = 20, Embedding = new float[] { 0, 1, 0, 0 } },
                new TestRecord { Id = "r3", Name = "Gamma", Score = 30, Embedding = new float[] { 0, 0, 1, 0 } },
                new TestRecord { Id = "r4", Name = "Delta", Score = 40, Embedding = new float[] { 0, 0, 0, 1 } },
            };

            foreach (var r in records)
                await collection.UpsertAsync(r);

            // 4. GetAsync(key) — retrieve by key
            var fetched = await collection.GetAsync("r2");
            Assert.NotNull(fetched);
            Assert.Equal("Beta", fetched!.Name);
            Assert.Equal(20, fetched.Score);

            // 5. GetAsync(filter, top) — filtered retrieval
            var filtered = new List<TestRecord>();
            await foreach (var item in collection.GetAsync(r => r.Score >= 30, top: 10))
                filtered.Add(item);
            Assert.Equal(2, filtered.Count);
            Assert.All(filtered, r => Assert.True(r.Score >= 30));

            // 6. SearchAsync — vector similarity search
            var searchResults = new List<VectorSearchResult<TestRecord>>();
            await foreach (var result in collection.SearchAsync(new float[] { 1, 0, 0, 0 }, top: 4))
                searchResults.Add(result);

            Assert.Equal(4, searchResults.Count);
            // First result should be r1 (exact match)
            Assert.Equal("r1", searchResults[0].Record.Id);
            // Scores should be descending (closer = higher score)
            for (int i = 0; i < searchResults.Count - 1; i++)
                Assert.True(searchResults[i].Score >= searchResults[i + 1].Score);

            // 7. DeleteAsync — delete a record
            await collection.DeleteAsync("r3");
            Assert.Null(await collection.GetAsync("r3"));

            // 8. UpsertAsync batch
            var batch = new[]
            {
                new TestRecord { Id = "r5", Name = "Epsilon", Score = 50, Embedding = new float[] { 0.5f, 0.5f, 0, 0 } },
                new TestRecord { Id = "r6", Name = "Zeta", Score = 60, Embedding = new float[] { 0, 0, 0.5f, 0.5f } },
            };
            await collection.UpsertAsync(batch);

            Assert.NotNull(await collection.GetAsync("r5"));
            Assert.NotNull(await collection.GetAsync("r6"));

            // 9. EnsureCollectionDeletedAsync
            await collection.EnsureCollectionDeletedAsync();
            Assert.False(await collection.CollectionExistsAsync());
        }
        finally
        {
            // Cleanup
            try { await collection.EnsureCollectionDeletedAsync(); } catch { }
        }
    }
}
