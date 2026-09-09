using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Guerra aérea por região: as asas destacadas saem do pool, custam estadia, abatem-se umas às
/// outras no céu disputado e pesam na batalha que se dá por baixo delas.</summary>
public class AirMissionTests
{
    private static World Build(float wings = 10f, float money = 500f, float lonStep = 2f)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, lonStep: lonStep);
        w.StartWar(1, 2);
        // os dois países de mão humana: a IA aérea tem um teste só para ela e não anda a engrossar estes
        foreach (var c in w.Countries.Values) { c.AirPower = wings; c.Money = money; c.IsPlayer = true; }
        w.Register(new AirMissionSystem());
        return w;
    }








    [Fact]
    public void PlanesOverTheBattleAreWorthMoreThanPlanesAtHome()
    {
        // dois mundos iguais: num deles o atacante destacou as asas para o céu da batalha
        static (World w, Division def) Field(bool overhead)
        {
            var (w, _) = TestWorld.Build();
            TestWorld.LinearMap(w);
            w.StartWar(1, 2);
            w.Countries[1].AirPower = 8f; w.Countries[1].Money = 500f;
            w.Countries[2].AirPower = 8f;                                // céu equilibrado sem missões nenhumas
            var att = TestWorld.AddDivision(w, 1, 1, TestWorld.Inf, 3);
            var def = TestWorld.AddDivision(w, 2, 2, TestWorld.Inf2, 4);
            if (overhead)
            {
                AirMissionSystem.Assign(w, 1, 4, "superioridade", 8f);
                AirMissionSystem.Assign(w, 1, 4, "apoio", 0f);                // não muda nada: zero asas
            }
            var battle = new Battle { RegionId = 4, AttackerCountryId = 1 };
            battle.Attackers.Add(att.Id); battle.Defenders.Add(def.Id);
            w.ActiveBattles.Add(battle);
            w.Register(new CombatSystem());
            return (w, def);
        }

        var (home, safe) = Field(false);
        var (sky, hit) = Field(true);
        for (int i = 0; i < 5; i++) { home.Tick(); sky.Tick(); }

        Assert.True(hit.Hp < safe.Hp, $"com o céu limpo o defensor devia estar pior: {hit.Hp:0.0} contra {safe.Hp:0.0}");
    }



}
