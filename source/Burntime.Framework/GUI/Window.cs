using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Framework.GUI;

public enum PositionAlignment
{
    Left,
    Right,
    Center
}

public class Window
{
    internal Container parent;
    public Container Parent
    {
        get { return parent; }
    }

    public Vector2 PositionOnScreen
    {
        get
        {
            if (parent == null)
                return Position;
            else
                return Position + parent.PositionOnScreen;
        }
    }

    protected Module app;

    public event EventHandler WindowShow;
    public event EventHandler WindowHide;

    // attributes
    protected bool visible = true;
    //bool active = false;
    bool modal;
    bool hasFocus = false;
    Vector2 pos = new Vector2();
    Vector2 size = new Vector2();
    PositionAlignment vertAlign;
    PositionAlignment horizAlign;
    bool isEntered = false;
    bool captureAllMouseMove = false;
    bool captureAllMouseClicks = false;

    public bool CaptureAllMouseMove
    {
        get { return captureAllMouseMove; }
        set { captureAllMouseMove = value; }
    }

    public bool CaptureAllMouseClicks
    {
        get { return captureAllMouseClicks; }
        set { captureAllMouseClicks = value; }
    }

    public bool HasFocus
    {
        get { return hasFocus; }
        set { hasFocus = value; }
    }

    public PositionAlignment VerticalAlignment
    {
        get { return vertAlign; }
        set { vertAlign = value; RefreshBoundings(); }
    }

    public PositionAlignment HorizontalAlignment
    {
        get { return horizAlign; }
        set { horizAlign = value; RefreshBoundings(); }
    }

    private int group = 0;
    public int Group
    {
        get { return group; }
        set { group = value; }
    }

    protected int layer = 0;
    public virtual int Layer
    {
        get { return layer; }
        set { layer = value; }
    }

    public Vector2 Position
    {
        get { return pos; }
        set
        {
            pos = value;
            RefreshBoundings();
        }
    }

    public virtual Vector2 Size
    {
        get { return size; }
        set
        {
            size = value;
            RefreshBoundings();
        }
    }

    public bool IsVisible
    {
        get { return visible; }
        set { if (value) Show(); else Hide(); }
    }

    public bool IsModal
    {
        get { return modal; }
        set { Hide(); modal = value; }
    }

    // public attributes
    Rect boundings = new Rect(0, 0, 0, 0);

    void RefreshBoundings()
    {
        boundings.Left = pos.x;
        if (horizAlign == PositionAlignment.Center)
            boundings.Left -= size.x / 2;
        else if (horizAlign == PositionAlignment.Right)
            boundings.Left -= size.x;
        boundings.Right = boundings.Left + size.x;
        boundings.Top = pos.y;
        if (vertAlign == PositionAlignment.Center)
            boundings.Top -= size.y / 2;
        else if (vertAlign == PositionAlignment.Right)
            boundings.Top -= size.y;
        boundings.Bottom = boundings.Top + size.y;
    }

    public Rect Boundings
    {
        get { return boundings; }
        //set { pos = value.Position; size = value.Size; }
    }

    public Window(Module App)
    {
        app = App;
    }

    public void Show()
    {
        if (!visible && modal)
            app.SceneManager.PushModalStack(this);
        visible = true;

        OnShow();
        if (WindowShow != null)
            WindowShow.Invoke(this, new EventArgs());
    }

    public void Hide()
    {
        if (visible && modal)
            app.SceneManager.PopModalStack();
        visible = false;

        OnHide();
        if (WindowHide != null)
            WindowHide.Invoke(this, new EventArgs());
    }

    public void MoveInside(Rect rectangle)
    {
        pos.Min(rectangle.Position);
        pos.Max(rectangle.Position + rectangle.Size - Size);
        RefreshBoundings();
    }

    // internal message handling
    internal virtual bool MouseClick(Vector2 position, MouseButton button)
    {
        if (!visible)
            return false;

        if (Boundings.PointInside(position))
            return OnMouseClick(position - this.Position, button);
        else if (captureAllMouseClicks)
            return OnMouseClick(position - this.Position, button);

        return false;
    }

    internal virtual void ModalLeave()
    {
        if (isEntered)
        {
            OnMouseLeave();
            isEntered = false;
        }
    }

    internal virtual bool MouseMove(Vector2 position)
    {
        if (!visible)
            return false;

        if (Boundings.PointInside(position))
        {
            if (!isEntered)
            {
                OnMouseEnter();
                isEntered = true;
            }

            OnMouseMove(position - this.Position);
        }
        else if (isEntered)
        {
            OnMouseLeave();
            isEntered = false;
        }

        if (captureAllMouseMove && !isEntered)
            OnMouseMove(position - this.Position);

        return true;
    }

    internal virtual bool MouseDown(Vector2 position, MouseButton button)
    {
        if (!visible)
            return false;

        if (Boundings.PointInside(position))
            return OnMouseDown(position - this.Position, button);
        else if (captureAllMouseClicks)
            return OnMouseDown(position - this.Position, button);

        return false;
    }

    internal virtual bool MouseUp(Vector2 position, MouseButton button)
    {
        if (!visible)
            return false;

        if (Boundings.PointInside(position))
            return OnMouseUp(position - this.Position, button);
        else if (captureAllMouseClicks)
            return OnMouseUp(position - this.Position, button);

        return false;
    }

    internal virtual void KeyPress(char key)
    {
        if (hasFocus)
            OnKeyPress(key);
    }

    internal virtual void VKeyPress(SystemKey key)
    {
        if (hasFocus)
            OnVKeyPress(key);
    }

    internal virtual void Render(RenderTarget target)
    {
        if (!visible)
            return;

        target.Layer = layer;
        OnRender(target.GetSubBuffer(Boundings));
        target.Layer = layer;
    }

    internal virtual void Update(float elapsed)
    {
        if (!visible)
            return;

        OnUpdate(elapsed);
    }

    internal Rect LocalToGlobal(Rect rc)
    {
        if (parent == null)
            return rc;
        return parent.LocalToGlobal(rc + pos);
    }

    // message handling
    // input
    public virtual bool OnMouseClick(Vector2 position, MouseButton button) { return false; }
    public virtual bool OnMouseMove(Vector2 position) { return false; }
    public virtual void OnMouseEnter() { }
    public virtual void OnMouseLeave() { }
    public virtual bool OnMouseDown(Vector2 position, MouseButton button) { return false; }
    public virtual bool OnMouseUp(Vector2 position, MouseButton button) { return false; }
    public virtual bool OnKeyPress(char key) { return false; }
    public virtual bool OnVKeyPress(SystemKey key) { return false; }
    // state
    public virtual void OnActivate() { }
    public virtual void OnShow() { }
    public virtual void OnHide() { }
    public virtual void OnInactivate() { }
    // frame update
    public virtual void OnUpdate(float elapsed) { }
    // render
    public virtual void OnRender(RenderTarget target) { }

    public virtual void OnResizeScreen() { }
}
