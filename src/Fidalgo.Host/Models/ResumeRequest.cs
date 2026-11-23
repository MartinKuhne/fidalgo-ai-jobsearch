namespace Fidalgo.Host.Models;

/// <summary>
/// Request payload for the legacy <c>POST /api/jobs/{id}/resume</c> endpoint.
/// Falls back to the server's configured <c>resume.md</c> when <see cref="BaseResume"/> is omitted.
/// </summary>
public sealed record ResumeRequest(string? BaseResume = null);
