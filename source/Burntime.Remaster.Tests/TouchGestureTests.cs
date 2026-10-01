using System.Collections.Generic;
using Burntime.Framework;
using Burntime.Framework.GUI;
using BindingFlags = System.Reflection.BindingFlags;
using Burntime.Platform;
using Burntime.Remaster.GUI;
using Burntime.Remaster.Maps;

namespace Burntime.Remaster.Tests;

using static Program;

static class TouchGestureTests
{
    internal static IEnumerable<Case<int>> Cases()
    {
        yield return Int("background and multi-contact cancellation discard pending hold release", 0, () =>
        {
            var app = new Module { Scenes = new() };
            var manager = new SceneManager(app);
            var stack = (Stack<Window>)typeof(SceneManager).GetField("modalStack",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            int cancellations = 0;
            int activations = 0;
            var menu = new HoldCancellationProbe(app, cancelled =>
            {
                if (cancelled) cancellations++;
                else activations++;
                stack.Clear();
            });
            stack.Push(menu);
            manager.QueueTouchGesture(new(TouchGestureKind.HoldEnd, Vector2.Zero,
                Vector2.Zero, Vector2.Zero), manager.TouchInputContext);
            manager.ClearTouchGestures();
            Equal(0, cancellations, "UI thread only requests cancellation");
            typeof(SceneManager).GetMethod("Process", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(manager, new object[] { 0f });
            Equal(1, cancellations, "game thread cancels the held menu");
            Equal(0, activations, "queued release never executes a command");
            return 0;
        });
        yield return Int("touch labels win taps and overlapping entrance markers choose the nearest", 0, () =>
        {
            TouchEntranceTarget[] targets = [
                new(0, new(20, 20), new(8, 8, 25, 25), new Rect(80, 10, 75, 25)),
                new(1, new(35, 20), new(23, 8, 25, 25), null),
                new(2, new(85, 20), new(73, 8, 25, 25), null)
            ];
            Equal(0, TouchEntranceLayout.HitTest(targets, new(90, 20)), "label before padded marker");
            Equal(0, TouchEntranceLayout.HitTest(targets, new(24, 20)), "nearest left marker");
            Equal(1, TouchEntranceLayout.HitTest(targets, new(31, 20)), "nearest right marker");
            Equal(-1, TouchEntranceLayout.HitTest(targets, new(200, 100)), "background remains movement area");
            Equal(true, TouchEntranceLayout.IsNearby(44, 15, false), "names appear outside interaction range");
            Equal(false, TouchEntranceLayout.IsNearby(50, 15, false), "distant names stay hidden");
            Equal(true, TouchEntranceLayout.IsNearby(50, 15, true), "visible names persist at the boundary");
            Equal(false, TouchEntranceLayout.IsNearby(58, 15, true), "names hide after leaving outer radius");
            return 0;
        });
        yield return Int("touch targets enlarge small buttons without changing their layout", 0, () =>
        {
            var app = new Module();
            var root = new Container(app) { Position = new(30, 20), Size = new(150, 150) };
            var upper = new Button(app) { Position = new(20, 20), Size = new(40, 8) };
            var lower = new Button(app) { Position = new(20, 36), Size = new(40, 8) };
            root.Windows += upper;
            root.Windows += lower;
            Window? Pick(Vector2 position) => (Window?)typeof(Container)
                .GetMethod("FindTouchTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(root, new object[] { position });
            Equal(upper, Pick(new(65, 34)), "padding above upper button");
            Equal(upper, Pick(new(65, 50)), "nearest upper in overlap");
            Equal(lower, Pick(new(65, 54)), "nearest lower in overlap");
            Equal(upper, Pick(new(65, 47)), "exact target wins over expanded neighbor");
            Equal(new Vector2(40, 8), upper.Size, "visual size unchanged");
            int pressCount = 0;
            int releaseCount = 0;
            bool releaseCancelled = false;
            upper.TouchPressed += () => pressCount++;
            upper.TouchReleased += cancelled =>
            {
                releaseCount++;
                releaseCancelled = cancelled;
            };
            upper.OnTouchPress(Vector2.Zero);
            upper.OnTouchPress(Vector2.Zero);
            Equal(true, upper.IsTouchPressed, "touch down highlights button");
            Equal(1, pressCount, "duplicate dispatch publishes one touch down");
            typeof(Container).GetMethod("TouchRelease",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(root,
                new object[] { new Vector2(120, 120), true });
            Equal(false, upper.IsTouchPressed, "cancelled touch clears highlight");
            Equal(1, releaseCount, "touch release is published once");
            Equal(true, releaseCancelled, "touch release preserves cancellation");
            lower.IsEnabled = false;
            Equal<Window?>(null, Pick(new(65, 66)), "disabled target ignored");
            root.Hide();
            Equal<Window?>(null, Pick(new(65, 34)), "hidden parent ignored");
            return 0;
        });
        yield return Int("face selection wraps in both directions and skips portraits in use", 0, () =>
        {
            Equal(0, FaceWindow.NextFaceId(5, 1, 5, new HashSet<int>()),
                "forward selection wraps to first face");
            Equal(5, FaceWindow.NextFaceId(0, -1, 5, new HashSet<int>()),
                "backward selection wraps to last face");
            Equal(1, FaceWindow.NextFaceId(5, 1, 5, new HashSet<int> { 0 }),
                "wrapped selection skips other player's face");
            Equal(5, FaceWindow.NextFaceId(1, -1, 5, new HashSet<int> { 0 }),
                "reverse selection skips other player's face");
            return 0;
        });
        yield return Int("location target pulse spans full opacity and repeats", 0, () =>
        {
            float bright = MapTargetPulse.GetAlpha(0);
            float dim = MapTargetPulse.GetAlpha(.375f);
            float nextBright = MapTargetPulse.GetAlpha(.75f);
            Equal(true, bright > dim, "pulse fades from its bright phase");
            Equal(true, System.Math.Abs(dim) < .0001f,
                "target label fully disappears at its dim phase");
            Equal(true, System.Math.Abs(bright - nextBright) < .0001f,
                "pulse repeats without drifting");
            return 0;
        });
        yield return Int("touch fight mode keeps distant rival names visible", 0, () =>
        {
            Equal(false, MapViewOverlayTouch.ShouldShowCharacter(100, true, false),
                "distant rival is hidden outside fight mode");
            Equal(true, MapViewOverlayTouch.ShouldShowCharacter(100, true, true),
                "distant rival remains visible in fight mode");
            Equal(false, MapViewOverlayTouch.ShouldShowCharacter(100, false, true),
                "distant friendly character stays hidden");
            Equal(true, MapViewOverlayTouch.ShouldShowCharacter(20, false, true),
                "nearby friendly character remains visible");
            return 0;
        });
        yield return Int("touch item selection survives modal focus changes", 0, () =>
        {
            Equal(false, ItemGridWindow.ShouldClearFocusOnPointerLeave(InputMode.Touch),
                "touch selection remains available after closing a modal");
            Equal(true, ItemGridWindow.ShouldClearFocusOnPointerLeave(InputMode.Mouse),
                "mouse hover still clears when leaving the grid");
            return 0;
        });
        yield return Int("location touch mode indicator clears the prompt row", 0, () =>
        {
            Equal(new Vector2(16, 382),
                LocationScene.TouchModeIndicatorPosition(new Vector2(640, 400)),
                "mode animation is raised into the HUD");
            return 0;
        });
        yield return Int("touch prompts resolve to gesture glyphs", 0, () =>
        {
            var app = new Module();
            Equal(TouchGlyph.Tap, InputControlDisplay.Resolve(app, InputMode.Touch,
                InputAction.Primary).Parts[0].Touch, "primary action uses tap");
            Equal(TouchGlyph.LongPress, InputControlDisplay.Resolve(app, InputMode.Touch,
                InputAction.Action).Parts[0].Touch, "action uses long press");
            Equal(TouchGlyph.LongPress, InputControlDisplay.Resolve(app, InputMode.Touch,
                InputAction.Secondary).Parts[0].Touch, "secondary action uses long press");
            Equal(TouchGlyph.SwipeHorizontal, InputControlDisplay.ResolvePattern(app,
                InputMode.Touch, InputPattern.HorizontalPaging).Parts[0].Touch,
                "horizontal paging uses horizontal swipe");
            Equal(TouchGlyph.SwipeVertical, InputControlDisplay.ResolvePattern(app,
                InputMode.Touch, InputPattern.VerticalPaging).Parts[0].Touch,
                "vertical paging uses vertical swipe");
            Equal(true, InputControlDisplay.Resolve(app, InputMode.Touch,
                InputAction.Back).IsEmpty, "unmapped touch action stays hidden");
            var overridden = new InputPrompt(InputAction.Back, "...")
            {
                TouchControl = TouchControl.LongPress
            };
            Equal(TouchGlyph.LongPress, InputControlDisplay.Resolve(app,
                InputMode.Touch, overridden).Parts[0].Touch,
                "prompt can override an action's touch gesture");
            var verticalTouchPaging = new InputPrompt(InputPattern.HorizontalPaging, "Page")
            {
                TouchPattern = InputPattern.VerticalPaging
            };
            Equal(TouchGlyph.SwipeVertical, InputControlDisplay.Resolve(app,
                InputMode.Touch, verticalTouchPaging).Parts[0].Touch,
                "prompt can override its touch navigation pattern");
            return 0;
        });
        yield return Int("fixed item targets keep bounds and topmost overlap priority", 0, () =>
        {
            var app = new Module();
            var root = new Container(app) { Size = new(100, 100) };
            var first = new FixedTarget(app) { Position = new(20, 20), Size = new(32, 32) };
            var top = new FixedTarget(app) { Position = new(36, 36), Size = new(32, 32) };
            root.Windows += first;
            root.Windows += top;
            Window? Pick(Vector2 position) => (Window?)typeof(Container)
                .GetMethod("FindTouchTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(root, new object[] { position });
            Equal<Window?>(null, Pick(new(19, 30)), "no extra item padding");
            Equal(top, Pick(new(40, 40)), "top overlapping item wins");
            Equal(first, Pick(new(22, 22)), "original item bounds");
            return 0;
        });
        yield return Int("scroll regions intercept child drags using local coordinates", 0, () =>
        {
            var module = new Module();
            var parent = new ScrollProbe(module) { Position = new(20, 30), Size = new(100, 100) };
            var child = new DragProbe(module) { Size = new(100, 100) };
            parent.Windows += child;
            var dispatch = typeof(Container).GetMethod("TouchDrag", BindingFlags.Instance | BindingFlags.NonPublic)!;
            dispatch.Invoke(parent, new object[] { new Vector2(40, 60), new Vector2(0, -12) });
            Equal(new Vector2(0, -12), parent.Scrolled, "scroll region receives drag");
            Equal(Vector2.Zero, child.Dragged, "child does not consume scroll");
            dispatch.Invoke(parent, new object[] { new Vector2(25, 35), new Vector2(0, 7) });
            Equal(new Vector2(0, 7), child.Dragged, "outside scroll region reaches child");
            parent.IsVisible = false;
            dispatch.Invoke(parent, new object[] { new Vector2(40, 60), new Vector2(0, -12) });
            Equal(new Vector2(0, -12), parent.Scrolled, "hidden container ignores drag");
            return 0;
        });
        yield return Int("scroll regions receive release velocity and kinetic scrolling decays", 0, () =>
        {
            var module = new Module();
            var parent = new ScrollProbe(module) { Position = new(20, 30), Size = new(100, 100) };
            var child = new DragProbe(module) { Size = new(100, 100) };
            parent.Windows += child;
            var dispatch = typeof(Container).GetMethod("TouchDragEnd",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            dispatch.Invoke(parent, new object[] {
                new Vector2(40, 60), new Vector2(0, -5), new Vector2f(0, -240)
            });
            Equal(new Vector2f(0, -240), parent.Released,
                "scroll owner receives release velocity");
            Equal(Vector2.Zero, child.Dragged,
                "child does not receive consumed release");

            var momentum = new KineticScroll();
            momentum.Release(1000);
            Equal(50f, momentum.Update(.1f), "release velocity is capped");
            Equal(true, momentum.IsActive, "momentum continues after first frame");
            for (int i = 0; i < 200; i++)
                momentum.Update(.1f);
            Equal(false, momentum.IsActive, "momentum decays to rest");
            return 0;
        });
        yield return Int("tap only fires on release", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            Equal(TouchGestureKind.Press, input.Begin(new(20, 30), 0).Kind, "press");
            Equal<TouchGesture?>(null, input.Move(new(23, 31), .1), "pending press");
            Equal<TouchGestureKind?>(TouchGestureKind.Tap, input.End(new(23, 31), .2)?.Kind, "tap");
            Equal<TouchGesture?>(null, input.End(new(23, 31), .3), "no duplicate release");
            return 0;
        });
        yield return Int("hold fires once and never left-clicks", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(20, 30), 0);
            Equal<TouchGestureKind?>(TouchGestureKind.LongPress, input.Move(new(20, 30), .5)?.Kind, "hold");
            Equal<TouchGesture?>(null, input.Move(new(20, 30), 1), "no repeat");
            Equal<TouchGestureKind?>(TouchGestureKind.HoldEnd, input.End(new(20, 30), 1.1)?.Kind, "release ends hold, never taps");
            return 0;
        });
        yield return Int("slow frame release cannot open an unseen hold menu", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(20, 30), 0);
            Equal<TouchGestureKind?>(TouchGestureKind.HoldEnd, input.End(new(20, 30), .6)?.Kind, "release without activating menu");
            return 0;
        });
        yield return Int("drag includes initial displacement and suppresses click", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(20, 30), 0);
            input.Move(new(25, 30), .1);
            var first = input.Move(new(40, 30), .2)!.Value;
            Equal(TouchGestureKind.Drag, first.Kind, "drag");
            Equal(new Vector2(20, 0), first.Delta, "full first delta");
            Equal(new Vector2(5, 0), input.Move(new(45, 30), .7)!.Value.Delta, "continued drag, no hold");
            Equal<TouchGestureKind?>(TouchGestureKind.DragEnd, input.End(new(45, 30), 1)?.Kind,
                "release ends drag without clicking");
            return 0;
        });
        yield return Int("release movement can start a drag", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(20, 30), 0);
            Equal<TouchGestureKind?>(TouchGestureKind.DragEnd, input.End(new(40, 30), .1)?.Kind, "drag");
            return 0;
        });
        yield return Int("drag release reports recent velocity but drops it after a pause", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(0, 0), 0);
            input.Move(new(20, 0), .05);
            input.Move(new(40, 0), .1);
            TouchGesture movingRelease = input.End(new(50, 0), .12)!.Value;
            Equal(true, movingRelease.Velocity.x > 0 && movingRelease.Velocity.y == 0,
                "moving release velocity");

            input.Begin(new(0, 0), 1);
            input.Move(new(30, 0), 1.05);
            TouchGesture pausedRelease = input.End(new(30, 0), 1.2)!.Value;
            Equal(Vector2f.Zero, pausedRelease.Velocity, "pause cancels fling");
            return 0;
        });
        yield return Int("cancellation prevents stale clicks", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(20, 30), 0);
            input.Cancel();
            Equal<TouchGesture?>(null, input.End(new(20, 30), .1), "cancelled");
            input.Begin(new(20, 30), 1);
            Equal<TouchGestureKind?>(TouchGestureKind.Tap, input.End(new(20, 30), 1.1)?.Kind, "new tap");
            return 0;
        });
        yield return Int("slide after hold never becomes a pan or tap", 0, () =>
        {
            var input = new TouchGestureRecognizer();
            input.Begin(new(20, 30), 0);
            input.Move(new(20, 30), .5);
            Equal<TouchGestureKind?>(TouchGestureKind.HoldMove, input.Move(new(80, 90), .7)?.Kind, "slide");
            Equal<TouchGestureKind?>(TouchGestureKind.HoldMove, input.Move(new(20, 30), .8)?.Kind, "back to center");
            Equal<TouchGestureKind?>(TouchGestureKind.HoldEnd, input.End(new(20, 30), .9)?.Kind, "release");
            Equal<TouchGesture?>(null, input.End(new(80, 90), 1), "no duplicate release");
            input.Begin(new(20, 30), 2);
            input.Move(new(20, 30), 2.5);
            input.Cancel();
            Equal<TouchGesture?>(null, input.End(new(80, 90), 3), "cancelled hold cannot act");
            return 0;
        });
    }

    sealed class ScrollProbe(Module module) : Container(module)
    {
        public Vector2 Scrolled;
        public Vector2f Released;
        public override bool OnTouchScroll(Vector2 position, Vector2 delta)
        {
            if (!new Rect(10, 20, 80, 60).PointInside(position)) return false;
            Scrolled += delta;
            return true;
        }

        public override void OnTouchScrollEnd(Vector2 position, Vector2f velocity) =>
            Released = velocity;
    }

    sealed class DragProbe(Module module) : Window(module)
    {
        public Vector2 Dragged;
        public override void OnTouchDrag(Vector2 delta) => Dragged += delta;
    }

    sealed class HoldCancellationProbe(Module module, System.Action<bool> end) : Window(module)
    {
        public override bool ContinuesTouchHold => true;
        public override void OnTouchHoldEnd(Vector2 position, bool cancelled) => end(cancelled);
    }

    sealed class FixedTarget(Module module) : Window(module)
    {
        public override bool IsTouchTarget => true;
    }
}
