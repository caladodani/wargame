using Godot;

namespace WarGame.Presentation;

public partial class Hud : CanvasLayer
{
    private Label _date = null!;

    public override void _Ready()
    {
        _date = GetNode<Label>("Top/Date");
        var game = GetNode<Game>("/root/Game");
        game.TickCompleted += _ => _date.Text = game.World.Clock.Date.ToString("yyyy-MM-dd");
        GetNode<Button>("Top/Pause").Pressed += () => game.World.Clock.Speed = game.World.Clock.Speed == 0 ? 1 : 0;
        GetNode<Button>("Top/Faster").Pressed += () => game.World.Clock.Speed = Mathf.Min(4, game.World.Clock.Speed + 1);
        GetNode<Button>("Top/Slower").Pressed += () => game.World.Clock.Speed = Mathf.Max(0, game.World.Clock.Speed - 1);
    }
}
