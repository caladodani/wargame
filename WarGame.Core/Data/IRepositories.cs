using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Data;

public interface IUnitRepository
{
    UnitType GetUnitType(int id);
    DivisionTemplate GetTemplate(int id);
    IReadOnlyList<DivisionTemplate> GetTemplates(int countryId);
    IEnumerable<Modifier> GetModifiers();
    /// <summary>Todos os unit_type da static.db (desenhador de templates).</summary>
    IReadOnlyList<UnitType> AllUnitTypes();
    /// <summary>Regista um template criado em jogo (id ≥ World.CustomTemplateBase); só em memória — persiste no save.</summary>
    DivisionTemplate AddCustomTemplate(int id, int countryId, string name, IReadOnlyList<(int UnitTypeId, int Qty)> units);
}

public interface IWorldRepository
{
    void LoadStatic(World w);                 // regiões, países, vizinhos, regras
    void LoadStartArmies(World w);            // start_division → divisões do dia 0 (só quando não há save)
    void LoadStartWars(World w);              // start_war → guerras a decorrer no dia 0 (só quando não há save)
    void LoadSave(World w, IDatabase save);   // divisões, controlo, clock…
    void WriteSave(World w, IDatabase save);
}
