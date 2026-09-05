using Godot;
using FileAccess = Godot.FileAccess;
using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using WarGame.Data;

namespace WarGame.Presentation;

/// <summary>Autoload. Dono do World; corre o tick em background e emite sinal para a UI.</summary>
public partial class Game : Node
{
    [Signal] public delegate void TickCompletedEventHandler(int day);

    public World World { get; private set; } = null!;
    public CommandDispatcher Commands { get; } = new();
    public SqlWorldRepository WorldRepo { get; private set; } = null!;
    public IDatabase StaticDb => _static;

    private IDatabase _static = null!;
    private double _accum;
    private bool _ticking;
    private static readonly double[] SpeedSeconds = { 0, 2.0, 1.0, 0.5, 0.25 };

    public override void _Ready()
    {
        // static.db é só leitura; em Android o res:// não é acessível ao SQLite → copiar para user:// no 1º arranque.
        var path = EnsureUserCopy("res://data/static.db", "user://static.db");
        _static = new GdSqliteDatabase(path, readOnly: true);

        var units = new SqlUnitRepository(_static);
        World = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units), new ModifierEngine(units.GetModifiers()));
        WorldRepo = new SqlWorldRepository(_static);
        WorldRepo.LoadStatic(World);

        // Ordem do tick — única fonte de verdade.
        World.Register(new SupplySystem());
        World.Register(new EconomySystem());
        World.Register(new ProductionSystem());
        World.Register(new MovementSystem());
        World.Register(new CombatSystem());
        World.Register(new RecoverySystem());
        World.Register(new AiSystem());
    }

    public override void _Process(double delta)
    {
        if (World.Clock.Paused || _ticking) return;
        _accum += delta;
        if (_accum < SpeedSeconds[World.Clock.Speed]) return;
        _accum = 0; _ticking = true;
        Task.Run(() => World.Tick()).ContinueWith(_ =>
            CallDeferred(nameof(OnTickDone)));
    }

    private void OnTickDone() { _ticking = false; EmitSignal(SignalName.TickCompleted, World.Clock.Day); }

    public string? Dispatch(ICommand c) => Commands.Dispatch(World, c);

    private static string EnsureUserCopy(string res, string user)
    {
        var abs = ProjectSettings.GlobalizePath(user);
        if (!FileAccess.FileExists(user))
        {
            using var src = FileAccess.Open(res, FileAccess.ModeFlags.Read);
            using var dst = FileAccess.Open(user, FileAccess.ModeFlags.Write);
            dst.StoreBuffer(src.GetBuffer((long)src.GetLength()));
        }
        return abs;
    }
}
