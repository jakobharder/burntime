namespace Burntime.Platform;

public class Log
{
    static readonly object sync = new();
    static string? filePath;
    public static bool DebugOut;

    public static string FormatPercentage(float factor) => System.Math.Round(factor * 100) + "%";
    public static string FormatPercentage(Vector2f factor) => System.Math.Round(factor.x * 100) + "% x " + System.Math.Round(factor.y * 100) + "%";

    static public void Initialize(String file, int retainedSessionCount = 1)
    {
        lock (sync)
        {
            filePath = file;

            try
            {
                Rotate(file, Math.Max(1, retainedSessionCount));
                using StreamWriter writer = new(file, false);
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException)
            {
                // Diagnostics must never prevent the application from starting.
            }
        }
    }

    static void Rotate(string file, int retainedSessionCount)
    {
        if (retainedSessionCount <= 1)
            return;

        for (int index = retainedSessionCount - 1; index >= 2; --index)
        {
            string previous = ArchivePath(file, index - 1);
            if (System.IO.File.Exists(previous))
                System.IO.File.Move(previous, ArchivePath(file, index), true);
        }

        if (System.IO.File.Exists(file))
            System.IO.File.Move(file, ArchivePath(file, 1), true);
    }

    static string ArchivePath(string file, int index)
    {
        string? directory = Path.GetDirectoryName(file);
        string name = Path.GetFileNameWithoutExtension(file);
        string extension = Path.GetExtension(file);
        return Path.Combine(directory ?? string.Empty, $"{name}.{index}{extension}");
    }

    /// <summary>
    /// Writes directly to the current log without retaining a writable file
    /// handle between messages. This is required on iOS, which terminates a
    /// suspended application that retains file locks.
    /// </summary>
    public static void Write(Action<StreamWriter> write)
    {
        lock (sync)
        {
            if (filePath is null)
                return;

            try
            {
                using StreamWriter writer = new(filePath, true);
                write(writer);
            }
            catch (Exception exception) when (exception is IOException or
                UnauthorizedAccessException)
            {
                // Diagnostics must never crash the application.
            }
        }
    }

    static public void Info(String str)
    {
        Write(writer => writer.WriteLine("[info] " + str));
    }

    static public void Warning(String str)
    {
        Write(writer => writer.WriteLine("[warning] " + str));
    }

    static public void Debug(String str)
    {
        if (DebugOut)
            Write(writer => writer.WriteLine("[debug] " + str));
    }
}
