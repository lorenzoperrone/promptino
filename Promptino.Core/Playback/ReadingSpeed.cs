namespace Promptino.Core.Playback;

public static class ReadingSpeed
{
    public const int MinWpm = 20;
    public const int MaxWpm = 500;
    public const int DefaultWpm = 130;

    public static int Clamp(int wpm) => Math.Clamp(wpm, MinWpm, MaxWpm);

    public static double WordsPerSecond(int wpm) => Clamp(wpm) / 60d;
}
