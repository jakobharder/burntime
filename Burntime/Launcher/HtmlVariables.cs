using System;
using System.Collections.Generic;
using System.Text;

using System.Windows.Forms;

namespace Burntime.Launcher
{
    class HtmlVariables
    {
        Dictionary<string, bool> flags;
        Dictionary<string, string> texts;

        public HtmlVariables()
        {
            flags = new Dictionary<string, bool>();
            texts = new Dictionary<string, string>();
        }

        public void SetFlag(string name, bool flag)
        {
            if (flags.ContainsKey(name))
                flags[name] = flag;
            else
                flags.Add(name, flag);
        }

        public void SetText(string name, string text)
        {
            if (texts.ContainsKey(name))
                texts[name] = text;
            else
                texts.Add(name, text);
        }

        public void UpdateHtml(HtmlDocument document)
        {
            // retrieve all buttons
            foreach (HtmlElement element in document.GetElementsByTagName("var"))
            {
                string name = element.GetAttribute("name").ToLower();

                if (name != "")
                {
                    if (texts.ContainsKey(name))
                    {
                        element.OuterHtml = texts[name];
                    }
                    else
                    {
                        element.OuterHtml = "<i>$" + name + "</i>";
                    }
                }
            }

            // retrieve all ifs
            foreach (HtmlElement element in document.GetElementsByTagName("if"))
            {
                string name = element.GetAttribute("name").ToLower();

                if (name != "")
                {
                    if (flags.ContainsKey(name))
                    {
                        if (!flags[name])
                            element.OuterHtml = "";
                    }
                    else
                    {
                        element.OuterHtml = "<i>$" + name + "</i>";
                    }
                }
            }
        }
    }
}
