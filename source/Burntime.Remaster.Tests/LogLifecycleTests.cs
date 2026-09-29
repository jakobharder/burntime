using System;
using System.Collections.Generic;
using System.IO;
using Burntime.Platform;

namespace Burntime.Remaster.Tests;

using static Program;

static class LogLifecycleTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("each message releases the writable log handle", 0, () =>
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"burntime-log-{Guid.NewGuid():N}.txt");
            try
            {
                Log.Initialize(path);
                Log.Info("first message");

                // An exclusive open succeeds only when the logger retained no
                // sharing lock after writing the previous message.
                using (FileStream stream = new(path, FileMode.Open,
                    FileAccess.ReadWrite, FileShare.None))
                {
                    Equal(true, stream.CanWrite,
                        "log file is exclusively available between messages");
                }

                Log.Info("second message");

                string[] lines = File.ReadAllLines(path);
                Equal(2, lines.Length, "each message is appended once");
                Equal("[info] first message", lines[0],
                    "initial message is preserved");
                Equal("[info] second message", lines[1],
                    "later messages append instead of truncating");
                return 0;
            }
            finally
            {
                File.Delete(path);
            }
        });

        yield return Int("only the latest ten sessions are retained", 0, () =>
        {
            string directory = Path.Combine(Path.GetTempPath(),
                $"burntime-log-rotation-{Guid.NewGuid():N}");
            string path = Path.Combine(directory, "log.txt");
            Directory.CreateDirectory(directory);

            try
            {
                for (int session = 1; session <= 12; ++session)
                {
                    Log.Initialize(path, 10);
                    Log.Info($"session {session}");
                }

                Equal("[info] session 12", File.ReadAllText(path).Trim(),
                    "current session stays in log.txt");
                Equal("[info] session 11",
                    File.ReadAllText(Path.Combine(directory, "log.1.txt")).Trim(),
                    "most recent previous session is the first archive");
                Equal("[info] session 3",
                    File.ReadAllText(Path.Combine(directory, "log.9.txt")).Trim(),
                    "oldest retained session is the ninth archive");
                Equal(false, File.Exists(Path.Combine(directory, "log.10.txt")),
                    "an eleventh log file is not retained");
                Equal(10, Directory.GetFiles(directory, "log*.txt").Length,
                    "rotation remains bounded to ten files");
                return 0;
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        });
    }
}
