namespace Burntime.Platform;

public class Log
{
    public static StreamWriter? File { get; private set; }
    public static bool DebugOut;

    public static string FormatPercentage(float factor) => System.Math.Round(factor * 100) + "%";
    public static string FormatPercentage(Vector2f factor) => System.Math.Round(factor.x * 100) + "% x " + System.Math.Round(factor.y * 100) + "%";

    static public void Initialize(String file)
    {
        Log.File = new StreamWriter(file, false);
    }

    static public void Info(String str)
    {
        if (File != null)
        {
            File.WriteLine("[info] " + str);
            File.Flush();
        }
    }

    static public void Warning(String str)
    {
        if (File != null)
        {
            File.WriteLine("[warning] " + str);
            File.Flush();
        }
    }

    static public void Debug(String str)
    {
        if (File != null && DebugOut)
        {
            File.WriteLine("[debug] " + str);
            File.Flush();
        }
    }
}
