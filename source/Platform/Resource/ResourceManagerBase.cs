﻿using Burntime.Platform.IO;
using System.Collections.Generic;
using System;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource;

public struct Replacement
{
    public String Argument;
    public String Value;
}

public struct ResourceInfoFont
{
    public String Name;
    public PixelColor Fore;
    public PixelColor Back;
}

public class ResourceManagerBase
{
    public ResourceManagerBase()
    {
        AddSpriteProcessor("png", new SpriteProcessorPng());
        AddDataProcessor("png", typeof(SpriteProcessorPng));
        AddSpriteProcessor("pngani", new AniProcessorPng());
        AddDataProcessor("pngani", typeof(AniProcessorPng));
        AddFontProcessor("txt", new FontProcessorTxt());
    }

    #region Text
    public Encoding? Encoding { get; set; }

    // from textDB

    Dictionary<String, TextResourceFile> txtDB = new Dictionary<string, TextResourceFile>();
    List<Replacement> listArguments = new List<Replacement>();

    public void AddDB(string filename)
    {
        File file = FileSystem.GetFile(filename + (filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) ? "" : ".txt"));
        file.Encoding = Encoding;
        TextResourceFile db = new TextResourceFile(file);
        txtDB.Add(filename, db);
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

    public String ShiftID(String id, int shift)
    {
        String fileName;
        int pos = shift;
        int atmark = id.LastIndexOf('?');
        if (atmark > 0)
        {
            fileName = id.Substring(0, atmark);
            pos += int.Parse(id.Substring(atmark + 1));
        }
        else
            fileName = id;

        return fileName + "?" + pos;
    }

    public String[] GetStrings(String id)
    {
        int atmark = id.LastIndexOf('?');

        string file = id.Substring(0, atmark);

        int index = 0;
        if (id.Substring(atmark + 1).ToLower().StartsWith("s"))
        {
            if (!txtDB.ContainsKey(file))
                AddDB(file);

            int section = int.Parse(id.Substring(atmark + 2));

            int sections = 0;
            for (int i = 0; i < txtDB[file].Data.Count && sections < section; i++)
            {
                int f = txtDB[file].Data[i].IndexOf("}#");
                if (f != -1)
                {
                    sections++;
                    index = i + 1;
                }
            }
        }
        else
            index = int.Parse(id.Substring(atmark + 1));

        return GetStrings(file, index);
    }

    public String[] GetStrings(String file, int startIndex)
    {
        if (!txtDB.ContainsKey(file))
            AddDB(file);

        int last = startIndex + 1;

        for (int i = startIndex; i < txtDB[file].Data.Count; i++)
        {
            int f = txtDB[file].Data[i].IndexOf("}#");
            if (f != -1)
            {
                last = i;
                break;
            }
        }

        int count = last - startIndex;
        String[] strs = new String[count];
        for (int i = 0; i < count; i++)
        {
            strs[i] = txtDB[file].Data[i + startIndex].Replace("}", "");
        }

        return strs;
    }

    public string GetString(string id)
    {
        if (id.StartsWith("@"))
            id = id.Substring(1);

        int atmark = id.LastIndexOf('?');
        return GetString(id.Substring(0, atmark), int.Parse(id.Substring(atmark + 1)));
    }

    public string GetString(string file, int index)
    {
        if (!txtDB.ContainsKey(file))
            AddDB(file);
        string res = txtDB[file].Data[index];

        if (res.EndsWith("}"))
            return res.Substring(0, res.Length - 1);
        return res;
    }
    #endregion

    #region DataProcessor
    protected Dictionary<String, IFontProcessor> fontProcessors = new Dictionary<string, IFontProcessor>();
    protected Dictionary<String, Type> dataProcessors = new Dictionary<string, Type>();

    public void AddSpriteProcessor(String Extension, ISpriteProcessor Processor)
    {
        spriteProcessors.Add(Extension, Processor);
    }

    public void AddFontProcessor(String Extension, IFontProcessor Processor)
    {
        fontProcessors.Add(Extension, Processor);
    }

    public void AddDataProcessor(String format, Type dataProcessor)
    {
        dataProcessors.Add(format, dataProcessor);
    }

    public IDataProcessor GetDataProcessor(String Format)
    {
        return (IDataProcessor)Activator.CreateInstance(dataProcessors[Format]);
    }
    #endregion

    #region Replacement
    protected ConfigFile? replacement;

    public void SetResourceReplacement(string file)
    {
        if (string.IsNullOrEmpty(file))
        {
            replacement = null;
        }
        else
        {
            replacement = new ConfigFile();
            replacement.Open(FileSystem.GetFile(file));
        }
    }

    protected ResourceID GetReplacementID(ResourceID id)
    {
        if (replacement != null)
        {
            string idstring = null;

            if (replacement["replacement"].ContainsKey(id.Format + "@" + id.File))
            {
                idstring = replacement["replacement"].Get(id.Format + "@" + id.File);
            }
            else if (replacement["replacement"].ContainsKey(id.File))
            {
                idstring = replacement["replacement"].Get(id.File);
            }

            if (idstring != null)
            {

                // construct new resource id
                if (id.IndexProvided)
                {
                    idstring += "?" + id.Index.ToString();
                    if (id.EndIndex != -1)
                        idstring += "-" + id.EndIndex.ToString();
                    if (!string.IsNullOrEmpty(id.Custom))
                        idstring += "?" + id.Custom;
                }
                else
                {
                    if (!string.IsNullOrEmpty(id.Custom))
                        idstring += "??" + id.Custom;
                }

                return idstring;
            }
        }

        return null;
    }

    protected bool CheckReplacementID(ResourceID id)
    {
        string format = id.Format;
        if (spriteProcessors.ContainsKey(format))
        {
            lock (this)
            {
                ISpriteProcessor loader = spriteProcessors[format];
                //loader.IsAvailable(newid);

                // TODO
                // for the moment just check the newid.file file existance
                return FileSystem.ExistsFile(string.Format(id.File, id.Index));
            }
        }

        return false;
    }
    #endregion

    protected static int MakePowerOfTwo(int nValue)
    {
        nValue--;
        int i;
        for (i = 0; nValue != 0; i++)
            nValue >>= 1;
        return 1 << i;
    }

    protected Dictionary<String, ISpriteProcessor> spriteProcessors = new Dictionary<string, ISpriteProcessor>();
    protected ISpriteProcessor GetSpriteProcessor(ResourceID id, bool ownInstance)
    {

#warning this should be handled differently

        string format = id.Format;
        if (id.Format == "raw" && id.HasMultipleFrames)
        {
            format = "ani";
        }

        if (ownInstance)
            return (ISpriteProcessor)Activator.CreateInstance(spriteProcessors[format].GetType());

        return spriteProcessors[format];
    }
}
