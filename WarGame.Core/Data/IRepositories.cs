using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Data;

public interface IUnitRepository
{
    UnitType GetUnitType(int id);
    DivisionTemplate GetTemplate(int id);
    IReadOnlyList<DivisionTemplate> GetTemplates(int countryId);
    IEnumerable<Modifier> GetModifiers();
}

public interface IWorldRepository
{
    void LoadStatic(World w);                 // regiões, países, vizinhos, regras
    void LoadStartArmies(World w);            // start_division → divisões do dia 0 (só quando não há save)
    void LoadStartWars(World w);              // start_war → guerras a decorrer no dia 0 (só quando não há save)
    void LoadSave(World w, IDatabase save);   // divisões, controlo, clock…
    void WriteSave(World w, IDatabase save);
}
