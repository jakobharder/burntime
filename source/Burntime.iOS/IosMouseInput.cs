using CoreGraphics;
using Foundation;
using GameController;
using UIKit;

namespace Burntime.MonoGame;

// UIKit supplies absolute pointer coordinates, buttons and trackpad/wheel events.
// Only indirect-pointer touches are captured; finger/Pencil contacts still go
// to MonoGame's TouchPanel without being delayed or cancelled.
sealed class IosMouseInput : IDisposable
{
    internal enum Kind { Move, Buttons, Scroll, Leave, Cancel }
    internal readonly record struct Sample(Kind Type, CGPoint Position, ulong Buttons = 0, double Scroll = 0);

    readonly UIView view;
    readonly Action<Sample> receive;
    readonly PointerPress press;
    readonly UIHoverGestureRecognizer hover;
    readonly UIPanGestureRecognizer scroll;
    readonly HiddenPointerStyle pointerStyle = new();
    readonly UIPointerInteraction pointer;
    CGPoint? previousHover;
    ulong nativeButtons, publishedButtons;
    bool hardwareRight, suppressHardwareButton = true;
    bool pointerInside;
    CGPoint pointerPosition;

    public IosMouseInput(UIView view, Action<Sample> receive)
    {
        this.view = view;
        this.receive = receive;
        press = new PointerPress(OnPointerSample);
        hover = new UIHoverGestureRecognizer(OnHover)
        {
            AllowedTouchTypes = new[] { NSNumber.FromInt32((int)UITouchType.IndirectPointer) },
            CancelsTouchesInView = false,
            ShouldRecognizeSimultaneously = (_, _) => true
        };
        scroll = new UIPanGestureRecognizer(OnScroll)
        {
            AllowedTouchTypes = Array.Empty<NSNumber>(),
            AllowedScrollTypesMask = UIScrollTypeMask.All,
            CancelsTouchesInView = false,
            ShouldRecognizeSimultaneously = (_, _) => true
        };
        pointer = new UIPointerInteraction(pointerStyle);
        view.AddGestureRecognizer(press);
        view.AddGestureRecognizer(hover);
        view.AddGestureRecognizer(scroll);
        view.AddInteraction(pointer);
    }

    void OnPointerSample(Sample sample)
    {
        pointerPosition = sample.Position;
        pointerInside = view.Bounds.Contains(sample.Position);
        if (sample.Type == Kind.Buttons)
        {
            nativeButtons = sample.Buttons;
            PublishButtons();
        }
        else
        {
            if (sample.Type == Kind.Cancel)
            {
                nativeButtons = publishedButtons = 0;
                hardwareRight = false;
                suppressHardwareButton = true;
            }
            receive(sample);
        }
    }

    void PublishButtons()
    {
        ulong buttons = nativeButtons | (hardwareRight ? 2UL : 0UL);
        if (buttons == publishedButtons) return;
        publishedButtons = buttons;
        receive(new(Kind.Buttons, pointerPosition, buttons));
    }

    // Some Designed for iPad environments omit secondary UIKit touches.
    // Merge the physical button state with UIKit's mask before dispatch so
    // platforms delivering both sources still generate one down/up pair.
    public void PollButtons()
    {
        var mice = GCMouse.Mice;
        bool right = mice.Any(mouse => mouse.MouseInput?.RightButton?.IsPressed == true);
        if (!pointerInside) return;
        if (suppressHardwareButton)
        {
            if (!right) suppressHardwareButton = false;
            return;
        }
        if (hardwareRight && mice.Length == 0)
        {
            hardwareRight = false;
            nativeButtons = publishedButtons = 0;
            receive(new(Kind.Cancel, pointerPosition));
        }
        else if (right != hardwareRight)
        {
            hardwareRight = right;
            PublishButtons();
        }
    }

    void OnHover(UIHoverGestureRecognizer gesture)
    {
        CGPoint position = gesture.LocationInView(view);
        pointerPosition = position;
        pointerInside = view.Bounds.Contains(position);
        if (gesture.State == UIGestureRecognizerState.Changed &&
            previousHover is CGPoint previous && position != previous)
            receive(new(Kind.Move, position));
        previousHover = position;
        if (gesture.State is UIGestureRecognizerState.Ended or UIGestureRecognizerState.Cancelled)
        {
            previousHover = null;
            // Hover may end when a button is pressed. Do not cancel that
            // button's down/up pair while the pointer is still inside the view.
            if (!view.Bounds.Contains(position))
            {
                nativeButtons = publishedButtons = 0;
                hardwareRight = false;
                suppressHardwareButton = true;
                receive(new(Kind.Leave, position));
            }
        }
    }

    void OnScroll(UIPanGestureRecognizer gesture)
    {
        if (gesture.State is UIGestureRecognizerState.Began or UIGestureRecognizerState.Changed)
        {
            var delta = gesture.TranslationInView(view);
            gesture.SetTranslation(CGPoint.Empty, view);
            if (delta.Y != 0)
                receive(new(Kind.Scroll, gesture.LocationInView(view), Scroll: (double)delta.Y));
        }
    }

    public CGPoint ToNormalized(CGPoint position) => new(
        (position.X - view.Bounds.X) / Math.Max(1, (double)view.Bounds.Width),
        (position.Y - view.Bounds.Y) / Math.Max(1, (double)view.Bounds.Height));

    public void Reset()
    {
        previousHover = null;
        pointerInside = false;
        nativeButtons = publishedButtons = 0;
        hardwareRight = false;
        suppressHardwareButton = true;
        // Cancel any held pointer button without synthesizing a release click.
        press.Enabled = false;
        press.Enabled = true;
    }

    public void Dispose()
    {
        view.RemoveGestureRecognizer(press);
        view.RemoveGestureRecognizer(hover);
        view.RemoveGestureRecognizer(scroll);
        view.RemoveInteraction(pointer);
        press.Dispose();
        hover.Dispose();
        scroll.Dispose();
        pointer.Dispose();
        pointerStyle.Dispose();
    }

    sealed class HiddenPointerStyle : UIPointerInteractionDelegate
    {
        public override UIPointerRegion? GetRegionForRequest(UIPointerInteraction interaction,
            UIPointerRegionRequest request, UIPointerRegion defaultRegion) => defaultRegion;

        public override UIPointerStyle? GetStyleForRegion(UIPointerInteraction interaction, UIPointerRegion region) =>
            UIPointerStyle.CreateHiddenPointerStyle();
    }

    sealed class PointerPress : UIGestureRecognizer
    {
        readonly Action<Sample> receive;
        CGPoint previous;

        public PointerPress(Action<Sample> receive)
        {
            this.receive = receive;
            AllowedTouchTypes = new[] { NSNumber.FromInt32((int)UITouchType.IndirectPointer) };
            CancelsTouchesInView = true;
            DelaysTouchesBegan = true;
            ShouldRecognizeSimultaneously = (_, _) => true;
        }

        // UIKit's default event filter accepts primary clicks only. Touch type
        // filtering still excludes finger/Pencil input after this event filter.
        public override bool ShouldReceive(UIEvent evt) => true;

        void Read(NSSet touches, UIEvent evt, bool moved)
        {
            var position = touches.AnyObject is UITouch touch
                ? touch.LocationInView(View) : LocationInView(View);
            if (moved && position != previous) receive(new(Kind.Move, position));
            previous = position;
            receive(new(Kind.Buttons, position, (ulong)evt.ButtonMask));
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            Read(touches, evt, false);
            State = UIGestureRecognizerState.Began;
        }

        public override void TouchesMoved(NSSet touches, UIEvent evt)
        {
            Read(touches, evt, true);
            State = UIGestureRecognizerState.Changed;
        }

        public override void TouchesEnded(NSSet touches, UIEvent evt)
        {
            Read(touches, evt, false);
            State = UIGestureRecognizerState.Ended;
        }

        public override void TouchesCancelled(NSSet touches, UIEvent evt)
        {
            receive(new(Kind.Cancel, previous));
            State = UIGestureRecognizerState.Cancelled;
        }
    }
}
