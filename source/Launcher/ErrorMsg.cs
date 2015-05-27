using System;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace Burntime.Launcher
{
    public class CustomExceptionHandler
    {
        public void OnThreadException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = args.ExceptionObject as Exception;
            if (e == null)
            {
                MessageBox.Show("Error: Unknown", "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
            else
            {
                ErrorMsg.LogException(e);

                MessageBox.Show("Error: " + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }

            // close
            Environment.Exit(1);
        }
    };

    static class ErrorMsg
    {
        public static readonly string MsgCorruptedFiles = "Engine files are corrupt!\n\nPlease redownload.";

        public static void ShowError(string msg)
        {
            MessageBox.Show("Error: " + msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void ShowError(string msg, Exception e)
        {
            MessageBox.Show("Error: " + msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            LogException(e);
        }

        // add exception to errorlog.txt
        public static void LogException(Exception e)
        {
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter("system/errorlog.txt", true);
                writer.WriteLine();
                writer.WriteLine("----------------------------------------------------------------------");
                writer.WriteLine(System.DateTime.Now.ToLocalTime().ToString());
                writer.WriteLine("exception: " + e.Message);
                writer.WriteLine("trace:");
                writer.Write(e.StackTrace);
                writer.WriteLine();

                writer.Flush();
                writer.Close();
            }
            catch (Exception e2)
            {
                MessageBox.Show(e2.Message);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
    }
}
