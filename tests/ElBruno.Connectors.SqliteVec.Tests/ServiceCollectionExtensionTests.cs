using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Xunit;

namespace ElBruno.Connectors.SqliteVec.Tests;

public class ServiceCollectionExtensionTests
{
    private sealed class TestRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void AddSqliteVecCollection_RegistersVectorStoreCollection()
    {
        var services = new ServiceCollection();
        services.AddSqliteVecCollection<string, TestRecord>("test_collection", "Data Source=:memory:");

        var provider = services.BuildServiceProvider();
        var collection = provider.GetService<VectorStoreCollection<string, TestRecord>>();

        Assert.NotNull(collection);
        Assert.IsType<SqliteVecVectorStoreCollection<string, TestRecord>>(collection);
        Assert.Equal("test_collection", collection.Name);
    }
}
