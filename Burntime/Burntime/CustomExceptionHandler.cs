
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace Burntime
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
                writer = new StreamWriter("errorlog.txt", true);
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
