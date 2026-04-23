namespace CsLiveMute.Core.Models;

public sealed record AppSettings
{
    public const int DefaultPort = 3000;

    public bool Enabled { get; init; } = true;

    public int Port { get; init; } = DefaultPort;

    public string AuthToken { get; init; } = AuthTokenGenerator.Create();

    public MediaControlMode ControlMode { get; init; } = MediaControlMode.StreamMute;

    public static AppSettings CreateDefault()
    {
        return new AppSettings();
    }
}
