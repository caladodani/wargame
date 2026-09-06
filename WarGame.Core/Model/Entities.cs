using WarGame.Core.Stats;

namespace WarGame.Core.Model;

public sealed record UnitType(int Id, string Name, string Category, float Cost, int BuildDays, float Mobility, float SupplyUse, StatBlock Stats);

public sealed record DivisionTemplate(int Id, int CountryId, string Name, IReadOnlyList<(int UnitTypeId, int Qty)> Units);

/// <summary>Espírito nacional (tabela national_spirit); os efeitos são linhas modifier com SpiritId.</summary>
/// <summary>Tecnologia (tabela tech). Cost = dias com research_speed 1; Requires = id da anterior no ramo.</summary>
public sealed record Tech(string Id, string Branch, string Name, float Cost, string? Requires, string? Description);
/// <summary>Evento noticioso com data marcada (tabela news_event); CountryId null = global.</summary>
public sealed record NewsEvent(string Id, int Day, int? CountryId, string Title, string Body);
/// <summary>Escolha de um evento noticioso (news_event_option). A IA fica com a primeira (sort).</summary>
public sealed record NewsOption(string Id, string EventId, string Title, int Sort);
/// <summary>Lei nacional (tabela law): grupos (conscrição, economia…) com uma lei activa por grupo.
/// Sort maior = mais mobilizada (a IA escala em guerra). Efeitos em law_effect.</summary>
public sealed record Law(string Id, string Group, string Name, string Description, int Sort, bool IsDefault);

/// <summary>Operação de espionagem (tabela spy_op): one-shot, paga à partida, efeito ao concluir.</summary>
public sealed record SpyOp(string Id, string Name, string Description, float Cost, int Days, string Effect, float Magnitude);

/// <summary>Operação em curso (World.ActiveSpyOps; EspionageSystem conta os dias).</summary>
public sealed class ActiveSpyOp
{
    public int CountryId { get; init; }
    public int TargetCountryId { get; init; }
    public string OpId { get; init; } = "";
    public float DaysLeft { get; set; }
}
/// <summary>Foco nacional (HoI4): tabela focus; efeitos = focus_effect (multiplicadores de Stat).</summary>
public sealed record Focus(string Id, int CountryId, string Name, string Description, int Days, string? Requires, int Sort);

public sealed record NationalSpirit(string Id, string CountryTag, string Name, string Description);

/// <summary>Texto do painel de país (tabela country_info).</summary>
public sealed record CountryInfo(string CountryTag, string Government, string Leader, string Doctrine, string Alliance, string Description);

/// <summary>Aliança defensiva (tabelas faction + faction_member, HoI4: facção). Um país pode pertencer a várias;
/// declarar guerra a um membro chama os outros contra o agressor (DeclareWarCommand) — ver World.FactionsOf/Allies.</summary>
public sealed record Faction(string Id, string Name, string Description, List<int> Members);

public sealed class Region
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int OwnerId { get; set; }
    /// <summary>Dono na static.db; se OwnerId difere (capitulação), o save tem de guardar o novo dono.</summary>
    public int InitialOwnerId { get; init; }
    public int ControllerId { get; set; }
    public string Terrain { get; init; } = "plain";
    public bool River { get; init; }
    public float Infrastructure { get; set; } = 1f;
    /// <summary>Obra de infraestrutura em curso (ConstructionSystem); cancela se a região for capturada.</summary>
    public bool Building { get; set; }
    public float BuildProgress { get; set; }
    /// <summary>Nível de fortificação (0..fort_max): multiplica a força dos defensores (fort_defense_per_level).
    /// Captura tira um nível. Obra própria (FortBuilding/FortProgress) ao lado da de infraestrutura.</summary>
    public int Fort { get; set; }
    public bool FortBuilding { get; set; }
    public float FortProgress { get; set; }
    public int Population { get; init; }
    public float CenterX { get; init; }            // centróide projectado (unidades do mapa); só para UI/IA
    public float CenterY { get; init; }
    public List<int> Neighbours { get; init; } = new();
    /// <summary>Ligações marítimas (sea_link): região costeira → km da travessia. Vazio = interior.</summary>
    public Dictionary<int, float> SeaNeighbours { get; init; } = new();
    public bool Coastal { get; init; }
    public List<int> DivisionIds { get; } = new();
}

/// <summary>Estado de uma guerra em curso (World.Wars, chave min,max).</summary>
public sealed class WarInfo
{
    public int StartDay { get; set; }
    /// <summary>Último dia em que um dos dois capturou região ao outro; estagnado → paz branca.</summary>
    public int LastProgressDay { get; set; }
}

/// <summary>Uma encomenda na fila: divisão inteira de um template. Progress em pontos gastos.</summary>
public sealed class ProductionOrder
{
    public int TemplateId { get; init; }
    public float Progress { get; set; }
}

public sealed class Country
{
    public int Id { get; init; }
    public string Tag { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsPlayer { get; set; }
    public int CapitalRegionId { get; set; }
    /// <summary>Características do país (tabela country_stat): industry, production_speed, org_regain, start_army_mult…</summary>
    public StatBlock Stats { get; } = new();
    /// <summary>Multiplicadores acumulados das tecnologias concluídas (tech_effect); World.ApplyTechs recalcula.</summary>
    public Dictionary<string, float> TechMult { get; } = new();
    /// <summary>Stat de país com fallback 1 (multiplicadores): sem linha na tabela = neutro. × tecnologias.</summary>
    public float Stat(string key, float fallback = 1f) =>
        (Stats.Has(key) ? Stats[key] : fallback) * (TechMult.TryGetValue(key, out var m) ? m : 1f);
    public string? ResearchTech { get; set; }     // tecnologia em investigação (null = nenhuma)
    public float ResearchProgress { get; set; }   // dias acumulados × research_speed
    public float Money { get; set; }               // pontos de produção acumulados (EconomySystem +, ProductionSystem −)
    public float Manpower { get; set; } = -1f;     // pool de homens (ManpowerSystem); -1 = por inicializar
    public float Stability { get; set; } = 50f;    // 0..100 (StabilitySystem); 50 = neutro
    /// <summary>Lei activa por grupo (grupo → law_id); grupos ausentes usam a lei is_default.</summary>
    public Dictionary<string, string> Laws { get; } = new();
    public int? JustifyTarget { get; set; }        // a justificar guerra contra (DiplomacySystem)
    public float JustifyProgress { get; set; }
    /// <summary>Efeito da estabilidade no rendimento e no recrutamento: 0.5 (colapso) a 1.5 (união nacional).</summary>
    public float StabilityFactor => 0.5f + Stability / 100f;
    public List<ProductionOrder> Queue { get; } = new();
    public HashSet<string> Techs { get; } = new();
    public string? CurrentFocus { get; set; }      // foco nacional em curso (FocusSystem)
    public float FocusProgress { get; set; }
    public HashSet<string> FocusesDone { get; } = new();
    public HashSet<int> AtWarWith { get; } = new();
    /// <summary>Capitulou (PeaceSystem): sem regiões nem exército; a IA ignora-o. Persistido em s_country.</summary>
    public bool Capitulated { get; set; }
    public int? CapitulatedDay { get; set; }
}

/// <summary>Estado mutável mínimo; stats vêm do cache por template.</summary>
public sealed class Division
{
    public int Id { get; init; }
    public int CountryId { get; init; }
    public int TemplateId { get; set; }
    public int RegionId { get; set; }
    public string? Name { get; set; }             // "Brigada Mecanizada"… (start_division.name / produção); null = nome do template
    public float Hp { get; set; } = 100f;
    public float Org { get; set; } = 100f;
    public float Supply { get; set; } = 1f;
    public float MoveProgress { get; set; }        // 0..1 dentro do salto actual (MovementSystem)
    /// <summary>Saltos restantes, do próximo ao destino. Vazio = parada.</summary>
    public List<int> Path { get; } = new();
    public int? TargetRegionId => Path.Count > 0 ? Path[0] : null;
    public int? DestinationRegionId => Path.Count > 0 ? Path[^1] : null;
    public bool CanFight => Org >= 10f && Hp > 0f;

    public void SetPath(IEnumerable<int> hops) { Path.Clear(); Path.AddRange(hops); MoveProgress = 0f; }
    public void ClearPath() { Path.Clear(); MoveProgress = 0f; }
    public void AdvanceHop() { if (Path.Count > 0) Path.RemoveAt(0); MoveProgress = 0f; }
}
