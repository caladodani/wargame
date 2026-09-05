using WarGame.Core.Model;

namespace WarGame.Core.Commands;

/// <summary>Toda a acção do jogador (e da IA) passa aqui: valida, depois muta. Facilita replay e log.</summary>
public interface ICommand
{
    int CountryId { get; }
    string? Validate(World w);   // null = OK, senão mensagem de erro para a UI
    void Execute(World w);
}

public sealed class CommandDispatcher
{
    private readonly List<ICommand> _log = new();
    public IReadOnlyList<ICommand> Log => _log;

    public string? Dispatch(World w, ICommand c)
    {
        var err = c.Validate(w);
        if (err is not null) return err;
        c.Execute(w); _log.Add(c);
        return null;
    }
}

public sealed record MoveDivisionCommand(int CountryId, int DivisionId, int TargetRegionId) : ICommand
{
    public string? Validate(World w)
    {
        if (!w.Divisions.TryGetValue(DivisionId, out var d)) return "Divisão inexistente";
        if (d.CountryId != CountryId) return "Divisão não é tua";
        if (!w.Regions[d.RegionId].Neighbours.Contains(TargetRegionId)) return "Região não adjacente";
        return null;
    }
    public void Execute(World w) => w.Divisions[DivisionId].TargetRegionId = TargetRegionId;
}

public sealed record DeclareWarCommand(int CountryId, int TargetCountryId) : ICommand
{
    public string? Validate(World w) =>
        w.Countries[CountryId].AtWarWith.Contains(TargetCountryId) ? "Já em guerra" : null;
    public void Execute(World w)
    {
        w.Countries[CountryId].AtWarWith.Add(TargetCountryId);
        w.Countries[TargetCountryId].AtWarWith.Add(CountryId);
        w.Events.Publish(new Events.WarDeclared(CountryId, TargetCountryId));
    }
}
