using Burntime.Framework.GUI;

namespace Burntime.Framework;

public abstract class Scene : Container
{
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
