namespace Fidalgo.Shared;

/// <summary>
/// Shared constants for the Fidalgo application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Returns the default path for the SQLite database file.
    /// Uses %APPDATA%\fidalgo\jobs.db on Windows.
    /// </summary>
    public static string GetDefaultDatabasePath()
    {
        var dataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(dataPath, "fidalgo");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "jobs.db");
    }
}