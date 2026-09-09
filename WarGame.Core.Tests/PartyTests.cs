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
