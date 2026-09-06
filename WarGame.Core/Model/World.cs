using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Systems;
using WarGame.Core.Stats;

namespace WarGame.Core.Model;

/// <summary>Raiz do estado da simulação. Sem Godot. Tick executa cada ISystem na ordem registada.</summary>
public sealed class World
{
    public Clock Clock { get; }
    public Dictionary<int, Region> Regions { get; } = new();
    public Dictionary<int, Country> Countries { get; } = new();
    public Dictionary<int, Division> Divisions { get; } = new();
    public List<Battle> ActiveBattles { get; } = new();
    public EventBus Events { get; } = new();
    public DivisionStatCache Stats { get; }
    public ModifierEngine Modifiers { get; }
    public Random Rng { get; }
    /// <summary>Constantes de jogo da tabela `rule` (+ `move_cost:&lt;terreno&gt;` da tabela terrain). Nada em código.</summary>
    public Dictionary<string, float> Rules { get; } = new();
    public IUnitRepository Units => Stats.Units;
    /// <summary>Árvore tecnológica (tabela tech) e efeitos de país por tecnologia (tech_effect).</summary>
    public Dictionary<string, Tech> Techs { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> TechEffects { get; } = new();
    /// <summary>Focos nacionais (tabela focus) e efeitos (focus_effect), por id.</summary>
    public Dictionary<string, Focus> Focuses { get; } = new();
    /// <summary>Eventos noticiosos (news_event) e efeitos (news_event_effect), por id.</summary>
    public Dictionary<string, NewsEvent> NewsEvents { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> NewsEffects { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> FocusEffects { get; } = new();
    /// <summary>Alianças defensivas (tabelas faction + faction_member). Ver FactionsOf/SameFaction/Allies.</summary>
    public Dictionary<string, Faction> Factions { get; } = new();

    private readonly List<ISystem> _systems = new();
    private int _nextDivisionId;
    public IReadOnlyList<ISystem> Systems => _systems;

    public World(DateOnly start, DivisionStatCache stats, ModifierEngine modifiers, int seed = 0)
    {
        Clock = new Clock(start); Stats = stats; Modifiers = modifiers; Rng = new Random(seed);
    }

    public void Register(ISystem s) => _systems.Add(s);

    /// <summary>Um dia. Chamar fora da main thread; a UI lê depois.</summary>
    public void Tick()
    {
        foreach (var s in _systems) s.Tick(this);
        Clock.Advance();
        Events.Publish(new DayPassed(Clock.Day));
    }

    public float Rule(string key, float fallback = 0f) => Rules.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>Recalcula Country.TechMult a partir das tecnologias concluídas (chamar após LoadSave e ao concluir uma).</summary>
    public void ApplyTechs(Country c)
    {
        c.TechMult.Clear();
        foreach (var t in c.Techs)
            if (TechEffects.TryGetValue(t, out var effs))
                foreach (var (key, mul) in effs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var f in c.FocusesDone)
            if (FocusEffects.TryGetValue(f, out var effs))
                foreach (var (key, mul) in effs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        foreach (var e in NewsEvents.Values)   // eventos noticiosos já disparados (NewsSystem)
            if (e.Day <= Clock.Day && (e.CountryId is null || e.CountryId == c.Id) && NewsEffects.TryGetValue(e.Id, out var neffs))
                foreach (var (key, mul) in neffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
    }

    /// <summary>Pode escolher o foco: é do país, não o tem, e tem o anterior.</summary>
    public bool CanFocus(Country c, string focusId) =>
        Focuses.TryGetValue(focusId, out var f) && f.CountryId == c.Id && !c.FocusesDone.Contains(focusId)
        && (f.Requires is null || c.FocusesDone.Contains(f.Requires));

    /// <summary>Pode investigar: existe, não a tem, tem a anterior.</summary>
    public bool CanResearch(Country c, string techId) =>
        Techs.TryGetValue(techId, out var t) && !c.Techs.Contains(techId) && (t.Requires is null || c.Techs.Contains(t.Requires));
    public float MoveCost(string terrain) => Rule("move_cost:" + terrain, 1f);

    public bool AreAtWar(int a, int b) => a != b && Countries.TryGetValue(a, out var c) && c.AtWarWith.Contains(b);
    /// <summary>Região controlada por alguém com quem `countryId` está em guerra.</summary>
    public bool IsHostile(int countryId, Region r) => AreAtWar(countryId, r.ControllerId);

    /// <summary>Facções de que `countryId` é membro (0, 1 ou várias).</summary>
    public IEnumerable<Faction> FactionsOf(int countryId) => Factions.Values.Where(f => f.Members.Contains(countryId));
    /// <summary>Há alguma facção com ambos como membros (HoI4: aliados na mesma aliança nunca se declaram guerra).</summary>
    public bool SameFaction(int a, int b) => a != b && Factions.Values.Any(f => f.Members.Contains(a) && f.Members.Contains(b));
    /// <summary>Todos os membros de todas as facções de `countryId`, sem ele próprio (dissuasão: força que conta contra atacá-lo).</summary>
    public HashSet<int> Allies(int countryId)
    {
        var result = new HashSet<int>();
        foreach (var f in FactionsOf(countryId))
            foreach (var m in f.Members)
                if (m != countryId) result.Add(m);
        return result;
    }

    public float TemplateCost(int templateId) =>
        Units.GetTemplate(templateId).Units.Sum(u => Units.GetUnitType(u.UnitTypeId).Cost * u.Qty);

    public int NewDivisionId()
    {
        if (_nextDivisionId == 0) _nextDivisionId = Divisions.Count == 0 ? 1 : Divisions.Keys.Max() + 1;
        return _nextDivisionId++;
    }

    // ---- contabilidade de divisões (sem regras; só mantém Regions[].DivisionIds e batalhas coerentes)
    public Division AddDivision(Division d)
    {
        Divisions[d.Id] = d; Regions[d.RegionId].DivisionIds.Add(d.Id);
        if (d.Id >= _nextDivisionId) _nextDivisionId = d.Id + 1;
        return d;
    }

    public void RemoveDivision(int id)
    {
        if (!Divisions.Remove(id, out var d)) return;
        Regions[d.RegionId].DivisionIds.Remove(id);
        foreach (var b in ActiveBattles) { b.Attackers.Remove(id); b.Defenders.Remove(id); }
    }

    /// <summary>Muda a divisão de região (sem custo nem regras — MovementSystem decide quando).</summary>
    public void PlaceDivision(Division d, int regionId)
    {
        Regions[d.RegionId].DivisionIds.Remove(d.Id);
        d.RegionId = regionId;
        Regions[regionId].DivisionIds.Add(d.Id);
    }

    public Battle? BattleAt(int regionId, int attackerCountryId) =>
        ActiveBattles.FirstOrDefault(b => b.RegionId == regionId && b.AttackerCountryId == attackerCountryId);

    public bool InBattle(int divisionId) =>
        ActiveBattles.Any(b => b.Attackers.Contains(divisionId) || b.Defenders.Contains(divisionId));
}

public sealed class Battle
{
    public int RegionId { get; init; }
    public int AttackerCountryId { get; init; }
    public List<int> Attackers { get; } = new();
    public List<int> Defenders { get; } = new();
    public int Days { get; set; }
}
