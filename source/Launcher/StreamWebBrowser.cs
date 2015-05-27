using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace Burntime.Launcher
{
    class StreamWebBrowser : WebBrowser
    {
        public void Navigate(Stream stream)
        {
            DocumentStream = stream;
        }
    }
}
