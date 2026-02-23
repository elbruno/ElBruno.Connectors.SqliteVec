using Microsoft.Extensions.VectorData;
using Xunit;

namespace ElBruno.Connectors.SqliteVec.Tests;

public class RecordMapperTests
{
    private sealed class TestRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData]
        public string Name { get; set; } = string.Empty;

        [VectorStoreData(IsIndexed = true)]
        public int Age { get; set; }

        [VectorStoreVector(128)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    private sealed class StringVectorRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreVector(128)]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class NoKeyRecord
    {
        [VectorStoreData]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void GetOrCreate_DiscoversColumns()
    {
        var mapper = RecordMapper<string, TestRecord>.GetOrCreate();

        Assert.Equal(3, mapper.Columns.Count);
        Assert.Contains(mapper.Columns, c => c.ColumnName == "Id" && c.IsKey);
        Assert.Contains(mapper.Columns, c => c.ColumnName == "Name" && !c.IsKey);
        Assert.Contains(mapper.Columns, c => c.ColumnName == "Age" && c.IsIndexed);
    }

    [Fact]
    public void GetOrCreate_DiscoversVectorColumn()
    {
        var mapper = RecordMapper<string, TestRecord>.GetOrCreate();

        Assert.NotNull(mapper.VectorColumn);
        Assert.Equal("Embedding", mapper.VectorColumn!.ColumnName);
        Assert.Equal(128, mapper.VectorColumn.VectorDimensions);
        Assert.False(mapper.VectorColumn.IsStringVector);
    }

    [Fact]
    public void GetKey_ReturnsKeyValue()
    {
        var mapper = RecordMapper<string, TestRecord>.GetOrCreate();
        var record = new TestRecord { Id = "test-123" };

        Assert.Equal("test-123", mapper.GetKey(record));
    }

    [Fact]
    public void TypeMapping_StringMapsToText()
    {
        var mapper = RecordMapper<string, TestRecord>.GetOrCreate();
        var nameCol = mapper.Columns.First(c => c.ColumnName == "Name");

        Assert.Equal("TEXT", nameCol.SqliteType);
    }

    [Fact]
    public void TypeMapping_IntMapsToInteger()
    {
        var mapper = RecordMapper<string, TestRecord>.GetOrCreate();
        var ageCol = mapper.Columns.First(c => c.ColumnName == "Age");

        Assert.Equal("INTEGER", ageCol.SqliteType);
    }

    [Fact]
    public void MissingKey_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RecordMapper<string, NoKeyRecord>.GetOrCreate());
    }

    [Fact]
    public void StringVector_DetectedCorrectly()
    {
        var mapper = RecordMapper<string, StringVectorRecord>.GetOrCreate();

        Assert.NotNull(mapper.VectorColumn);
        Assert.True(mapper.VectorColumn!.IsStringVector);
    }

    [Fact]
    public void KeyColumn_ReturnsSameAsFirstKeyInColumns()
    {
        var mapper = RecordMapper<string, TestRecord>.GetOrCreate();

        Assert.Equal("Id", mapper.KeyColumn.ColumnName);
        Assert.True(mapper.KeyColumn.IsKey);
        Assert.Equal("TEXT", mapper.KeyColumn.SqliteType);
    }
}
