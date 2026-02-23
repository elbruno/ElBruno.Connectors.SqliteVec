using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.VectorData;

namespace ElBruno.Connectors.SqliteVec;

/// <summary>
/// Describes a column mapping derived from a record property's VectorData attributes.
/// </summary>
internal sealed class ColumnMapping
{
    /// <summary>Gets the property info for this column.</summary>
    public required PropertyInfo Property { get; init; }

    /// <summary>Gets the column name (property name or StorageName override).</summary>
    public required string ColumnName { get; init; }

    /// <summary>Gets the SQLite column type.</summary>
    public required string SqliteType { get; init; }

    /// <summary>Gets whether this column is the key column.</summary>
    public bool IsKey { get; init; }

    /// <summary>Gets whether this column is indexed.</summary>
    public bool IsIndexed { get; init; }

    /// <summary>Gets whether this is a vector column.</summary>
    public bool IsVector { get; init; }

    /// <summary>Gets the vector dimensions (only for vector columns).</summary>
    public int VectorDimensions { get; init; }

    /// <summary>Gets the distance function (only for vector columns).</summary>
    public string? DistanceFunction { get; init; }

    /// <summary>Gets whether the vector property is a string type (needs auto-embedding).</summary>
    public bool IsStringVector { get; init; }
}

/// <summary>
/// Discovers record schema from VectorData attributes via reflection and builds column mappings.
/// </summary>
/// <typeparam name="TKey">The type of the record key.</typeparam>
/// <typeparam name="TRecord">The type of the record.</typeparam>
internal sealed class RecordMapper<TKey, TRecord>
    where TKey : notnull
    where TRecord : class
{
    private static readonly ConcurrentDictionary<Type, RecordMapper<TKey, TRecord>> Cache = new();

    private readonly PropertyInfo _keyProperty;
    private readonly ColumnMapping? _vectorMapping;

    private RecordMapper(
        PropertyInfo keyProperty,
        ColumnMapping? vectorMapping,
        IReadOnlyList<ColumnMapping> columns)
    {
        _keyProperty = keyProperty;
        _vectorMapping = vectorMapping;
        Columns = columns;
    }

    /// <summary>Gets all column mappings for the record type.</summary>
    public IReadOnlyList<ColumnMapping> Columns { get; }

    /// <summary>Gets the key column mapping.</summary>
    public ColumnMapping KeyColumn => Columns.First(c => c.IsKey);

    /// <summary>Gets the vector column mapping, if any.</summary>
    public ColumnMapping? VectorColumn => _vectorMapping;

    /// <summary>Gets or creates a cached mapper for the record type.</summary>
    public static RecordMapper<TKey, TRecord> GetOrCreate()
    {
        return Cache.GetOrAdd(typeof(TRecord), static _ => Create());
    }

    /// <summary>Gets the key value from a record.</summary>
    public TKey GetKey(TRecord record)
    {
        var value = _keyProperty.GetValue(record);
        if (value is not TKey key)
            throw new InvalidOperationException(
                $"Key property '{_keyProperty.Name}' on '{typeof(TRecord).FullName}' must be assignable to '{typeof(TKey).FullName}'.");
        return key;
    }

    /// <summary>Gets the vector value from a record as ReadOnlyMemory&lt;float&gt;, or null if string-based.</summary>
    public ReadOnlyMemory<float>? GetVectorValue(TRecord record)
    {
        if (_vectorMapping is null) return null;

        if (_vectorMapping.IsStringVector) return null;

        var value = _vectorMapping.Property.GetValue(record);
        return value switch
        {
            ReadOnlyMemory<float> rom => rom,
            float[] arr => arr,
            null => null,
            _ => throw new InvalidOperationException(
                $"Vector property '{_vectorMapping.Property.Name}' has unsupported type '{value.GetType()}'.")
        };
    }

    /// <summary>Gets the string value to embed from a string-typed vector property.</summary>
    public string? GetStringVectorValue(TRecord record)
    {
        if (_vectorMapping is null || !_vectorMapping.IsStringVector) return null;
        return _vectorMapping.Property.GetValue(record) as string;
    }

    /// <summary>Gets a data property value by column name.</summary>
    public object? GetPropertyValue(TRecord record, string columnName)
    {
        var col = Columns.FirstOrDefault(c => c.ColumnName == columnName);
        return col?.Property.GetValue(record);
    }

    /// <summary>Sets a property value on a record by column name.</summary>
    public void SetPropertyValue(TRecord record, string columnName, object? value)
    {
        var col = Columns.FirstOrDefault(c => c.ColumnName == columnName);
        if (col is null) return;

        var targetType = Nullable.GetUnderlyingType(col.Property.PropertyType) ?? col.Property.PropertyType;

        if (value is null || value == DBNull.Value)
        {
            if (col.Property.CanWrite)
                col.Property.SetValue(record, null);
            return;
        }

        if (col.Property.CanWrite)
        {
            if (targetType == typeof(ReadOnlyMemory<float>) && value is float[] arr)
                col.Property.SetValue(record, new ReadOnlyMemory<float>(arr));
            else
                col.Property.SetValue(record, Convert.ChangeType(value, targetType));
        }
    }

    private static RecordMapper<TKey, TRecord> Create()
    {
        var properties = typeof(TRecord).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var columns = new List<ColumnMapping>();
        PropertyInfo? keyProperty = null;
        ColumnMapping? vectorMapping = null;

        foreach (var prop in properties)
        {
            var keyAttr = prop.GetCustomAttribute<VectorStoreKeyAttribute>();
            var dataAttr = prop.GetCustomAttribute<VectorStoreDataAttribute>();
            var vectorAttr = prop.GetCustomAttribute<VectorStoreVectorAttribute>();

            if (keyAttr is not null)
            {
                if (keyProperty is not null)
                    throw new InvalidOperationException($"Record type '{typeof(TRecord).FullName}' has multiple [VectorStoreKey] properties.");

                keyProperty = prop;
                columns.Add(new ColumnMapping
                {
                    Property = prop,
                    ColumnName = keyAttr.StorageName ?? prop.Name,
                    SqliteType = MapClrTypeToSqlite(prop.PropertyType),
                    IsKey = true
                });
            }
            else if (vectorAttr is not null)
            {
                var isString = prop.PropertyType == typeof(string);
                var mapping = new ColumnMapping
                {
                    Property = prop,
                    ColumnName = vectorAttr.StorageName ?? prop.Name,
                    SqliteType = "BLOB",
                    IsVector = true,
                    VectorDimensions = vectorAttr.Dimensions,
                    DistanceFunction = vectorAttr.DistanceFunction,
                    IsStringVector = isString
                };
                vectorMapping = mapping;
                // Vector columns go in the vec virtual table, not the main table
            }
            else if (dataAttr is not null)
            {
                columns.Add(new ColumnMapping
                {
                    Property = prop,
                    ColumnName = dataAttr.StorageName ?? prop.Name,
                    SqliteType = MapClrTypeToSqlite(prop.PropertyType),
                    IsIndexed = dataAttr.IsIndexed
                });
            }
        }

        if (keyProperty is null)
            throw new InvalidOperationException($"Record type '{typeof(TRecord).FullName}' must have a [VectorStoreKey] property.");

        if (!typeof(TKey).IsAssignableFrom(keyProperty.PropertyType))
            throw new InvalidOperationException(
                $"[VectorStoreKey] property '{keyProperty.Name}' must be assignable to key type '{typeof(TKey).FullName}'.");

        return new RecordMapper<TKey, TRecord>(keyProperty, vectorMapping, columns);
    }

    private static string MapClrTypeToSqlite(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string)) return "TEXT";
        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(bool)) return "INTEGER";
        if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal)) return "REAL";
        if (underlying == typeof(byte[])) return "BLOB";
        if (underlying == typeof(Guid)) return "TEXT";
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return "TEXT";
        return "TEXT";
    }
}
