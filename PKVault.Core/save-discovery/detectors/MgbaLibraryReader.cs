using Microsoft.Data.Sqlite;
using Serilog;

namespace PKVault.Core.SaveDiscovery;

/// <summary>
/// Reads ROM paths from mGBA "library.sqlite3" (tables roots + paths).
/// </summary>
public interface IMgbaLibraryReader
{
    /// <summary>
    /// Returns absolute ROM paths and library root folders, or empty on any error.
    /// </summary>
    public IReadOnlyList<string> ReadRomPaths(string libraryPath);
}

/// <summary>
/// Uses Microsoft.Data.Sqlite (already shipped with EF Core Sqlite), opened read-only on the real disk.
/// </summary>
public class MgbaLibraryReader : IMgbaLibraryReader
{
    public IReadOnlyList<string> ReadRomPaths(string libraryPath)
    {
        if (!File.Exists(libraryPath))
            return [];

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = libraryPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            List<string> results = [];

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT path FROM roots";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                    if (!reader.IsDBNull(0))
                        results.Add(reader.GetString(0));
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT r.path, p.path FROM paths p LEFT JOIN roots r ON r.rootid = p.rootid";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(1))
                        continue;
                    var root = reader.IsDBNull(0) ? null : reader.GetString(0);
                    var path = reader.GetString(1);
                    var combined = root == null ? path : DiscoveryPath.Combine(root, path);
                    if (combined != null)
                        results.Add(combined);
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Save discovery: cannot read mGBA library {libraryPath}");
            return [];
        }
    }
}
