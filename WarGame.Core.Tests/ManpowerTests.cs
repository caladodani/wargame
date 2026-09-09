using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>ManpowerSystem + custos de homens na produção e nos reforços. Regras usadas
/// (seed_world): per_million_daily 60, cap_share 0.05, start_share 0.5, per_cost 500,
/// reinforce_hp_manpower 30, reinforce_hp_money 0.05. LinearMap: 10M por região.</summary>
public class ManpowerTests
{
    [Fact]
    public void FirstTick_Initializes_ThenGrowsToCap()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);   // país 1 controla 3 regiões = 30M
        var c = w.Countries[1];
        c.Manpower = -1f;
        var sys = new ManpowerSystem();
        sys.Tick(w);
        Assert.Equal(30_000_000f * 0.05f * 0.5f, c.Manpower, 0.5f);   // tecto 1.5M, arranca a metade
        sys.Tick(w);
        Assert.Equal(750_000f + 30f * 60f, c.Manpower, 0.5f);         // +60/milhão/dia
        c.Manpower = 2_000_000f;   // acima do tecto → corta
        sys.Tick(w);
        Assert.Equal(1_500_000f, c.Manpower, 0.5f);
    }




}
