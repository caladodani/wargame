using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A opinião do país: partidos, eleições e golpes (HoI4: o ecrã de política).
///
/// Até aqui o país tinha poder político, leis, gabinete e decisões — e nenhuma opinião. O governo era
/// eterno e a população não existia como força. O que isto traz é o país a ter uma voz que se mexe
/// sozinha todos os dias e um governo que pode cair de duas maneiras: nas urnas e pela rua.
///
/// O que estes testes guardam: que os partidos, as bases e os puxões vêm da tabela e não do código; que
/// a opinião soma sempre 100; que a ficha estática manda no governo inicial e que um país sem ficha
/// nasce com o partido de maior base; que a guerra e a instabilidade puxam para onde a tabela diz; que
/// as urnas trocam o governo quando a oposição passa à frente e que quem não faz eleições não tem
/// relógio; que o golpe só pega com a rua ganha E o país instável; que quem governa dá o seu
/// multiplicador ao país; que a propaganda paga poder político e mexe mesmo na barra; e que a opinião
/// atravessa o save.</summary>
public class PartyTests
{
    private static World Build()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c in w.Countries.Values) { c.Money = 5000f; World.SettleParties(w, c); }
        return w;
    }

    /// <summary>A opinião com que a tabela deixa Portugal começar, posta num país de teste: é a mesma
    /// passagem que o mundo a sério faz, só que a tag é outra.</summary>
    private static void FromTable(World w, Country c, string tag)
    {
        c.Parties.Clear(); c.Party = "";
        w.StartParties[c.Tag] = w.StartParties[tag];
        World.SettleParties(w, c);
    }

    [Fact]
    public void OsPartidosEAsBasesVemDaTabelaENaoDeCodigo()
    {
        var w = Build();
        Assert.NotEmpty(w.PartyDefs);
        // nenhum id de partido está escrito em código: o que se confere é a forma, não os nomes
        foreach (var p in w.PartyDefs.Values)
        {
            Assert.NotEmpty(p.Name);
            Assert.NotEmpty(p.Glyph);
            Assert.True(p.Base >= 0f);
        }
        // e há pelo menos um que não faz eleições — é dele que vem o golpe como única saída
        Assert.Contains(w.PartyDefs.Values, p => !p.Elections);
        // a ficha estática existe e diz quem governa em cada país da tabela
        Assert.NotEmpty(w.StartParties);
        foreach (var (_, rows) in w.StartParties)
            Assert.True(rows.Count(r => r.Ruling) <= 1);
    }

    [Fact]
    public void AOpiniaoDoPaisSomaSempreCem()
    {
        var w = Build();
        var c = w.Countries[1];
        Assert.Equal(100f, c.Parties.Values.Sum(), 2);
        var sys = new PartySystem();
        for (int i = 0; i < 200; i++) { w.Clock.Advance(); sys.Tick(w); }
        Assert.Equal(100f, c.Parties.Values.Sum(), 2);
        Assert.All(c.Parties.Values, v => Assert.True(v >= 0f));
    }

    [Fact]
    public void UmPaisSemFichaNasceComOPartidoDeMaiorBaseNoPoder()
    {
        var w = Build();
        var c = w.Countries[1];                       // tag "A": a tabela country_party não o conhece
        var biggest = w.PartyDefs.Values.OrderByDescending(p => p.Base).ThenBy(p => p.Id).First();
        Assert.Equal(biggest.Id, c.Party);
        // e a opinião reparte-se pelas bases, não em partes iguais
        Assert.True(c.Parties[biggest.Id] > c.Parties.Values.Min());
    }

    [Fact]
    public void AFichaDaTabelaMandaNoGovernoInicialEORestoRepartePelasBases()
    {
        var w = Build();
        var c = w.Countries[1];
        var rows = w.StartParties.First(kv => kv.Value.Any(r => r.Ruling));
        FromTable(w, c, rows.Key);

        var ruling = rows.Value.First(r => r.Ruling);
        Assert.Equal(ruling.Party, c.Party);
        Assert.Equal(ruling.Popularity, c.Parties[ruling.Party], 1);
        // os que a ficha não nomeou ficaram lá na mesma, com a fatia da base deles
        foreach (var p in w.PartyDefs.Values) Assert.True(c.Parties.ContainsKey(p.Id));
        Assert.Equal(100f, c.Parties.Values.Sum(), 2);
    }

    [Fact]
    public void AGuerraPuxaAOpiniaoParaOndeATabelaDiz()
    {
        var w = Build();
        var c = w.Countries[1];
        // o partido que mais ganha com a guerra e o que mais perde, ambos lidos da tabela
        var hawk = w.PartyDefs.Values.OrderByDescending(p => p.DriftWar).First();
        var dove = w.PartyDefs.Values.OrderBy(p => p.DriftWar).First();
        Assert.True(hawk.DriftWar > dove.DriftWar);

        w.Countries[1].AtWarWith.Add(2); w.Countries[2].AtWarWith.Add(1);
        float hadHawk = c.Parties[hawk.Id], hadDove = c.Parties[dove.Id];
        var sys = new PartySystem();
        for (int i = 0; i < 120; i++) { w.Clock.Advance(); sys.Tick(w); }

        Assert.True(c.Parties[hawk.Id] > hadHawk, "a guerra tinha de encher os cartazes de quem vive dela");
        Assert.True(c.Parties[dove.Id] < hadDove, "e esvaziar os de quem a não quer");
    }

    [Fact]
    public void AsUrnasTrocamOGovernoQuandoAOposicaoPassaAFrente()
    {
        var w = Build();
        var c = w.Countries[1];
        var rival = w.PartyDefs.Values.First(p => p.Id != c.Party && p.Elections);
        foreach (var k in c.Parties.Keys.ToList()) c.Parties[k] = 10f;
        c.Parties[rival.Id] = 60f;
        World.NormalizeParties(c);
        c.NextElection = w.Clock.Day + 1;
        c.Stability = 60f;
        float stab = c.Stability;

        ElectionHeld? seen = null;
        w.Events.Subscribe<ElectionHeld>(e => seen = e);
        w.Clock.Advance();
        new PartySystem().Tick(w);

        Assert.NotNull(seen);
        Assert.True(seen!.Changed);
        Assert.Equal(rival.Id, c.Party);
        Assert.True(c.Stability < stab, "trocar de governo custa estabilidade");
        Assert.Equal(w.Clock.Day + PartySystem.Term(w), c.NextElection);
    }

    [Fact]
    public void OGovernoQueNaoFazEleicoesNaoTemRelogio()
    {
        var w = Build();
        var c = w.Countries[1];
        var strong = w.PartyDefs.Values.First(p => !p.Elections);
        c.Party = strong.Id;
        c.NextElection = w.Clock.Day + 10;
        c.Stability = 90f;                       // longe do golpe: o que se mede é só o relógio

        w.Clock.Advance();
        new PartySystem().Tick(w);

        Assert.Equal(0, c.NextElection);
        Assert.Equal(strong.Id, c.Party);
    }

    [Fact]
    public void OGolpeSoPegaComARuaGanhaEOPaisInstavel()
    {
        var w = Build();
        var c = w.Countries[1];
        var rival = w.PartyDefs.Values.First(p => p.Id != c.Party);
        foreach (var k in c.Parties.Keys.ToList()) c.Parties[k] = 5f;
        c.Parties[rival.Id] = w.Rule("coup_popularity", 60f) + 10f;
        World.NormalizeParties(c);

        // país calmo: a rua é dele, mas não há golpe nenhum
        c.Stability = w.Rule("coup_stability", 25f) + 20f;
        string was = c.Party;
        w.Clock.Advance();
        new PartySystem().Tick(w);
        Assert.Equal(was, c.Party);

        // o mesmo país pelas ruas: agora pega
        CoupHappened? seen = null;
        w.Events.Subscribe<CoupHappened>(e => seen = e);
        c.Stability = w.Rule("coup_stability", 25f) - 5f;
        float stab = c.Stability;
        w.Clock.Advance();
        new PartySystem().Tick(w);

        Assert.NotNull(seen);
        Assert.Equal(rival.Id, c.Party);
        Assert.Equal(was, seen!.From);
        Assert.True(c.Stability < stab, "um golpe não deixa o país mais calmo do que o encontrou");
    }

    [Fact]
    public void QuemGovernaDaOSeuMultiplicadorAoPais()
    {
        var w = Build();
        var c = w.Countries[1];
        var gift = w.PartyDefs.Values.First(p => !string.IsNullOrEmpty(p.StatKey) && p.StatMult != 1f);
        var other = w.PartyDefs.Values.First(p => p.Id != gift.Id && p.StatKey != gift.StatKey);

        c.Party = other.Id; World.ApplyParty(w, c);
        float without = c.Stat(gift.StatKey!);
        c.Party = gift.Id; World.ApplyParty(w, c);
        float with = c.Stat(gift.StatKey!);

        Assert.Equal(without * gift.StatMult, with, 4);
        // e não toca em mais nada: o partido só mexe na coluna dele
        Assert.Single(c.PartyMult);
    }

    [Fact]
    public void APropagandaPagaPoderPoliticoEMexeNaBarra()
    {
        var w = Build();
        var c = w.Countries[1];
        var target = w.PartyDefs.Values.First(p => p.Id != c.Party);
        float cost = w.Rule("party_push_cost", 25f), points = w.Rule("party_push_points", 5f);

        c.Political = cost - 1f;
        Assert.NotNull(new PushPartyCommand(1, target.Id).Validate(w));   // sem cofre não há cartazes

        c.Political = cost + 10f;
        float had = c.Parties[target.Id];
        Assert.Null(new PushPartyCommand(1, target.Id).Validate(w));
        new PushPartyCommand(1, target.Id).Execute(w);

        Assert.Equal(10f, c.Political, 3);
        Assert.True(c.Parties[target.Id] > had);
        // os pontos saem dos outros: a opinião continua a ser uma só
        Assert.Equal(100f, c.Parties.Values.Sum(), 2);
        Assert.True(c.Parties[target.Id] - had <= points + 0.01f);
        Assert.NotNull(new PushPartyCommand(1, "partido-que-nao-existe").Validate(w));
    }

    [Fact]
    public void UmPaisCapituladoNaoVotaNemSeRevolta()
    {
        var w = Build();
        var c = w.Countries[1];
        var rival = w.PartyDefs.Values.First(p => p.Id != c.Party);
        foreach (var k in c.Parties.Keys.ToList()) c.Parties[k] = 5f;
        c.Parties[rival.Id] = 90f;
        World.NormalizeParties(c);
        c.Stability = 0f;
        c.Capitulated = true;
        string was = c.Party;

        w.Clock.Advance();
        new PartySystem().Tick(w);

        Assert.Equal(was, c.Party);
    }

    [Fact]
    public void AOpiniaoAtravessaOSave()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        foreach (var c0 in w.Countries.Values) World.SettleParties(w, c0);
        var c = w.Countries[1];
        var rival = w.PartyDefs.Values.First(p => p.Id != c.Party && p.Elections);
        c.Party = rival.Id;
        c.Parties[rival.Id] = 51f;
        World.NormalizeParties(c);
        c.NextElection = w.Clock.Day + 777;
        var before = c.Parties.ToDictionary(kv => kv.Key, kv => kv.Value);

        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, SqlWorldRepository.SchemaFromSqliteMaster(staticDb));
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);

        var (back, _) = TestWorld.Build();
        TestWorld.LinearMap(back);
        repo.LoadSave(back, save);

        var again = back.Countries[1];
        Assert.Equal(rival.Id, again.Party);
        Assert.Equal(c.NextElection, again.NextElection);
        foreach (var (id, pop) in before) Assert.Equal(pop, again.Parties[id], 2);
        // e o multiplicador do governo volta a estar aplicado sem ninguém o pedir
        if (!string.IsNullOrEmpty(rival.StatKey)) Assert.Equal(rival.StatMult, again.PartyMult[rival.StatKey!], 4);
    }
}
