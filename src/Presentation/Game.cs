using Godot;
using FileAccess = Godot.FileAccess;
using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using WarGame.Data;

namespace WarGame.Presentation;

/// <summary>Autoload. Dono do World; corre o tick em background e emite sinal para a UI.
/// Contrato com a apresentação: o World só se lê em TickCompleted/StateChanged ou via RunWhenIdle,
/// e só se muta por Dispatch (excepção: Clock.Speed). Save em user://save.db.</summary>
public partial class Game : Node
{
    [Signal] public delegate void TickCompletedEventHandler(int day);
    /// <summary>Um comando aplicado com sucesso (imediato ou saído da fila): a UI deve reler o estado.</summary>
    [Signal] public delegate void StateChangedEventHandler();
    /// <summary>Erro de comando ou aviso curto para o jogador (toast no Hud).</summary>
    [Signal] public delegate void CommandFailedEventHandler(string msg);

    public World World { get; private set; } = null!;
    public CommandDispatcher Commands { get; private set; } = new();
    public SqlWorldRepository WorldRepo { get; private set; } = null!;
    public IDatabase StaticDb => _static;
    /// <summary>País com IsPlayer, ou null enquanto não há jogador. Cache refeita após cada comando e no load.</summary>
    public int? PlayerId { get; private set; }

    /// <summary>Slot de gravação actual (1..3); o 1 usa o save.db histórico.</summary>
    public int Slot { get; private set; } = 1;
    private string SavePath => Slot <= 1 ? "user://save.db" : $"user://save{Slot}.db";
    public const int SlotCount = 3;
    private const int AutoSaveDays = 30;
    private static readonly double[] SpeedSeconds = { 0, 2.0, 1.0, 0.5, 0.25 };

    private IDatabase _static = null!;
    private IDatabase? _save;
    private double _accum;
    private bool _ticking;
    private Task? _tickTask;
    private int _gen;                                   // sobe em BuildWorld: fins de tick do mundo antigo são ignorados
    private readonly Queue<Action> _pending = new();    // corre na main thread quando o tick acaba, antes de TickCompleted
    private int _lastSaveDay;
    private bool _smoke;

    public override void _Ready()
    {
        // static.db é só leitura; em Android o res:// não é acessível ao SQLite → copiar para user:// no 1º arranque.
        var path = EnsureUserCopy("res://data/static.db", "user://static.db");
        _static = new GdSqliteDatabase(path, readOnly: true);
        _smoke = OS.GetCmdlineUserArgs().Contains("--smoke");
        BuildWorld();
        if (_smoke) Smoke();
    }

    /// <summary>Mundo a partir de static.db + save (se houver um válido). Chamado no arranque e em NewGame.</summary>
    private void BuildWorld()
    {
        _gen++; _ticking = false; _tickTask = null; _pending.Clear(); _accum = 0;
        Fresh();
        OpenSave();
        try
        {
            if (_save is not null && SqlWorldRepository.HasSave(_save)) WorldRepo.LoadSave(World, _save);
            else { WorldRepo.LoadStartArmies(World); WorldRepo.LoadStartWars(World); }
        }
        catch (Exception ex)
        {
            GD.PushError("Save ilegível, a começar de novo: " + ex.Message);
            Fresh(); WorldRepo.LoadStartArmies(World); WorldRepo.LoadStartWars(World);
        }

        // Ordem do tick — única fonte de verdade.
        World.Register(new SupplySystem());
        World.Register(new TradeSystem());
        World.Register(new ResourceSystem());
        World.Register(new EconomySystem());
        World.Register(new StabilitySystem());
        World.Register(new ManpowerSystem());
        World.Register(new ProductionSystem());
        World.Register(new ResearchSystem());
        World.Register(new FocusSystem());
        World.Register(new NewsSystem());
        World.Register(new WarStatsSystem());   // antes do combate: liga-se aos eventos que vai contar
        World.Register(new MovementSystem());
        World.Register(new CombatSystem());
        World.Register(new PeaceSystem());
        World.Register(new TruceSystem());
        World.Register(new ConstructionSystem());
        World.Register(new ResistanceSystem());
        World.Register(new IntegrationSystem());
        World.Register(new InfrastructureRepairSystem());
        World.Register(new AutoFrontSystem());
        World.Register(new EspionageSystem());
        World.Register(new VictorySystem());
        World.Register(new DiplomacySystem());
        World.Register(new RecoverySystem());
        World.Register(new DecisionSystem());
        World.Register(new HistorySystem());
        World.Register(new AiSystem());

        RefreshPlayer();
        World.Clock.Speed = PlayerId is null ? 0 : 1;
        _lastSaveDay = World.Clock.Day;
    }

    private void Fresh()
    {
        var units = new SqlUnitRepository(_static);
        World = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units), new ModifierEngine(units.GetModifiers()));
        Commands = new CommandDispatcher();
        WorldRepo = new SqlWorldRepository(_static);
        WorldRepo.LoadStatic(World);
    }

    /// <summary>Abre (ou cria) user://save.db e garante as tabelas. Sem save o jogo continua, só não guarda.</summary>
    private void OpenSave()
    {
        _save?.Dispose(); _save = null;
        try
        {
            _save = new GdSqliteDatabase(ProjectSettings.GlobalizePath(SavePath), readOnly: false);
            SqlWorldRepository.EnsureSaveSchema(_save, SchemaSql());
        }
        catch (Exception ex) { GD.PushError("Sem save: " + ex.Message); _save?.Dispose(); _save = null; }
    }

    /// <summary>schema.sql do projecto; no APK os *.sql ficam de fora do export, por isso recai no
    /// sqlite_master do static.db (criado com o mesmo schema). O SQLite guarda o CREATE sem IF NOT EXISTS — repõe-se.</summary>
    private string SchemaSql()
    {
        const string res = "res://data/schema.sql";
        if (FileAccess.FileExists(res)) return FileAccess.GetFileAsString(res);
        var stmts = _static.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "));
        return string.Join(";\n", stmts) + ";\n";
    }

    /// <summary>Escreve o save. Se um tick estiver a correr, espera pela Task — o World fica coerente.</summary>
    public void Save()
    {
        if (World is null) return;
        try
        {
            WaitTick();
            if (_save is null) OpenSave();
            if (_save is null) { EmitSignal(SignalName.CommandFailed, "Não foi possível guardar"); return; }
            try { WorldRepo.WriteSave(World, _save); }
            catch { try { _save.Execute("ROLLBACK"); } catch { /* sem transacção aberta */ } throw; }
            _lastSaveDay = World.Clock.Day;
        }
        catch (Exception ex) { GD.PushError("Save: " + ex); EmitSignal(SignalName.CommandFailed, "Erro ao guardar: " + ex.Message); }
    }

    /// <summary>Apaga o save, reconstrói o mundo e recarrega a cena (MapView/Hud/RegionRenderer refazem-se no _Ready).</summary>
    public void NewGame()
    {
        try
        {
            WaitTick();
            _save?.Dispose(); _save = null;
            foreach (var suffix in new[] { "", "-journal", "-wal", "-shm" })
                if (FileAccess.FileExists(SavePath + suffix)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath + suffix));
            BuildWorld();
            GetTree().ReloadCurrentScene();
        }
        catch (Exception ex) { GD.PushError("NewGame: " + ex); }
    }

    /// <summary>Muda de slot: guarda o actual, aponta para o novo e reconstrói o mundo
    /// (slot vazio cai na escolha de país). Recarrega a cena como o NewGame.</summary>
    public void SwitchSlot(int slot)
    {
        if (slot == Slot || slot < 1 || slot > SlotCount) return;
        try
        {
            Save();
            WaitTick();
            _save?.Dispose(); _save = null;
            Slot = slot;
            BuildWorld();
            GetTree().ReloadCurrentScene();
        }
        catch (Exception ex) { GD.PushError("SwitchSlot: " + ex); }
    }

    /// <summary>Dia guardado num slot, ou null se vazio/ilegível. Não mexe no slot actual.</summary>
    public int? SlotDay(int slot)
    {
        string path = slot <= 1 ? "user://save.db" : $"user://save{slot}.db";
        if (!FileAccess.FileExists(path)) return null;
        try
        {
            using var db = new GdSqliteDatabase(ProjectSettings.GlobalizePath(path), readOnly: true);
            var rows = db.Query("SELECT value FROM save_meta WHERE key='day'");
            return rows.Count > 0 ? Convert.ToInt32(rows[0]["value"]) : null;
        }
        catch { return null; }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationPaused || what == NotificationWMCloseRequest || what == NotificationWMGoBackRequest) Save();
    }

    public override void _ExitTree() { _save?.Dispose(); _save = null; }

    public override void _Process(double delta)
    {
        if (World.Clock.Paused || _ticking) return;
        _accum += delta;
        if (_accum < SpeedSeconds[Mathf.Clamp(World.Clock.Speed, 0, SpeedSeconds.Length - 1)]) return;
        _accum = 0; _ticking = true;
        int gen = _gen;
        var t = Task.Run(() => World.Tick());
        _tickTask = t;
        t.ContinueWith(_ => Callable.From(() => OnTickDone(gen)).CallDeferred());
    }

    private void OnTickDone(int gen)
    {
        if (gen != _gen) return;
        _ticking = false;
        if (_tickTask is { IsFaulted: true } t)
        {
            GD.PushError("Tick: " + t.Exception?.InnerException);
            World.Clock.Speed = 0;
            EmitSignal(SignalName.CommandFailed, "Erro na simulação — jogo em pausa");
        }
        while (_pending.Count > 0) Safe(_pending.Dequeue());
        if (World.Clock.Day - _lastSaveDay >= AutoSaveDays) Save();
        EmitSignal(SignalName.TickCompleted, World.Clock.Day);
        if (_smoke && World.Clock.Day >= 6) { Save(); GD.Print($"smoke: dia {World.Clock.Day} guardado, a sair"); GetTree().Quit(); }
    }

    /// <summary>Aplica o comando já (mundo parado) e devolve o erro. Com tick a correr fica em fila, devolve null
    /// e o erro, se houver, sai por CommandFailed. Sucesso emite StateChanged.</summary>
    public string? Dispatch(ICommand c)
    {
        if (!_ticking) return Apply(c);
        _pending.Enqueue(() => { var e = Apply(c); if (e is not null) EmitSignal(SignalName.CommandFailed, e); });
        return null;
    }

    private string? Apply(ICommand c)
    {
        string? err;
        try { err = Commands.Dispatch(World, c); }
        catch (Exception ex) { GD.PushError("Comando: " + ex); return "Erro interno: " + ex.Message; }
        if (err is null) { RefreshPlayer(); EmitSignal(SignalName.StateChanged); }
        return err;
    }

    /// <summary>Corre `a` na main thread com o World parado: já, ou no fim do tick actual (antes de TickCompleted).</summary>
    public void RunWhenIdle(Action a) { if (_ticking) _pending.Enqueue(a); else Safe(a); }

    /// <summary>Aviso curto para o jogador (toast do Hud).</summary>
    public void Notify(string msg) => EmitSignal(SignalName.CommandFailed, msg);

    private void RefreshPlayer() => PlayerId = World.Countries.Values.FirstOrDefault(c => c.IsPlayer)?.Id;

    private void WaitTick()
    {
        try { _tickTask?.Wait(); }
        catch (AggregateException ex) { GD.PushError("Tick: " + ex.InnerException); }
    }

    private static void Safe(Action a) { try { a(); } catch (Exception ex) { GD.PushError(ex.ToString()); } }

    /// <summary>`godot --headless --path . -- --smoke`: escolhe o país com mais divisões, corre a 4× e guarda ao dia 6.
    /// Verificação sem ecrã dos caminhos tick → fila → save; o Hud abre os painéis no 1º tick.</summary>
    private void Smoke()
    {
        GD.Print($"smoke: dia {World.Clock.Day}, jogador {PlayerId?.ToString() ?? "nenhum"}, {World.Divisions.Count} divisões");
        if (PlayerId is null)
        {
            int best = World.Countries.Values.FirstOrDefault(c => c.Tag == "PRT")?.Id
                       ?? World.Divisions.Values.GroupBy(d => d.CountryId).OrderByDescending(g => g.Count()).First().Key;
            var err = Dispatch(new ChoosePlayerCommand(best));
            if (err is not null) GD.PushError("smoke: " + err);
        }
        World.Clock.Speed = 4;
    }

    // Copia res:// → user:// na 1ª execução e sempre que a base do pacote mudar (um APK
    // novo por cima do antigo ficava com o mapa velho). O md5 da última cópia fica ao lado.
    private static string EnsureUserCopy(string res, string user)
    {
        var abs = ProjectSettings.GlobalizePath(user);
        var stamp = user + ".md5";
        var md5 = FileAccess.GetMd5(res);
        var last = FileAccess.FileExists(stamp) ? FileAccess.GetFileAsString(stamp) : "";
        if (!FileAccess.FileExists(user) || last != md5)
        {
            using (var src = FileAccess.Open(res, FileAccess.ModeFlags.Read))
            using (var dst = FileAccess.Open(user, FileAccess.ModeFlags.Write))
                dst.StoreBuffer(src.GetBuffer((long)src.GetLength()));
            using var st = FileAccess.Open(stamp, FileAccess.ModeFlags.Write);
            st.StoreString(md5);
        }
        return abs;
    }
}
