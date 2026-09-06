using Godot;

namespace WarGame.Presentation;

/// <summary>Menu de jogo (botão ☰ da barra): guardar, trocar de slot, escolher dificuldade, recomeçar e
/// sair. A dificuldade vem da tabela difficulty e reescreve regras por World.ApplyDifficulty — muda a
/// meio do jogo e vale já no dia seguinte. Recomeçar e sair passam por confirmação.</summary>
public partial class GameMenu : PanelContainer
{
    private Game _game = null!;
    private VBoxContainer _body = null!;
    private ConfirmationDialog _confirmNew = null!, _confirmQuit = null!;
    private Action _openSlots = null!;

    public void Setup(Game game, Action openSlots)
    {
        _game = game; _openSlots = openSlots;
        Visible = false;
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.07f, 0.08f, 0.11f, 0.97f), 16));

        _confirmNew = Ui.Dialog(this, () => { Close(); _game.NewGame(); });
        _confirmNew.DialogText = "Começar um jogo novo? O jogo actual perde-se.";
        _confirmQuit = Ui.Dialog(this, () => { _game.Save(); GetTree().Quit(); });
        _confirmQuit.DialogText = "Guardar e sair do jogo?";

        _body = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        _body.AddThemeConstantOverride("separation", 8);
        AddChild(_body);
    }

    public void Open() { Fill(); Visible = true; Ui.FadeIn(this); }
    public void Close() => Visible = false;
    public void Toggle() { if (Visible) Close(); else Open(); }

    private void Fill()
    {
        Ui.Clear(_body);
        _body.AddChild(Ui.Lbl("Menu", 26));

        _body.AddChild(Ui.Btn("Continuar", Close));
        _body.AddChild(Ui.Btn("Guardar jogo", () => { _game.Save(); _game.Notify("Jogo guardado"); Close(); }));
        _body.AddChild(Ui.Btn("Jogos guardados", () => { Close(); _openSlots(); }));

        var w = _game.World;
        if (w.DifficultyDefs.Count > 0)
        {
            _body.AddChild(Ui.Lbl("Dificuldade", 20));
            string current = w.Difficulty ?? "normal";
            foreach (var def in w.DifficultyDefs.Values.OrderBy(d => d.Sort))
            {
                bool active = def.Id == current;
                var b = Ui.Btn((active ? "● " : "○ ") + def.Name, () => SetDifficulty(def.Id));
                b.Disabled = active;
                _body.AddChild(b);
            }
            _body.AddChild(Ui.Lbl("Muda a produção, o rendimento, os homens por mês e a folga da IA.", 14));
        }

        _body.AddChild(Ui.Btn("Recomeçar", () => _confirmNew.PopupCentered()));
        _body.AddChild(Ui.Btn("Sair do jogo", () => _confirmQuit.PopupCentered()));
    }

    /// <summary>Troca de nível a meio do jogo: aplica as regras novas e guarda para não se perder.</summary>
    private void SetDifficulty(string id) => _game.RunWhenIdle(() =>
    {
        _game.World.ApplyDifficulty(id);
        _game.Save();
        _game.Notify($"Dificuldade: {_game.World.DifficultyDefs[id].Name}");
        Fill();
    });
}
