using Godot;

namespace WarGame.Presentation;

/// <summary>Preferências do jogador que não são do mundo: ficam em user://settings.cfg e sobrevivem a um
/// jogo novo, porque não são do jogo — são do ecrã de quem joga.
///
/// A primeira delas é o tamanho da interface. O jogo desenha-se numa tela de 1152×648 esticada para o
/// ecrã; num telemóvel em pé isso dá letra de 17 px de aparelho, que é a letra de um relógio e não a de um
/// painel com quinze linhas de divisões. O factor de escala da janela multiplica tudo ao mesmo tempo —
/// letra, chapas, molduras e a altura do dedo — sem se mexer em nenhum ecrã.</summary>
public static class Settings
{
    private const string Path = "user://settings.cfg";

    /// <summary>Os degraus de tamanho, do que cabe mais ao que se lê melhor.</summary>
    public static readonly float[] Scales = { 1.0f, 1.2f, 1.35f, 1.55f };
    public static readonly string[] ScaleNames = { "Compacto", "Normal", "Grande", "Enorme" };

    private static int _scaleIndex = -1;

    /// <summary>Degrau escolhido. No telemóvel começa em "Grande": é onde o pedido nasceu.</summary>
    public static int ScaleIndex
    {
        get
        {
            if (_scaleIndex < 0)
            {
                var cfg = new ConfigFile();
                _scaleIndex = cfg.Load(Path) == Error.Ok
                    ? (int)cfg.GetValue("ui", "scale", Default())
                    : Default();
                _scaleIndex = Mathf.Clamp(_scaleIndex, 0, Scales.Length - 1);
            }
            return _scaleIndex;
        }
    }

    public static float Scale => Scales[ScaleIndex];
    public static string ScaleName => ScaleNames[ScaleIndex];

    /// <summary>Guarda o degrau e aplica-o já à janela.</summary>
    public static void SetScale(int index, Window? window)
    {
        _scaleIndex = Mathf.Clamp(index, 0, Scales.Length - 1);
        var cfg = new ConfigFile();
        cfg.Load(Path);                       // não deitar fora o que lá estiver de outras preferências
        cfg.SetValue("ui", "scale", _scaleIndex);
        cfg.Save(Path);
        Apply(window);
    }

    /// <summary>Põe a janela no tamanho escolhido. É chamado no arranque e sempre que se muda de degrau.</summary>
    public static void Apply(Window? window)
    {
        if (window is not null) window.ContentScaleFactor = Scale;
    }

    /// <summary>Telemóvel e tablet começam maiores; no computador o rato acerta em tudo e cabe mais no ecrã.</summary>
    private static int Default() => OS.HasFeature("mobile") ? 2 : 0;
}
