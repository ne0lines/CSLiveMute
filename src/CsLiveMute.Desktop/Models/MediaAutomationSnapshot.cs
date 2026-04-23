using CsLiveMute.Core.Models;

namespace CsLiveMute.Desktop.Models;

public sealed record MediaAutomationSnapshot(
    MediaControlMode Mode,
    bool SessionDetected,
    string? SourceApp,
    string? Title,
    bool IsPlaying,
    bool ChangeApplied,
    string? Action,
    string? Detail)
{
    public static MediaAutomationSnapshot CreateIdle(MediaControlMode mode, string detail)
    {
        return new MediaAutomationSnapshot(mode, false, null, null, false, false, null, detail);
    }
}
