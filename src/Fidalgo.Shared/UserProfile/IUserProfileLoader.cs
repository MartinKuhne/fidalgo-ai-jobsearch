using Fidalgo.Shared.Models;

namespace Fidalgo.Shared.UserProfile;

/// <summary>
/// Loads the candidate's Markdown profile files (resume, career story, career goals)
/// and packet output directory configuration.
/// </summary>
public interface IUserProfileLoader
{
    /// <summary>
    /// Loads the user profile, applying optional path or inline content overrides.
    /// </summary>
    /// <param name="overrides">Optional path and inline content overrides.</param>
    /// <param name="cancellationToken">Token for cancellation.</param>
    /// <returns>The loaded profile content and source metadata.</returns>
    Task<UserProfileContent> LoadAsync(
        UserProfileLoadOverrides? overrides = null,
        CancellationToken cancellationToken = default);
}
