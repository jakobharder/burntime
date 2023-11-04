using Burntime.Framework.GUI;

namespace Burntime.Framework;

public abstract class Scene : Container
{
    public string? Music { get; set; }
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
            app.Engine.Music.Play(Music);
    }

    internal void InactivateScene() => OnInactivateScene();

    protected virtual void OnActivateScene(object? parameter) { }
    protected virtual void OnInactivateScene() { }
}
