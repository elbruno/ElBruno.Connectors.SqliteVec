using Microsoft.Data.Sqlite;

namespace ElBruno.Connectors.SqliteVec;

/// <summary>
/// Provides helpers for loading the sqlite-vec extension on a SQLite connection.
/// </summary>
public static class SqliteVecExtensions
{
    /// <summary>
    /// Loads the sqlite-vec extension on the given connection.
    /// This should be called after opening the connection.
    /// </summary>
    /// <param name="connection">The SQLite connection to load the extension on.</param>
    public static void LoadVecExtension(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.LoadExtension("vec0");
    }
}
