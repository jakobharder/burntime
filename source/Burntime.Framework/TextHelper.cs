using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Burntime.Platform.Resource;

namespace Burntime.Framework;

public sealed class TextHelper
{
    struct Replacement
    {
        public string Argument;
        public string Value;
    }

    readonly IResourceManager _resourceManager;
    readonly List<Replacement> _arguments = new();
    readonly string _textFile;

    public TextHelper(Module app, string file)
    {
        _resourceManager = app.ResourceManager;
        _textFile = file;
    }

    public void AddArgument(string argument, int intValue)
    {
        AddArgument(argument, intValue.ToString());
    }

    public void AddArgument(string argument, string textValue)
    {
        Replacement repl = new()
        {
            Argument = argument,
            Value = textValue
        };
        _arguments.Add(repl);
    }

    public void ClearArguments() => _arguments.Clear();

    public string this[int Index] => Get(Index);

    public string Get(int index) => Get(_textFile + "?" + index);

    /// <summary>
    /// Return text with replaced arguments.
    /// Separate plural with |# (plural, single, none).
    /// Plural only supports one replacement.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public string Get(string id)
    {
        string str = _resourceManager.GetString(id);
        string[] plural = str.Split("|#");
        string? result = null;

        foreach (Replacement r in _arguments)
        {
            if (!(result ?? plural[0]).Contains(r.Argument))
                continue;

            if (plural.Length >= 3 && r.Value == "0")
            {
                result = (result ?? plural[2]).Replace(r.Argument, r.Value);
            }
            else if (plural.Length >= 2 && r.Value == "1")
            {
                result = (result ?? plural[1]).Replace(r.Argument, r.Value);
            }
            else
            {
                result = (result ?? plural[0]).Replace(r.Argument, r.Value);
            }
        }

        return result ?? plural[0];
    }

    public string[] GetStrings(int start)
    {
        string[] strs = _resourceManager.GetStrings(_textFile + "?" + start);
        for (int i = 0; i < strs.Length; i++)
        {
            foreach (Replacement r in _arguments)
            {
                strs[i] = strs[i].Replace(r.Argument, r.Value);
            }
        }

        return strs;
    }
}
