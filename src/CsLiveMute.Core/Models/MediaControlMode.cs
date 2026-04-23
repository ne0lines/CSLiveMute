namespace CsLiveMute.Core.Models;

public enum MediaControlMode
{
    StreamMute = 0,
    VideoPause = 1
}

public static class MediaControlModeExtensions
{
    public static string ToDisplayName(this MediaControlMode mode)
    {
        return mode switch
        {
            MediaControlMode.StreamMute => "Stream",
            MediaControlMode.VideoPause => "Video",
            _ => "Unknown"
        };
    }

    public static string ToDescription(this MediaControlMode mode)
    {
        return mode switch
        {
            MediaControlMode.StreamMute => "Mute browser audio during live rounds.",
            MediaControlMode.VideoPause => "Pause the current browser media during live rounds.",
            _ => string.Empty
        };
    }
}
