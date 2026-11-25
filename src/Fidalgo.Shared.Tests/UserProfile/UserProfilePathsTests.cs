using Fidalgo.Shared.UserProfile;

namespace Fidalgo.Shared.Tests.UserProfile;

public class UserProfilePathsTests
{
    [Fact]
    public void GetDefaultBaseDir_EndsWithDotConfigFidalgo()
    {
        var dir = UserProfilePaths.GetDefaultBaseDir();

        Assert.EndsWith(Path.Combine(".config", "Fidalgo"), dir);
    }

    [Fact]
    public void GetDefaultDocumentsDir_EndsWithFidalgo()
    {
        var dir = UserProfilePaths.GetDefaultDocumentsDir();

        Assert.EndsWith("Fidalgo", dir);
        Assert.NotEqual(UserProfilePaths.GetDefaultBaseDir(), dir);
    }

    [Fact]
    public void GetDefaultApplicationsDir_EndsWithFidalgoApplications()
    {
        var dir = UserProfilePaths.GetDefaultApplicationsDir();

        Assert.EndsWith(Path.Combine("Fidalgo", UserProfilePaths.ApplicationsDirectoryName), dir);
    }

    [Fact]
    public void ResolveDocumentsDir_UsesMyDocuments_WhenPresent()
    {
        var result = UserProfilePaths.ResolveDocumentsDir(@"C:\Users\alice\Documents", @"C:\Users\alice");

        Assert.Equal(Path.Combine(@"C:\Users\alice\Documents", "Fidalgo"), result);
    }

    [Fact]
    public void ResolveDocumentsDir_FallsBackToUserProfileDocuments_WhenMyDocumentsEmpty()
    {
        var result = UserProfilePaths.ResolveDocumentsDir(string.Empty, @"C:\Users\alice");

        Assert.Equal(Path.Combine(@"C:\Users\alice", "Documents", "Fidalgo"), result);
    }
}
