using Fidalgo.Shared;
using Xunit;

namespace Fidalgo.Shared.Tests;

public class ConstantsTests
{
    [Fact]
    public void GetDefaultDatabasePath_CombinesAppDataAndFidalgoSubfolder()
    {
        var path = Constants.GetDefaultDatabasePath();

        Assert.EndsWith(Path.Combine("fidalgo", "jobs.db"), path);
        Assert.True(Path.IsPathRooted(path));
    }

    [Fact]
    public void GetDefaultDatabasePath_CreatesDirectoryIfMissing()
    {
        var path = Constants.GetDefaultDatabasePath();

        var dir = Path.GetDirectoryName(path)!;
        Assert.True(Directory.Exists(dir), $"Expected directory to exist: {dir}");
    }
}
