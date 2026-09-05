using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Uma mecânica isolada. Nova mecânica = nova classe, registada em World.</summary>
public interface ISystem
{
    string Name { get; }
    void Tick(World world);
}
