using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Runtime.InteropServices.ObjectiveC;

namespace Burntime.Framework.GUI;

public class Button : Window
{
    public VerticalTextAlignment TextVerticalAlign = VerticalTextAlignment.Default;
    public TextAlignment TextHorizontalAlign = TextAlignment.Default;

    public bool IsHover { get; private set; }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; if (_isEnabled == false) IsHover = false; }
    }

    public CommandEvent? Command;

    public GuiImage? Image { get; set; }
    public GuiImage? HoverImage { get; set; }
    public GuiImage? DownImage { get; set; }

    public GuiFont? HoverFont { get; set; }
    public GuiFont? DisabledFont { get; set; }
    public GuiString? ToolTipText { get; set; }
    public GuiFont? ToolTipFont { get; set; }

    public GuiString? Text
    {
        get => _text;
        set { if (_text?.ID != value?.ID) { _text = value; if (_isTextOnly) RefreshTextSize(); } }
    }
    GuiString? _text;

    public GuiFont? Font
    {
        get => _font;
        set { _font = value; if (_isTextOnly) RefreshTextSize(); }
    }
    GuiFont? _font;

    [Obsolete("use IsTextOnly instead")]
    public void SetTextOnly() => IsTextOnly = true;

    public bool IsTextOnly
    {
        get => _isTextOnly;
        set
        {
            if (value)
            {
                RefreshTextSize();
                if (TextVerticalAlign == VerticalTextAlignment.Default)
                    TextVerticalAlign = VerticalTextAlignment.Top;
                if (TextHorizontalAlign == TextAlignment.Default)
                    TextHorizontalAlign = TextAlignment.Left;
            }
            _isTextOnly = value;
        }
    }
    private bool _isTextOnly;

    void RefreshTextSize()
    {
        if (Font != null && Text != null)
        {
            Size = Font.GetRect(0, 0, Text).Size;
            sizeSet = true;
        }
    }
    bool sizeSet = false;

    protected bool _isDown = false;

    public object Context { get; set; }

    public Button(Module app, Action? command = null)
        : base(app)
    {
        if (command is not null)
        {
            Command = new CommandEvent();
            Command += new CommandHandler(command);
        }
    }

    private string _lastLanguage = string.Empty;
    public override void OnRender(RenderTarget Target)
    {
        if (IsTextOnly && _lastLanguage != app.Language)
        {
            RefreshTextSize();
            _lastLanguage = app.Language;
        }

        if (!sizeSet)
        {
            if (Image != null && Image.IsLoaded)
            {
                sizeSet = true;
                Size = new Vector2(Image.Width, Image.Height);
            }
            else if (HoverImage != null && HoverImage.IsLoaded)
            {
                sizeSet = true;
                Size = new Vector2(HoverImage.Width, HoverImage.Height);
            }
            else if (DownImage != null && DownImage.IsLoaded)
            {
                sizeSet = true;
                Size = new Vector2(DownImage.Width, DownImage.Height);
            }
        }

        // preload hover and down images
        HoverImage?.Touch();
        DownImage?.Touch();

        if (IsHover && HoverImage != null)
        {
            Target.DrawSprite(HoverImage);
        }
        else if (_isDown)
        {
            Target.DrawSprite(DownImage);
        }
        else
        {
            Target.DrawSprite(Image);
        }

        if (Text != null && Font != null)
        {
            Vector2 textpos;
            if (TextHorizontalAlign == TextAlignment.Left)
                textpos.x = 0;
            else if (TextHorizontalAlign == TextAlignment.Center
                || TextHorizontalAlign == TextAlignment.Default)
                textpos.x = Size.x / 2;
            else
                textpos.x = Size.x;
            if (TextVerticalAlign == VerticalTextAlignment.Top)
                textpos.y = 0;
            else if (TextVerticalAlign == VerticalTextAlignment.Center
                || TextVerticalAlign == VerticalTextAlignment.Default)
                textpos.y = Size.y / 2;
            else
                textpos.y = Size.y;

            GuiFont font;
            if (!IsEnabled && DisabledFont is not null)
                font = DisabledFont;
            else if (IsEnabled && IsHover && HoverFont is not null)
                font = HoverFont;
            else
                font = Font;
            font.DrawText(Target, textpos, Text, TextHorizontalAlign, TextVerticalAlign);
        }

        if (IsHover && ToolTipText is not null && ToolTipFont is not null)
        {
            Vector2 textpos;
            textpos.x = Size.x / 2;
            textpos.y = -2;

            ToolTipFont.DrawText(Target, textpos, ToolTipText, TextAlignment.Center, VerticalTextAlignment.Bottom);
        }
    }

    public override void OnMouseEnter()
    {
        if (!IsEnabled) return;

        IsHover = true;
    }

    public override void OnMouseLeave()
    {
        if (!IsEnabled) return;

        IsHover = false;
    }

    public override bool OnMouseClick(Vector2 Position, MouseButton Button)
    {
        if (!IsEnabled) return true;

        return OnButtonClick();
    }

    public virtual bool OnButtonClick()
    {
        if (Command != null)
        {
            Command.Execute();
            return false;
        }

        return false;
    }
}
