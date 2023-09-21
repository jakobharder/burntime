using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Burntime.Launcher
{
    static class ChangelogToHtml
    {
        static public string Convert(Stream txt)
        {
            string result = "<br/>";
            bool list = false;

            StreamReader reader = new StreamReader(txt);
            string str = reader.ReadLine();
            while (str != null)
            {
                str = str.Trim();
                if (str != "")
                {
                    if (str.StartsWith("*"))
                    {
                        if (!list)
                            result += "<ul>";
                        list = true;

                        result += "<li>" + str.Substring(1).Trim() + "</li>";
                    }
                    else
                    {
                        if (list)
                            result += "</ul>";
                        list = false;

                        result += str;
                        result += "<br/>";
                    }
                }
                str = reader.ReadLine();
            }

            if (list)
                result += "</ul>";
            return result + "<br/>";
        }
    }
}