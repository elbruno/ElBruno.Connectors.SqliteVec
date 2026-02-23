using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace ElBruno.Connectors.SqliteVec;

/// <summary>
/// A <see cref="VectorStoreCollection{TKey, TRecord}"/> implementation backed by SQLite with the sqlite-vec extension.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
/// <typeparam name="TRecord">The type of the record.</typeparam>
public sealed class SqliteVecVectorStoreCollection<TKey, TRecord> : VectorStoreCollection<TKey, TRecord>
    where TKey : notnull
    where TRecord : class, new()
{
    private readonly string _connectionString;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private readonly RecordMapper<TKey, TRecord> _mapper;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteVecVectorStoreCollection{TKey, TRecord}"/>.
    /// </summary>
    /// <param name="name">The collection name (used as the table name).</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <param name="embeddingGenerator">Optional embedding generator for string-typed vector properties.</param>
    public SqliteVecVectorStoreCollection(
        string name,
        string connectionString,
        IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Collection name cannot be null or whitespace.", nameof(name));

        Name = name;
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _embeddingGenerator = embeddingGenerator;
        _mapper = RecordMapper<TKey, TRecord>.GetOrCreate();
    }

    /// <inheritdoc />
    public override string Name { get; }

    /// <inheritdoc />
    public override async Task<bool> CollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        cmd.Parameters.AddWithValue("@name", Name);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        // Create main table
        var dataColumns = _mapper.Columns;
        var keyCol = _mapper.KeyColumn;
        var columnDefs = new List<string> { $"\"{keyCol.ColumnName}\" {keyCol.SqliteType} PRIMARY KEY" };

        foreach (var col in dataColumns.Where(c => !c.IsKey))
        {
            columnDefs.Add($"\"{col.ColumnName}\" {col.SqliteType}");
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"CREATE TABLE IF NOT EXISTS \"{Name}\" ({string.Join(", ", columnDefs)})";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Create indexes for indexed columns
        foreach (var col in dataColumns.Where(c => c.IsIndexed))
        {
            await using var indexCmd = connection.CreateCommand();
            indexCmd.CommandText = $"CREATE INDEX IF NOT EXISTS \"idx_{Name}_{col.ColumnName}\" ON \"{Name}\" (\"{col.ColumnName}\")";
            await indexCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Create vec virtual table if there's a vector column
        if (_mapper.VectorColumn is { } vecCol)
        {
            await using var vecCmd = connection.CreateCommand();
            vecCmd.CommandText = $"CREATE VIRTUAL TABLE IF NOT EXISTS \"vec_{Name}\" USING vec0({keyCol.ColumnName} TEXT, embedding float[{vecCol.VectorDimensions}])";
            await vecCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public override async Task EnsureCollectionDeletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        if (_mapper.VectorColumn is not null)
        {
            await using var vecCmd = connection.CreateCommand();
            vecCmd.CommandText = $"DROP TABLE IF EXISTS \"vec_{Name}\"";
            await vecCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP TABLE IF EXISTS \"{Name}\"";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<TRecord?> GetAsync(
        TKey key,
        RecordRetrievalOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        var keyCol = _mapper.KeyColumn;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{Name}\" WHERE \"{keyCol.ColumnName}\" = @key";
        cmd.Parameters.AddWithValue("@key", key);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadRecord(reader);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<TRecord> GetAsync(
        Expression<Func<TRecord, bool>> filter,
        int top,
        FilteredRecordRetrievalOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (top < 1)
            throw new ArgumentOutOfRangeException(nameof(top), "top must be greater than zero.");

        var predicate = filter.Compile();
        var skip = options?.Skip ?? 0;

        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{Name}\"";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<TRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var record = ReadRecord(reader);
            if (predicate(record))
                results.Add(record);
        }

        foreach (var record in results.Skip(skip).Take(top))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        var keyCol = _mapper.KeyColumn;

        if (_mapper.VectorColumn is not null)
        {
            await using var vecCmd = connection.CreateCommand();
            vecCmd.CommandText = $"DELETE FROM \"vec_{Name}\" WHERE \"{keyCol.ColumnName}\" = @key";
            vecCmd.Parameters.AddWithValue("@key", key);
            await vecCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM \"{Name}\" WHERE \"{keyCol.ColumnName}\" = @key";
        cmd.Parameters.AddWithValue("@key", key);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(TRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        await UpsertRecordAsync(connection, record, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpsertAsync(IEnumerable<TRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UpsertRecordAsync(connection, record, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<VectorSearchResult<TRecord>> SearchAsync<TInput>(
        TInput searchValue,
        int top,
        VectorSearchOptions<TRecord>? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (top < 1)
            throw new ArgumentOutOfRangeException(nameof(top), "top must be greater than zero.");

        if (_mapper.VectorColumn is null)
            throw new InvalidOperationException("Record type does not have a [VectorStoreVector] property.");

        var queryVector = await ResolveSearchVectorAsync(searchValue, cancellationToken);
        var predicate = options?.Filter?.Compile();
        var skip = options?.Skip ?? 0;

        await using var connection = await CreateAndOpenConnectionAsync(cancellationToken);

        var keyCol = _mapper.KeyColumn;

        // Query vec table for nearest neighbors (fetch extra to account for filtering)
        var fetchCount = top + skip + (predicate is not null ? top * 2 : 0);
        await using var vecCmd = connection.CreateCommand();
        vecCmd.CommandText = $"SELECT \"{keyCol.ColumnName}\", distance FROM \"vec_{Name}\" WHERE embedding MATCH @query ORDER BY distance LIMIT @limit";
        vecCmd.Parameters.AddWithValue("@query", SerializeVector(queryVector));
        vecCmd.Parameters.AddWithValue("@limit", fetchCount);

        var matches = new List<(string Key, double Distance)>();
        await using var vecReader = await vecCmd.ExecuteReaderAsync(cancellationToken);
        while (await vecReader.ReadAsync(cancellationToken))
        {
            matches.Add((vecReader.GetString(0), vecReader.GetDouble(1)));
        }

        // Join with main table
        var results = new List<VectorSearchResult<TRecord>>();
        foreach (var match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM \"{Name}\" WHERE \"{keyCol.ColumnName}\" = @key";
            cmd.Parameters.AddWithValue("@key", match.Key);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var record = ReadRecord(reader);
                if (predicate is null || predicate(record))
                {
                    // Convert distance to similarity score (1 / (1 + distance))
                    var score = 1.0 / (1.0 + match.Distance);
                    results.Add(new VectorSearchResult<TRecord>(record, score));
                }
            }
        }

        foreach (var result in results.Skip(skip).Take(top))
        {
            yield return result;
        }
    }

    /// <inheritdoc />
    public override object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(VectorStoreCollection<TKey, TRecord>) || serviceType == GetType() ? this : null;
    }

    private async Task<SqliteConnection> CreateAndOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        SqliteVecExtensions.LoadVecExtension(connection);
        return connection;
    }

    private async Task UpsertRecordAsync(SqliteConnection connection, TRecord record, CancellationToken cancellationToken)
    {
        var keyCol = _mapper.KeyColumn;
        var dataCols = _mapper.Columns.Where(c => !c.IsKey).ToList();

        // Build INSERT OR REPLACE for main table
        var allCols = new List<string> { $"\"{keyCol.ColumnName}\"" };
        var allParams = new List<string> { "@p_key" };

        foreach (var col in dataCols)
        {
            allCols.Add($"\"{col.ColumnName}\"");
            allParams.Add($"@p_{col.ColumnName}");
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"INSERT OR REPLACE INTO \"{Name}\" ({string.Join(", ", allCols)}) VALUES ({string.Join(", ", allParams)})";
        cmd.Parameters.AddWithValue("@p_key", _mapper.GetKey(record)!);

        foreach (var col in dataCols)
        {
            var value = col.Property.GetValue(record) ?? DBNull.Value;
            cmd.Parameters.AddWithValue($"@p_{col.ColumnName}", value);
        }

        await cmd.ExecuteNonQueryAsync(cancellationToken);

        // Upsert vector if applicable
        if (_mapper.VectorColumn is not null)
        {
            var vector = await ResolveRecordVectorAsync(record, cancellationToken);
            if (vector is not null)
            {
                var keyValue = _mapper.GetKey(record);

                // Delete existing vec entry
                await using var delCmd = connection.CreateCommand();
                delCmd.CommandText = $"DELETE FROM \"vec_{Name}\" WHERE \"{keyCol.ColumnName}\" = @key";
                delCmd.Parameters.AddWithValue("@key", keyValue!);
                await delCmd.ExecuteNonQueryAsync(cancellationToken);

                // Insert new vec entry
                await using var vecCmd = connection.CreateCommand();
                vecCmd.CommandText = $"INSERT INTO \"vec_{Name}\" (\"{keyCol.ColumnName}\", embedding) VALUES (@key, @embedding)";
                vecCmd.Parameters.AddWithValue("@key", keyValue!);
                vecCmd.Parameters.AddWithValue("@embedding", SerializeVector(vector.Value));
                await vecCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    private async Task<ReadOnlyMemory<float>?> ResolveRecordVectorAsync(TRecord record, CancellationToken cancellationToken)
    {
        // If vector property is a direct float vector
        var directVector = _mapper.GetVectorValue(record);
        if (directVector is not null)
            return directVector;

        // If vector property is a string, embed it
        var stringValue = _mapper.GetStringVectorValue(record);
        if (stringValue is not null)
        {
            if (_embeddingGenerator is null)
                throw new InvalidOperationException(
                    "An IEmbeddingGenerator is required to embed string vector properties. " +
                    "Provide one in the constructor.");

            var embedding = await _embeddingGenerator.GenerateAsync(
                [stringValue], cancellationToken: cancellationToken);
            return embedding[0].Vector;
        }

        return null;
    }

    private async Task<ReadOnlyMemory<float>> ResolveSearchVectorAsync<TInput>(TInput searchValue, CancellationToken cancellationToken)
    {
        return searchValue switch
        {
            Embedding<float> embedding => embedding.Vector,
            ReadOnlyMemory<float> vector => vector,
            float[] vectorArray => vectorArray,
            string text => await EmbedStringAsync(text, cancellationToken),
            _ => throw new NotSupportedException(
                $"Search value type '{typeof(TInput).FullName}' is not supported. " +
                "Supported types are string, Embedding<float>, ReadOnlyMemory<float>, and float[].")
        };
    }

    private async Task<ReadOnlyMemory<float>> EmbedStringAsync(string text, CancellationToken cancellationToken)
    {
        if (_embeddingGenerator is null)
            throw new InvalidOperationException(
                "An IEmbeddingGenerator is required for string-based search. Provide one in the constructor.");

        var embeddings = await _embeddingGenerator.GenerateAsync([text], cancellationToken: cancellationToken);
        return embeddings[0].Vector;
    }

    private TRecord ReadRecord(SqliteDataReader reader)
    {
        var record = new TRecord();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            if (!reader.IsDBNull(i))
            {
                var value = reader.GetValue(i);
                _mapper.SetPropertyValue(record, columnName, value);
            }
        }
        return record;
    }

    private static byte[] SerializeVector(ReadOnlyMemory<float> vector)
    {
        var span = vector.Span;
        var bytes = new byte[span.Length * sizeof(float)];
        Buffer.BlockCopy(span.ToArray(), 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
