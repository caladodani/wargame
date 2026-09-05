using WarGame.Core.Stats;

namespace WarGame.Core.Model;

public sealed record UnitType(int Id, string Name, string Category, float Cost, int BuildDays, StatBlock Stats);

public sealed record DivisionTemplate(int Id, int CountryId, string Name, IReadOnlyList<(int UnitTypeId, int Qty)> Units);

public sealed class Region
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public int OwnerId { get; set; }
    public int ControllerId { get; set; }
    public string Terrain { get; init; } = "plain";
    public bool River { get; init; }
    public float Infrastructure { get; set; } = 1f;
    public int Population { get; init; }
    public List<int> Neighbours { get; init; } = new();
    public List<int> DivisionIds { get; } = new();
}

public sealed class Country
{
    public int Id { get; init; }
    public string Tag { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsPlayer { get; set; }
    public HashSet<string> Techs { get; } = new();
    public HashSet<int> AtWarWith { get; } = new();
}

/// <summary>Estado mutável mínimo; stats vêm do cache por template.</summary>
public sealed class Division
{
    public int Id { get; init; }
    public int CountryId { get; init; }
    public int TemplateId { get; set; }
    public int RegionId { get; set; }
    public float Hp { get; set; } = 100f;
    public float Org { get; set; } = 100f;
    public float Supply { get; set; } = 1f;
    public int? TargetRegionId { get; set; }
    public bool CanFight => Org >= 10f && Hp > 0f;
}
