using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

using System.IO;

namespace Burntime.Launcher
{
    public class WebClientEx : WebClient
    {
        private int timeout;

        public int TimeOut
        {
            get { return timeout; }
            set { timeout = value; }
        }

        public WebClientEx()
        {
            timeout = -1;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);

            if (request.GetType() == typeof(HttpWebRequest))
            {
                ((HttpWebRequest)request).Timeout = timeout;
            }

            return request;
        }
    }
    
    class HttpHelper
    {
        public Stream DownloadFileToMemory(string url)
        {
            MemoryStream stream = null;

            try
            {
                // simply download data and make memory stream
                using (WebClientEx client = new WebClientEx())
                {
                    // set time out to 10 seconds
                    client.TimeOut = 10 * 1000;

                    byte[] data = client.DownloadData(url);
                    stream = new MemoryStream(data, 0, data.Length);
                }
            }
            catch
            {
                // propably time out
                return null;
            }

            return stream;
        }
    }
}
