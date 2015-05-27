using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;

namespace Burntime.Framework
{
    public sealed class TextHelper
    {
        struct Replacement
        {
            public String Argument;
            public String Value;
        }

        ResourceManager resMan;
        List<Replacement> listArguments = new List<Replacement>();
        String file;

        public TextHelper(Module App, String File)
        {
            resMan = App.ResourceManager;
            file = File;
        }

        public void AddArgument(String Argument, int Value)
        {
            AddArgument(Argument, Value.ToString());
        }

        public void AddArgument(String Argument, String Value)
        {
            Replacement repl = new Replacement();
            repl.Argument = Argument;
            repl.Value = Value;
            listArguments.Add(repl);
        }

        public void ClearArguments()
        {
            listArguments.Clear();
        }

        public String Get(int Index)
        {
            String str = resMan.GetString(file + "?" + Index);
            foreach (Replacement r in listArguments)
            {
                str = str.Replace(r.Argument, r.Value);
            }

            return str;
        }

        public String[] GetStrings(int start)
        {
            String[] strs = resMan.GetStrings(file + "?" + start);
            for (int i = 0; i < strs.Length; i++)
            {
                foreach (Replacement r in listArguments)
                {
                    strs[i] = strs[i].Replace(r.Argument, r.Value);
                }
            }

            return strs;
        }

        public String this[int Index]
        {
            get { return Get(Index); }
        }
    }
}
