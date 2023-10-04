namespace Burntime.Platform;

public class Log
{
    static StreamWriter file;
    public static bool DebugOut;

    public static string FormatPercentage(float factor) => System.Math.Round(factor * 100) + "%";
    public static string FormatPercentage(Vector2f factor) => System.Math.Round(factor.x * 100) + "% x " + System.Math.Round(factor.y * 100) + "%";

    static public void Initialize(String file)
    {
        Log.file = new StreamWriter(file, false);
    }

    static public void Info(String str)
    {
        if (file != null)
        {
            file.WriteLine("[info] " + str);
            file.Flush();
        }
    }

    static public void Warning(String str)
    {
        if (file != null)
        {
            file.WriteLine("[warning] " + str);
            file.Flush();
        }
    }

    static public void Debug(String str)
    {
        if (file != null && DebugOut)
        {
            file.WriteLine("[debug] " + str);
            file.Flush();
        }
    }
}
