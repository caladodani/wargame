using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Data;

public interface IUnitRepository
{
    UnitType GetUnitType(int id);
    DivisionTemplate GetTemplate(int id);
    IEnumerable<Modifier> GetModifiers();
}

public interface IWorldRepository
{
    void LoadStatic(World w);                 // regiões, países, vizinhos
    void LoadSave(World w, IDatabase save);   // divisões, controlo, clock…
    void WriteSave(World w, IDatabase save);
}
