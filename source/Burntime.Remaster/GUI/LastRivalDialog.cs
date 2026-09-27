using System.Linq;
using Burntime.Framework;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.GUI;

internal sealed class LastRivalDialog : DialogWindow
{
    readonly BurntimeClassic classic;
    Player? winner;

    internal LastRivalDialog(BurntimeClassic app) : base(app)
    {
        classic = app;
        PlayMusic = false;
        Layer = 70;
        Hide();
        WindowHide += (_, _) => ResolveChoice();
    }

    internal void Show(Player player)
    {
        ClassicGame game = classic.Game;
        winner = player;
        Position = (Parent.Size - Size) / 2;
        TextHelper text = new(classic, "last_rival");
        text.AddArgument("{total}", game.World.Locations.Count(location => location.IsCity));
        Conversation conversation = new()
        {
            Text = [text.Get(0), text.Get(1), text.Get(2)],
            Choices =
            [
                new() { Text = text.Get(3), Action = new(ConversationActionType.Yes) },
                new() { Text = text.Get(4), Action = new(ConversationActionType.No) },
                new()
            ]
        };
        SetCharacter(player.Character, conversation, textLinesPerPage: 3);
        Show();
    }

    public override void OnResizeScreen(bool reload = false)
    {
        base.OnResizeScreen(reload);
        if (Parent != null)
            Position = (Parent.Size - Size) / 2;
    }

    void ResolveChoice()
    {
        Player? player = winner;
        winner = null;
        if (player != null && Result == ConversationActionType.Yes)
        {
            classic.Game.World.VictoryCondition.Object.AcceptLastRivalVictory(player);
            classic.Server.CheckVictory();
        }
        classic.SceneManager.SetScene("WaitScene");
    }
}
