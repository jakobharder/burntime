using Burntime.Framework.GUI;
using Burntime.Platform;

namespace Burntime.Framework;

public interface IMapMusicContinuationScene { }

// Temporarily replaces map music, then restores it when returning to a map.
public interface IMapMusicInterruptionScene { }

// A directly navigable map screen. Unlike WaitScene, transitions between these
// scenes should not fade music when map music is enabled.
public interface IMapNavigationScene : IMapMusicContinuationScene { }

// A transient scene that bridges one visible scene to another while the
// existing fade remains fully out.
public interface ISceneTransitionBridge { }

public abstract class Scene : Container
{
    public virtual bool UseCardinalGamepadMovement => false;
    public virtual bool UseDiagonalGamepadNavigation => false;
    public virtual Key PreferredPrimaryKeyboardControl => new(' ');

    public string? Music { get; set; }
    public bool MusicLoop { get; set; } = true;
    public bool KeepMusic { get; set; } = false;

    public Scene(Module app)
        : base(app)
    {
        Layer = 0;
        HasFocus = true;
    }

    internal void ActivateScene(object? parameter = null)
    {
        OnResizeScreen();
        OnActivateScene(parameter);

        foreach (var window in Windows)
            window.OnActivate();

        if (!KeepMusic)
            app.Engine.Music.Stop();
        if (!string.IsNullOrEmpty(Music))
        {
            if (MusicLoop)
                app.Engine.Music.Play(Music);
            else 
                app.Engine.Music.PlayOnce(Music);
        }
    }

    internal void InactivateScene() => OnInactivateScene();

    protected virtual void OnActivateScene(object? parameter) { }
    protected virtual void OnInactivateScene() { }
}
