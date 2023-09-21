namespace Burntime.Platform;

public class Log
{
    static StreamWriter file;
    public static bool DebugOut;

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
