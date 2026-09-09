using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A guerra submarina: o que se esconde não leva tiro, e vai-se lá buscar com sonar.
///
/// O submarino era um navio como os outros — dava mais bloqueio e ia ao fundo no combate de esquadra ao
/// lado dos cruzadores. Faltava o que faz de um submarino um submarino: andar escondido. E do outro lado
/// faltava a resposta — o contratorpedeiro dizia na ficha que caçava submarinos e não tinha uma linha de
/// código que o fizesse.
///
/// O que estes testes guardam: a conta do esconderijo (busca contra bando), que o escondido não leva nem dá
/// tiro no combate de esquadra nem do ar, que só a missão de caça o afunda e só afunda o que vê, e que a IA
/// manda a caça quando lhe metem submarinos no mar de casa.</summary>
public class SubTests
{
    private const int Mar = 7;      // um mar ao largo, longe de toda a terra: é lá que o bando se esconde

    /// <summary>Linha do costume mais um mar ao largo. Os dois países de mão humana (a IA tem o seu próprio
    /// teste) e cofre cheio, para nenhuma esquadra voltar ao porto por falta de dinheiro.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[Mar] = new Region
        {
            Id = Mar, Name = "M" + Mar, OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1000, Lon = 60f, CenterX = 700, CenterY = 500, Coastal = true,
        };
        // rotas de mar: da nossa costa (3) para o largo e para a costa deles, senão nenhuma esquadra tem
        // por onde ir e a caça nunca sai do porto
        w.Regions[3].SeaNeighbours[Mar] = 500f; w.Regions[Mar].SeaNeighbours[3] = 500f;
        w.Regions[3].SeaNeighbours[4] = 400f; w.Regions[4].SeaNeighbours[3] = 400f;
        w.StartWar(1, 2);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; c.IsPlayer = true; c.Ships.Clear(); }
        w.Register(new NavalMissionSystem());
        return w;
    }

    /// <summary>Esquadra escrita à mão: o que interessa é a composição, não quem a escolheu.</summary>
    private static NavalMission Esquadra(World w, int countryId, int regionId, string missionId,
                                         params (string Class, float N)[] squadron)
    {
        var m = new NavalMission { CountryId = countryId, RegionId = regionId, MissionId = missionId, Name = "E" };
        foreach (var (cls, n) in squadron) m.Squadron[cls] = n;
        w.NavalMissions.Add(m);
        foreach (var (cls, n) in squadron)
            if (w.Countries.TryGetValue(countryId, out var c)) c.Ships[cls] = c.Ships.GetValueOrDefault(cls) + n;
        return m;
    }






    /// <summary>A caça não apanha mais do que vê: contra o mesmo bando, poucos cascos afundam menos do que
    /// muitos — e não é só por serem menos, é por verem menos.</summary>
    [Fact]
    public void SoSeAfundaOQueSeVe()
    {
        float Afundado(float cascos)
        {
            var w = Build();
            Esquadra(w, 2, Mar, "bloqueio", ("submarino", 8f));
            Esquadra(w, 1, Mar, "caca_submarina", ("destroier", cascos));
            float ido = 0f;
            w.Events.Subscribe<SubsHunted>(e => ido += e.Ships);
            w.Tick();
            return ido;
        }

        float poucos = Afundado(4f), muitos = Afundado(20f);
        Assert.True(poucos > 0f && muitos > poucos, $"{poucos} contra {muitos}");
        Assert.True(muitos < 8f, "a caça de um dia não devia varrer o bando todo");
    }



}
