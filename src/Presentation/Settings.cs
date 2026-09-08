using Godot;

namespace WarGame.Presentation;

/// <summary>Preferências do jogador que não são do mundo: ficam em user://settings.cfg e sobrevivem a um
/// jogo novo, porque não são do jogo — são do ecrã de quem joga.
///
/// A primeira delas é o tamanho da interface. O jogo desenha-se numa tela de 1152×648 esticada para o
/// ecrã; num telemóvel em pé isso dá letra de 17 px de aparelho, que é a letra de um relógio e não a de um
/// painel com quinze linhas de divisões. O factor de escala da janela multiplica tudo ao mesmo tempo —
/// letra, chapas, molduras e a altura do dedo — sem se mexer em nenhum ecrã.
///
/// A segunda é onde ficou a faixa de alarmes. Num ecrã de telemóvel não há canto que esteja sempre livre:
/// ao centro tapa as notificações, à direita tapava os painéis. Quem joga é que sabe o que quer ver, por
/// isso arrasta-a e o sítio fica guardado aqui, com a faixa aberta ou dobrada.</summary>
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
        Write("ui", "scale", _scaleIndex);
        Apply(window);
    }

    private static bool _alertsRead;
    private static Vector2? _alertSpot;
    private static bool _alertFolded;

    /// <summary>Canto onde o jogador arrumou a faixa de alarmes, ou null enquanto nunca lhe tocou — e aí é
    /// o Hud que a põe por baixo da barra de topo.</summary>
    public static Vector2? AlertSpot { get { ReadAlerts(); return _alertSpot; } }

    /// <summary>A faixa ficou dobrada até ao título.</summary>
    public static bool AlertFolded { get { ReadAlerts(); return _alertFolded; } }

    public static void SetAlertSpot(Vector2 spot)
    {
        ReadAlerts();
        _alertSpot = spot;
        Write("alerts", "spot", spot);
    }

    public static void SetAlertFolded(bool folded)
    {
        ReadAlerts();
        _alertFolded = folded;
        Write("alerts", "folded", folded);
    }

    private static void ReadAlerts()
    {
        if (_alertsRead) return;
        _alertsRead = true;
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        if (cfg.HasSectionKey("alerts", "spot")) _alertSpot = cfg.GetValue("alerts", "spot").AsVector2();
        _alertFolded = cfg.GetValue("alerts", "folded", false).AsBool();
    }

    private static bool _pauseRead;
    private static bool _pauseOnEvent = true;

    /// <summary>Parar o relógio quando um acontecimento abre o cartão. Vem ligado: um cartão que pede uma
    /// decisão com o mundo a andar por trás é uma decisão tomada à pressa — e num telemóvel, com o dedo
    /// ainda a arrastar o mapa, é uma decisão tomada por engano. Quem gosta do mundo sempre a andar
    /// desliga-o aqui e o cartão passa a abrir sem tocar no relógio.</summary>
    public static bool PauseOnEvent
    {
        get
        {
            if (!_pauseRead)
            {
                _pauseRead = true;
                var cfg = new ConfigFile();
                if (cfg.Load(Path) == Error.Ok) _pauseOnEvent = cfg.GetValue("jogo", "pausa_evento", true).AsBool();
            }
            return _pauseOnEvent;
        }
    }

    public static void SetPauseOnEvent(bool on)
    {
        _pauseRead = true;
        _pauseOnEvent = on;
        Write("jogo", "pausa_evento", on);
    }

    /// <summary>Grava uma preferência sem deitar fora as outras: o ficheiro é lido antes de se lhe mexer.</summary>
    private static void Write(string section, string key, Variant value)
    {
        var cfg = new ConfigFile();
        cfg.Load(Path);                       // não deitar fora o que lá estiver de outras preferências
        cfg.SetValue(section, key, value);
        cfg.Save(Path);
    }

    /// <summary>Põe a janela no tamanho escolhido. É chamado no arranque e sempre que se muda de degrau.</summary>
    public static void Apply(Window? window)
    {
        if (window is not null) window.ContentScaleFactor = Scale;
    }

    /// <summary>Telemóvel e tablet começam maiores; no computador o rato acerta em tudo e cabe mais no ecrã.</summary>
    private static int Default() => OS.HasFeature("mobile") ? 2 : 0;
}
