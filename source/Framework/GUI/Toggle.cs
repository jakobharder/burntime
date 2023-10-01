using Burntime.Platform.Graphics;
using System.Collections.Generic;

namespace Burntime.Framework.GUI;

public class Toggle : Button
{
    struct StateVisuals
    {
        public GuiString Text;
        public GuiImage Image;
        public GuiImage HoverImage;
        public GuiImage DownImage;
        public GuiString ToolTip;
    }

    private List<StateVisuals> states_ = new();
    private int state_ = -1;

    public int State
    {
        get { return state_; }
        set
        {
            state_ = value;
            Text = states_[state_].Text;
            Image = states_[state_].Image;
            HoverImage = states_[state_].HoverImage;
            DownImage = states_[state_].DownImage;
            ToolTipText = states_[state_].ToolTip;
        }
    }

    public Toggle(Module App)
        : base(App)
    {
    }

    public void AddState(string text, GuiImage image, GuiImage hoverImage, GuiImage downImage, string toolTip)
    {
        states_.Add(new() { Text = text, Image = image, HoverImage = hoverImage, DownImage = downImage, ToolTip = toolTip });

        if (states_.Count == 1)
            State = 0;
    }

    public override bool OnMouseClick(Platform.Vector2 Position, Platform.MouseButton Button)
    {
        State = (State + 1) % states_.Count;

        OnButtonClick();
        return true;
    }

    public override void OnRender(IRenderTarget Target)
    {
        base.OnRender(Target);

        // ensure all states are loaded to avoid flickering
        foreach (var state in states_)
        {
            state.Image?.Touch();
            state.HoverImage?.Touch();
            state.DownImage?.Touch();
        }
    }
}
