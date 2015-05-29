using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.ComponentModel;

namespace Burntime.Launcher
{
    class BrowserTabPage : TabPage
    {
        protected WebBrowser browser;

        struct CompletedInfo
        {
            public HtmlWindow Window;
            public Uri Url;

            public CompletedInfo(HtmlWindow window, Uri url)
            {
                this.Window = window;
                this.Url = url;
            }
        }

        private BackgroundWorker pageBuilder;
        private Queue<CompletedInfo> pageBuilderQueue;

        public BrowserTabPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            browser = new System.Windows.Forms.WebBrowser();
            SuspendLayout();

            pageBuilderQueue = new Queue<CompletedInfo>();
            pageBuilder = new BackgroundWorker();
            pageBuilder.DoWork += new DoWorkEventHandler(pageBuilder_DoWork);

            browser.Dock = System.Windows.Forms.DockStyle.Fill;
            browser.IsWebBrowserContextMenuEnabled = false;
            browser.Location = new System.Drawing.Point(3, 3);
            browser.MinimumSize = new System.Drawing.Size(20, 20);
            browser.Name = "browser";
            //browser.ScrollBarsEnabled = false;
            browser.Size = new System.Drawing.Size(394, 240);
            browser.TabIndex = 0;
            browser.DocumentCompleted += new WebBrowserDocumentCompletedEventHandler(browser_DocumentCompleted);
            browser.Navigated += new WebBrowserNavigatedEventHandler(browser_Navigated);

            // TODO throws exception
            browser.AllowWebBrowserDrop = false;
            browser.ScrollBarsEnabled = false;

            Controls.Add(browser);

            ResumeLayout(false);
        }

        void browser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            string str = e.Url.ToString();
            return;
        }

        void browser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            
            CompletedInfo info = new CompletedInfo(browser.Document.Window, e.Url);
            ReplaceVariables(info.Window, info.Url);
            AttachClickHandler(info.Window, info.Url);
            //lock (pageBuilderQueue)
            //{
            //    // enqueue page to replace variables and button actions
            //    pageBuilderQueue.Enqueue(new CompletedInfo(browser.Document.Window, e.Url));

            //    // fire background worker if not running
            //    if (pageBuilderQueue.Count == 1)
            //        pageBuilder.RunWorkerAsync();
            //}
        }

        void  pageBuilder_DoWork(object sender, DoWorkEventArgs e)
        {
            //while (pageBuilderQueue.Count > 0)
            //{
            //    CompletedInfo info;
            //    lock (pageBuilderQueue)
            //        info = pageBuilderQueue.Peek();

            //    try
            //    {

            //        if (!info.Window.IsClosed)
            //        {
            //            ReplaceVariables(info.Window, info.Url);
            //            AttachClickHandler(info.Window, info.Url);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        // happens when window is already invalid
            //        int x = 0;
            //        x++;
            //    }

            //    lock (pageBuilderQueue)
            //        pageBuilderQueue.Dequeue();
            //}
        }

        void AttachClickHandler(HtmlWindow window, Uri url)
        {
            if (window.Document.Url == url)
            {
                // retrieve all buttons
                foreach (HtmlElement element in window.Document.GetElementsByTagName("button"))
                {
                    if (element.GetAttribute("href") != "")
                    {
                        // treat as link
                        if (element.GetAttribute("rel") == "external")
                        {
                            // add event handler which opens system default browser
                            element.Click += new HtmlElementEventHandler(OnExternal);
                        }
                        else
                        {
                            // add event handler which opens page internally
                            element.Click += new HtmlElementEventHandler(OnInternal);
                        }

                        element.SetAttribute("url", element.GetAttribute("href"));
                        element.SetAttribute("href", "javascript:;");
                    }
                    else
                    {
                        // treat as button
                        element.Click += new HtmlElementEventHandler(OnClick);
                    }
                }

                // retrieve all links with external flag
                foreach (HtmlElement element in window.Document.GetElementsByTagName("a"))
                {
                    if (element.GetAttribute("href") != "")
                    {
                        // treat as link
                        if (element.GetAttribute("rel") == "external")
                        {
                            // add event handler which opens system default browser
                            element.Click += new HtmlElementEventHandler(OnExternal);
                        }
                        else if (element.GetAttribute("command") != "")
                        {
                            // treat as button
                            element.Click += new HtmlElementEventHandler(OnClick);
                        }
                        else
                        {
                            // add event handler which opens page internally
                            element.Click += new HtmlElementEventHandler(OnInternal);
                        }

                        element.SetAttribute("url", element.GetAttribute("href"));
                        element.SetAttribute("href", "javascript:;");
                    }
                }
            }

            foreach (HtmlWindow frame in window.Frames)
                AttachClickHandler(frame, url);
        }

        protected virtual void ReplaceVariables(HtmlWindow window, Uri url)
        {
        }

        protected virtual void OnClick(object sender, HtmlElementEventArgs args)
        {
        }

        void OnExternal(object sender, HtmlElementEventArgs args)
        {
            HtmlElement element = sender as HtmlElement;
            ProcessStartInfo sInfo = new ProcessStartInfo(element.GetAttribute("url"));
            Process.Start(sInfo);
        }

        void OnInternal(object sender, HtmlElementEventArgs args)
        {
            HtmlElement element = sender as HtmlElement;
            Uri url = new Uri(browser.Url, element.GetAttribute("url"));
            if (element.GetAttribute("target") == "")
                element.Document.Window.Navigate(url);
            else
                browser.Navigate(url, element.GetAttribute("target"));
        }
    }
}
