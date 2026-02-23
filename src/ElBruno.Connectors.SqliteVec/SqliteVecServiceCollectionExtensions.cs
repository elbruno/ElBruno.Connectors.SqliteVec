using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;

namespace ElBruno.Connectors.SqliteVec;

/// <summary>
/// Extension methods for registering <see cref="SqliteVecVectorStoreCollection{TKey, TRecord}"/> with dependency injection.
/// </summary>
public static class SqliteVecServiceCollectionExtensions
{
    /// <summary>
    /// Registers a <see cref="VectorStoreCollection{TKey, TRecord}"/> backed by sqlite-vec as a singleton.
    /// </summary>
    /// <typeparam name="TKey">The type of the record key.</typeparam>
    /// <typeparam name="TRecord">The type of the record.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="collectionName">The name of the collection (table name).</param>
    /// <param name="connectionString">The SQLite connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqliteVecCollection<TKey, TRecord>(
        this IServiceCollection services,
        string collectionName,
        string connectionString)
        where TKey : notnull
        where TRecord : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<VectorStoreCollection<TKey, TRecord>>(sp =>
        {
            var embeddingGenerator = sp.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
            return new SqliteVecVectorStoreCollection<TKey, TRecord>(
                collectionName,
                connectionString,
                embeddingGenerator);
        });

        return services;
    }
}
