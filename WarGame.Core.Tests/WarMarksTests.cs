using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A guerra do ar e do mar em cima do mapa: quem se vê, o que se conta e o que fica por baixo.
///
/// O céu e o mar tinham tudo menos presença — para saber onde andava a aviação abria-se um painel e lia-se
/// uma lista. Agora cada missão de ar e cada esquadra é uma chapa por cima da província, como no HoI4. O que
/// estes testes guardam é a única parte que decide alguma coisa: QUEM SE VÊ. As nossas vêem-se sempre, as
/// deles só onde o nevoeiro deixa, e debaixo de água só se vê o que a busca já levantou — uma alcateia
/// inteira escondida deixa o mar do inimigo vazio, que é exactamente o que faz o submarino valer o preço.</summary>
public class WarMarksTests
{
    private const int Mar = 7;      // um mar ao largo, onde as esquadras destes testes se põem

    /// <summary>Linha do costume (1..3 nossas, 4..6 deles) mais um mar ao largo, e guerra declarada. A
    /// região 4 faz fronteira com a nossa 3 — vê-se; a 5 não faz fronteira com nada nosso e é onde se
    /// esconde o que não havemos de ver.</summary>
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Regions[Mar] = new Region
        {
            Id = Mar, Name = "M" + Mar, OwnerId = 2, InitialOwnerId = 2, ControllerId = 2,
            Terrain = "plain", Population = 1000, Lon = 60f, CenterX = 700, CenterY = 500, Coastal = true,
        };
        w.StartWar(1, 2);
        return w;
    }

    /// <summary>Uma missão de ar escrita à mão: o que interessa é onde ela está e de quem é.</summary>
    private static AirMission Asas(World w, int countryId, int regionId, string missionId,
                                   params (string Class, float N)[] squadron)
    {
        var m = new AirMission { CountryId = countryId, RegionId = regionId, MissionId = missionId, Name = "A" };
        foreach (var (cls, n) in squadron) m.Squadron[cls] = n;
        w.AirMissions.Add(m);
        return m;
    }

    /// <summary>Uma esquadra escrita à mão, com os cascos também no país (é de lá que a busca os conta).</summary>
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

    /// <summary>As nossas asas vêem-se sempre; as deles só onde há olhos nossos. É a mesma porta das
    /// divisões (Vision.Sees), não uma segunda regra escrita à parte.</summary>
    [Fact]
    public void AsNossasVeemSeSempreEAsDelesSoOndeONevoeiroDeixa()
    {
        var w = Build();
        Asas(w, 1, 1, "superioridade", ("caca", 4f));        // em casa
        Asas(w, 2, 4, "bombardeamento", ("bombardeiro", 6f)); // à nossa porta: a 4 faz fronteira com a nossa 3
        Asas(w, 2, 5, "superioridade", ("caca", 9f));         // lá dentro: não temos lá olhos nenhuns

        var mine = WarMarks.All(w, 1);
        Assert.Equal(new[] { 1, 4 }, mine.Select(m => m.RegionId).ToArray());
        Assert.Equal(1, mine[0].Side);
        Assert.Equal(-1, mine[1].Side);

        // e do lado deles é ao contrário: as três missões deles vêem-se todas, a nossa está em casa nossa
        var theirs = WarMarks.All(w, 2);
        Assert.Equal(new[] { 4, 5 }, theirs.Where(m => m.Side == 1).Select(m => m.RegionId).ToArray());
    }

    /// <summary>Sem nevoeiro (regra fog_of_war=0) vê-se tudo, como no mapa aberto de antes.</summary>
    [Fact]
    public void SemNevoeiroOMapaAbreSeTodo()
    {
        var w = Build();
        Asas(w, 2, 5, "superioridade", ("caca", 9f));
        Assert.Empty(WarMarks.All(w, 1));

        w.Rules["fog_of_war"] = 0f;
        var open = Assert.Single(WarMarks.All(w, 1));
        Assert.Equal(5, open.RegionId);
        Assert.Equal(-1, open.Side);
    }

    /// <summary>Uma missão sem asas não é chapa nenhuma: o céu vazio desenha-se vazio.</summary>
    [Fact]
    public void CeuVazioNaoDaChapa()
    {
        var w = Build();
        Asas(w, 1, 2, "superioridade", ("caca", 0f));
        Assert.Empty(WarMarks.All(w, 1));
    }

    /// <summary>A chapa é do modelo que leva mais asas — é esse que se vê do chão — e a conta é a soma de
    /// todos, não a do maior.</summary>
    [Fact]
    public void AChapaEDoModeloQueLevaMaisAsas()
    {
        var w = Build();
        Asas(w, 1, 1, "bombardeamento", ("caca", 2f), ("estrategico", 5f));

        var m = Assert.Single(WarMarks.All(w, 1));
        Assert.Equal(Air.Glyph(w, "estrategico"), m.Glyph);
        Assert.Equal(7f, m.Count, 3);
        Assert.Equal(w.AirMissionDefs["bombardeamento"].Glyph, m.MissionGlyph);
        Assert.Equal(w.AirMissionDefs["bombardeamento"].Name, m.MissionName);
    }

    /// <summary>No mar conta-se o que está à superfície: o inimigo vê a esquadra menos o que a busca dele
    /// não levantou, e o dono vê a sua toda.</summary>
    [Fact]
    public void NoMarSoSeContaOQueEstaAFlutuar()
    {
        var w = Build();
        var m = Esquadra(w, 2, Mar, "bloqueio", ("submarino", 10f));

        float below = Subs.Hidden(w, m).Values.Sum();
        Assert.True(below > 0.05f);            // sem caça nenhuma a maior parte do bando anda por baixo

        w.Rules["fog_of_war"] = 0f;            // o mar vê-se; o que não se vê é o que vai submerso
        var seen = Assert.Single(WarMarks.All(w, 1));
        Assert.Equal(10f - below, seen.Count, 3);
        Assert.Equal(0f, seen.Hidden);         // o que vai por baixo não é conta nossa

        var owner = Assert.Single(WarMarks.All(w, 2));
        Assert.Equal(10f, owner.Count, 3);
        Assert.Equal(below, owner.Hidden, 3);
    }

    /// <summary>Um bando inteiramente escondido deixa o mar do inimigo vazio: não há chapa nenhuma, e é
    /// essa a razão de haver caça anti-submarina. O dono continua a ver a sua alcateia.</summary>
    [Fact]
    public void OBandoTodoEscondidoDeixaOMarVazio()
    {
        var w = Build();
        // um casco que se esconde por inteiro é uma linha da tabela, não código: sem busca nenhuma do outro
        // lado, o que se esconde por inteiro não deixa nada à superfície
        w.ShipClasses["submarino"] = w.ShipClasses["submarino"] with { Stealth = 1f };
        Esquadra(w, 2, Mar, "bloqueio", ("submarino", 12f));
        w.Rules["fog_of_war"] = 0f;

        Assert.Empty(WarMarks.All(w, 1));

        var owner = Assert.Single(WarMarks.All(w, 2));
        Assert.Equal(12f, owner.Count, 3);
        Assert.Equal(12f, owner.Hidden, 3);
    }

    /// <summary>A chapa do mar vem do casco mais numeroso do que SE VÊ: um comboio de escolta não se marca
    /// com o submarino que vai lá no meio escondido.</summary>
    [Fact]
    public void AChapaDoMarNaoEADoQueVaiSubmerso()
    {
        var w = Build();
        w.ShipClasses["submarino"] = w.ShipClasses["submarino"] with { Stealth = 1f };
        Esquadra(w, 2, Mar, "bloqueio", ("destroier", 3f), ("submarino", 8f));
        w.Rules["fog_of_war"] = 0f;

        var seen = Assert.Single(WarMarks.All(w, 1));
        Assert.Equal(Navy.Glyph(w, "destroier"), seen.Glyph);
        Assert.Equal(3f, seen.Count, 3);

        var owner = Assert.Single(WarMarks.All(w, 2));
        Assert.Equal(Navy.Glyph(w, "submarino"), owner.Glyph);   // para o dono, o bando é a esquadra
    }

    /// <summary>Ar primeiro, mar depois, e por região: duas máquinas com o mesmo mundo desenham o mesmo
    /// mapa, que a ordem de um dicionário não pode mudar chapas de sítio.</summary>
    [Fact]
    public void AOrdemNaoDependeDeDicionarioNenhum()
    {
        var w = Build();
        Esquadra(w, 1, 3, "patrulha", ("corveta", 2f));
        Asas(w, 1, 2, "superioridade", ("caca", 3f));
        Asas(w, 1, 1, "apoio", ("ataque", 1f));
        Asas(w, 1, 1, "superioridade", ("caca", 2f));

        var ids = WarMarks.All(w, 1).Select(m => (m.Sea, m.RegionId, m.MissionName)).ToArray();
        Assert.Equal(new[] { (false, 1, "Apoio próximo"), (false, 1, "Superioridade aérea"),
                             (false, 2, "Superioridade aérea"), (true, 3, "Patrulha") }, ids);
        Assert.Equal(ids, WarMarks.All(w, 1).Select(m => (m.Sea, m.RegionId, m.MissionName)).ToArray());
    }

    /// <summary>A linha da prova headless conta o que o mapa mostra, e conta pelo lado de quem olha.</summary>
    [Fact]
    public void ALinhaDaProvaContaOQueOMapaMostra()
    {
        var w = Build();
        Asas(w, 1, 1, "superioridade", ("caca", 4f));
        Asas(w, 2, 4, "bombardeamento", ("bombardeiro", 6f));
        Esquadra(w, 1, 3, "patrulha", ("corveta", 2f));

        string line = WarMarks.Short(w, 1);
        Assert.Contains("2 chapas de ar", line);
        Assert.Contains("1 de mar", line);
        Assert.Contains("6 deles à vista", line);
    }
}
