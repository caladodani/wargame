using Godot;

namespace WarGame.Presentation;

/// <summary>O quadro de fábricas de uma encomenda — o cursor de linhas de produção do HoI4, feito de chapas
/// de fábrica em vez de um cursor. Cada chapa é uma fábrica militar do país: as acesas em latão trabalham
/// nesta encomenda, as apagadas estão livres e as vermelhas estão a servir outra encomenda da fila. Clicar
/// na chapa nº 3 dedica três fábricas a esta encomenda; clicar na primeira devolve-a a uma.
///
/// Antes disto as fábricas eram invisíveis e automáticas: as primeiras encomendas da fila ficavam com uma
/// cada e o jogador não tinha maneira nenhuma de concentrar o país inteiro na coluna blindada de que
/// precisava para a semana. Agora vê-se onde está cada fábrica e move-se com o dedo.
///
/// Só sabe de contas; quem despacha o comando é o painel.</summary>
public partial class FactoryDial : HBoxContainer
{
    private Action<int>? _onSet;
    private readonly List<Button> _slots = new();

    /// <param name="assigned">Fábricas dedicadas a esta encomenda.</param>
    /// <param name="cap">Tecto: o que o país tem, sem passar a regra order_factories_max.</param>
    /// <param name="spare">Fábricas livres que esta encomenda ainda podia apanhar sem tirar a ninguém.</param>
    public void Bind(int assigned, int cap, int spare, Action<int> onSet)
    {
        _onSet = onSet;
        Ui.Clear(this);
        _slots.Clear();
        AddThemeConstantOverride("separation", 2);
        for (int i = 0; i < cap; i++)
        {
            int n = i + 1;
            bool on = n <= assigned, idle = n <= assigned + spare;
            var b = new Button { Text = "🏭", Flat = true, CustomMinimumSize = new Vector2(30, 30) };
            b.AddThemeFontSizeOverride("font_size", 15);
            b.AddThemeColorOverride("font_color", on ? Ui.Accent : idle ? Ui.TextDim : Ui.Danger.Darkened(0.2f));
            b.AddThemeColorOverride("font_hover_color", Ui.Accent);
            b.TooltipText = n == assigned ? "as fábricas que esta encomenda já leva"
                : idle ? (n == 1 ? "uma só fábrica nesta encomenda" : $"dedicar {n} fábricas a esta encomenda")
                : $"dedicar {n} fábricas — tira-as às encomendas de baixo";
            b.Pressed += () => _onSet?.Invoke(n);
            AddChild(b);
            _slots.Add(b);
        }
    }

    /// <summary>--smoke: carrega na chapa nº n sem dedo nenhum.</summary>
    public bool Smoke(int n)
    {
        if (n < 1 || n > _slots.Count) return false;
        _slots[n - 1].EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }
}
