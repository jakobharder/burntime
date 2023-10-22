using System.Collections.Generic;

namespace Burntime.Framework.GUI;

public class GuiString
{
    public string ID { get; private set; }
    protected Dictionary<string, string> _text;

    public static implicit operator GuiString(string id) => new GuiString(id);

    public GuiString(string? str)
    {
        _text = new Dictionary<string, string>();
        ID = str ?? "";
    }

    public static implicit operator string(GuiString right)
    {
        if (!right._text.TryGetValue(Module.Instance.Language, out string? text))
        {
            if (right.ID.StartsWith("@") == true)
                text = Module.Instance.ResourceManager.GetString(right.ID[1..]);
            else
                text = right.ID;
            right._text[Module.Instance.Language] = text;
        }

        return text;
    }
}
