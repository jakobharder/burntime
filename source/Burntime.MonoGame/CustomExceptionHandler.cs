﻿using System;
using System.IO;

namespace Burntime
{
    public class CustomExceptionHandler
    {
        public static void OnThreadException(object sender, UnhandledExceptionEventArgs args)
        {
            var e = args.ExceptionObject as Exception;
            if (e is not null)
                ErrorMsg.LogException(e);

            //MessageBox.Show("Error: " + e?.Message ?? "Unknown", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);

            // close
            Environment.Exit(1);
        }
    };

    static class ErrorMsg
    {
        //public static void ShowError(string msg)
        //{
        //    MessageBox.Show("Error: " + msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //}

        //public static void ShowError(string msg, Exception e)
        //{
        //    MessageBox.Show("Error: " + msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //    LogException(e);
        //}

        // add exception to errorlog.txt
        public static void LogException(Exception exception)
        {
            var log = (StreamWriter writer, Exception e) =>
            {
                writer.WriteLine();
                writer.WriteLine("----------------------------------------------------------------------");
                writer.WriteLine(System.DateTime.Now.ToLocalTime().ToString());
                writer.WriteLine("exception: " + e.Message);
                writer.WriteLine("trace:");
                writer.Write(e.StackTrace);
                writer.WriteLine();

                writer.Flush();
                writer.Close();
            };

            try
            {
                if (Platform.Log.File is not null)
                    log(Platform.Log.File, exception);
            }
            catch { }

            using StreamWriter writer = new("crash.txt", true);
            log(writer, exception);

#warning TODO message box?
        }
    }
}
