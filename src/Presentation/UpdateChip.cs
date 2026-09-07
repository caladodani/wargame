using Godot;
using WarGame.Core.Update;

namespace WarGame.Presentation;

/// <summary>A chapa da actualização: pergunta ao servidor que versão há, e se houver uma mais nova do que a
/// instalada começa logo a descarregá-la — sem perguntar nada, porque a resposta era sempre sim.
///
/// O jogo vive fora da loja: o APK está em vilarongacalado.com/downloads e até aqui a única maneira de
/// apanhar uma versão nova era ir ao site buscá-la à mão, o que quer dizer que ninguém a apanhava. Agora,
/// no arranque, lê-se o manifesto (wargame.json, publicado ao lado do APK), compara-se com a versão desta
/// compilação e, se for caso disso, o APK começa a vir para user://updates/ com a barra a andar na barra de
/// cima. Quando acaba, a chapa passa a "Instalar" e o toque entrega o ficheiro ao Android — que pede a
/// confirmação dele, como pede a qualquer instalação de fora da loja.
///
/// A chapa só aparece quando há alguma coisa para dizer: sem versão nova, é como se não existisse.</summary>
public partial class UpdateChip : PanelContainer
{
    /// <summary>Onde se pergunta o que há. Escrito pela publicação, ao lado do APK.</summary>
    public const string ManifestUrl = "https://vilarongacalado.com/downloads/wargame.json";
    private const string Folder = "user://updates";

    /// <summary>Em que ponto está a actualização — é o que a chapa desenha.</summary>
    public enum Phase { Quiet, Asking, Downloading, Ready, Failed }

    private Game _game = null!;
    private HttpRequest _ask = null!, _fetch = null!;
    private Label _text = null!;
    private ProgressBar _bar = null!;
    private Button _act = null!;

    public Phase State { get; private set; } = Phase.Quiet;
    public UpdateInfo? Found { get; private set; }
    private string _file = "";

    /// <summary>A versão desta compilação (application/config/version do project.godot).</summary>
    public static string Installed => ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString();

    public void Setup(Game game)
    {
        _game = game;
        Visible = false;
        AddThemeStyleboxOverride("panel", Ui.Box(Ui.Accent with { A = 0.16f }, 4));

        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6); AddChild(row);
        var icon = Ui.Lbl("⬇", 16);
        icon.AddThemeColorOverride("font_color", Ui.Accent);
        row.AddChild(icon);
        var cell = new VBoxContainer(); cell.AddThemeConstantOverride("separation", 1); row.AddChild(cell);
        _text = Ui.Lbl("", 13);
        _text.AddThemeColorOverride("font_color", Ui.Text);
        cell.AddChild(_text);
        _bar = new ProgressBar { CustomMinimumSize = new Vector2(120, 6), ShowPercentage = false, MaxValue = 100 };
        _bar.AddThemeStyleboxOverride("background", Ui.Box(Ui.Ink, 2));
        _bar.AddThemeStyleboxOverride("fill", Ui.Box(Ui.Accent, 2));
        cell.AddChild(_bar);
        _act = Ui.Btn("Instalar", Install, 0, Ui.Kind.Primary);
        _act.Visible = false;
        row.AddChild(_act);

        _ask = new HttpRequest { UseThreads = true };
        AddChild(_ask);
        _ask.RequestCompleted += OnManifest;
        _fetch = new HttpRequest { UseThreads = true };
        AddChild(_fetch);
        _fetch.RequestCompleted += OnDownloaded;
    }

    /// <summary>Pergunta ao servidor. No arranque de prova não se toca na rede.</summary>
    public void Check()
    {
        if (_game.IsSmoke || State is Phase.Asking or Phase.Downloading) return;
        State = Phase.Asking;
        if (_ask.Request(ManifestUrl) != Error.Ok) State = Phase.Quiet;
    }

    private void OnManifest(long result, long code, string[] headers, byte[] body)
    {
        // sem rede não se diz nada ao jogador: não é um erro dele, e o jogo não depende disto
        if (result != (long)HttpRequest.Result.Success || code != 200) { State = Phase.Quiet; Paint(); return; }
        Found = UpdateCheck.Parse(System.Text.Encoding.UTF8.GetString(body));
        if (!UpdateCheck.ShouldDownload(Installed, Found)) { State = Phase.Quiet; Paint(); return; }
        Start();
    }

    /// <summary>Começa (ou salta) a descarga. Um ficheiro já cá em baixo e inteiro não se puxa outra vez.</summary>
    private void Start()
    {
        if (Found is not UpdateInfo info) return;
        DirAccess.MakeDirRecursiveAbsolute(Folder);
        _file = $"{Folder}/wargame-{info.Version}.apk";
        if (Godot.FileAccess.FileExists(_file))
        {
            using var have = Godot.FileAccess.Open(_file, Godot.FileAccess.ModeFlags.Read);
            if (have is not null && UpdateCheck.IsComplete(info, (long)have.GetLength())) { State = Phase.Ready; Paint(); return; }
        }
        _fetch.DownloadFile = ProjectSettings.GlobalizePath(_file);
        State = Phase.Downloading;
        Paint();
        if (_fetch.Request(info.Url) != Error.Ok) { State = Phase.Failed; Paint(); }
    }

    private void OnDownloaded(long result, long code, string[] headers, byte[] body)
    {
        if (result != (long)HttpRequest.Result.Success || code != 200 || Found is null) { State = Phase.Failed; Paint(); return; }
        using var got = Godot.FileAccess.Open(_file, Godot.FileAccess.ModeFlags.Read);
        long size = got is null ? 0 : (long)got.GetLength();
        State = UpdateCheck.IsComplete(Found, size) ? Phase.Ready : Phase.Failed;
        Paint();
        if (State == Phase.Ready) _game.Notify($"Versão {Found.Version} descarregada — toca em Instalar");
    }

    /// <summary>Entrega o APK ao Android. A instalação em si é dele: pede a confirmação do dono do telefone
    /// e a permissão de instalar de fora da loja. No computador não há nada para instalar — abre-se a página.</summary>
    private void Install()
    {
        if (State != Phase.Ready || Found is null) { if (Found is not null) OS.ShellOpen(Found.Url); return; }
        OS.ShellOpen(ProjectSettings.GlobalizePath(_file));
        _game.Notify("A instalação é do Android: confirma no ecrã dele");
    }

    public override void _Process(double delta)
    {
        if (State != Phase.Downloading) return;
        long size = _fetch.GetBodySize(), got = _fetch.GetDownloadedBytes();
        if (size <= 0 && Found is not null) size = Found.Size;
        _bar.Value = size > 0 ? Mathf.Clamp(100.0 * got / size, 0, 100) : 0;
        _text.Text = $"{Found?.Version} · {got / 1_048_576.0:0.#}/{size / 1_048_576.0:0.#} MB";
        Visible = true;
    }

    private void Paint()
    {
        Visible = State != Phase.Quiet;
        _act.Visible = State == Phase.Ready;
        _bar.Visible = State == Phase.Downloading;
        _text.Text = State switch
        {
            Phase.Downloading => $"{Found?.Version} a descarregar",
            Phase.Ready => $"versão {Found?.Version} pronta",
            Phase.Failed => "descarga falhada",
            _ => "",
        };
        TooltipText = Found is null ? "" : $"{Found.Notes}\ninstalada {Installed}, no servidor {Found.Version}";
    }

    /// <summary>--smoke: sem rede. Passa a chapa pelos estados com um manifesto de mentira e devolve o que
    /// ficou escrito, para a linha de prova dizer que a actualização se desenha.</summary>
    public string Smoke()
    {
        string installed = Installed;
        Found = UpdateCheck.Parse($$"""
            {"version":"99.0.0","code":99,"apk":"https://vilarongacalado.com/downloads/wargame.apk",
             "size":42,"notes":"versão de prova"}
            """);
        State = Phase.Downloading; Paint();
        string mid = _text.Text;
        State = Phase.Ready; Paint();
        string end = _text.Text + (_act.Visible ? " + Instalar" : "");
        bool wants = UpdateCheck.ShouldDownload(installed, Found);
        State = Phase.Quiet; Found = null; Paint();
        return $"instalada {installed}, {(wants ? "puxaria" : "ignoraria")} a 99.0.0 ({mid} → {end})";
    }
}
