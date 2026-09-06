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
    /// <summary>Escolhas dos eventos (news_event_option, ordenadas por sort) e efeitos por opção.</summary>
    public Dictionary<string, List<NewsOption>> NewsOptions { get; } = new();
    public Dictionary<string, List<(string Key, float Mul)>> NewsOptionEffects { get; } = new();
    /// <summary>Escolha feita por evento (s_news_choice no save): event_id → option_id.</summary>
    public Dictionary<string, string> NewsChoices { get; } = new();
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
        {
            if (e.Day > Clock.Day || (e.CountryId is not null && e.CountryId != c.Id)) continue;
            if (NewsEffects.TryGetValue(e.Id, out var neffs))
                foreach (var (key, mul) in neffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
            // Efeitos da opção escolhida (eventos com escolhas); sem escolha ainda → sem efeito.
            if (NewsChoices.TryGetValue(e.Id, out var opt) && NewsOptionEffects.TryGetValue(opt, out var oeffs))
                foreach (var (key, mul) in oeffs) c.TechMult[key] = c.TechMult.GetValueOrDefault(key, 1f) * mul;
        }
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

    /// <summary>Templates desenhados em jogo (CreateTemplateCommand); ids a partir de CustomTemplateBase,
    /// persistidos no save em template/template_unit (os da static.db têm ids pequenos e nunca se escrevem).</summary>
    public List<int> CustomTemplateIds { get; } = new();
    public const int CustomTemplateBase = 1_000_000;

    /// <summary>Estado por guerra (chave normalizada min,max): quando começou e o último dia com progresso
    /// (captura de região entre os dois). O TruceSystem fecha guerras estagnadas com paz branca.</summary>
    public Dictionary<(int A, int B), WarInfo> Wars { get; } = new();
    public static (int A, int B) WarKey(int a, int b) => (Math.Min(a, b), Math.Max(a, b));

    /// <summary>Começa (ou regista) uma guerra: AtWarWith dos dois + entrada em Wars.</summary>
    public void StartWar(int a, int b, int? sinceDay = null)
    {
        Countries[a].AtWarWith.Add(b); Countries[b].AtWarWith.Add(a);
        var key = WarKey(a, b);
        if (!Wars.ContainsKey(key)) Wars[key] = new WarInfo { StartDay = sinceDay ?? Clock.Day, LastProgressDay = sinceDay ?? Clock.Day };
    }

    /// <summary>Fim de guerra entre dois: AtWarWith + Wars. Não publica eventos (o chamador decide).</summary>
    public void EndWar(int a, int b)
    {
        if (Countries.TryGetValue(a, out var ca)) ca.AtWarWith.Remove(b);
        if (Countries.TryGetValue(b, out var cb)) cb.AtWarWith.Remove(a);
        Wars.Remove(WarKey(a, b));
    }

    /// <summary>Captura de região entre beligerantes: renova o relógio da paz branca dessa guerra.</summary>
    public void NoteWarProgress(int a, int b)
    { if (Wars.TryGetValue(WarKey(a, b), out var info)) info.LastProgressDay = Clock.Day; }
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

    /// <summary>Facções fundadas em jogo (CreateFactionCommand); persistem em s_faction. As da static.db não entram.</summary>
    public List<string> CustomFactionIds { get; } = new();
    public Faction CreateFaction(string id, string name, string description)
    {
        var f = new Faction(id, name, description, new List<int>());
        Factions[id] = f; CustomFactionIds.Add(id);
        return f;
    }
    public string NewFactionId()
    {
        int n = 0;
        foreach (var id in CustomFactionIds)
            if (id.StartsWith("fx_") && int.TryParse(id.AsSpan(3), out var k) && k > n) n = k;
        return "fx_" + (n + 1);
    }
    /// <summary>A IA (e a regra de adesão do jogador) aceita entrar numa facção só com inimigo comum:
    /// o candidato está em guerra com alguém com quem um membro também está. Nunca em guerra com membros.</summary>
    public bool FactionWouldAccept(int countryId, Faction f)
    {
        if (!Countries.TryGetValue(countryId, out var c) || c.Capitulated || f.Members.Contains(countryId)) return false;
        foreach (var m in f.Members) if (AreAtWar(countryId, m)) return false;
        foreach (var e in c.AtWarWith)
            foreach (var m in f.Members)
                if (AreAtWar(m, e)) return true;
        return false;
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
