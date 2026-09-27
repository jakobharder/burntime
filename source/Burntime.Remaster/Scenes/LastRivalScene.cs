using Burntime.Framework;
using Burntime.Remaster.GUI;

namespace Burntime.Remaster.Scenes;

// WaitScene stays behind the transition fade; the choice needs a visible scene.
class LastRivalScene : Scene, IMapMusicContinuationScene
{
    readonly LastRivalDialog dialog;

    public LastRivalScene(Module app) : base(app)
    {
        Windows += dialog = new LastRivalDialog((BurntimeClassic)app);
    }

    public override void OnResizeScreen(bool reload = false)
    {
        Size = app.Engine.Resolution.Game;
        base.OnResizeScreen(reload);
    }

    protected override void OnActivateScene(object parameter)
    {
        app.RenderMouse = true;
        dialog.Show(((BurntimeClassic)app).Game.World.ActivePlayerObj);
    }
}
