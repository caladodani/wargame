using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Stats;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Facções (alianças defensivas). Membros e regra de guerra usam o static.db a sério (tags reais, ex.
/// RUS/EST) — só a IA (dissuasão) precisa de um mapa sintético, feito com TestWorld.LinearMap.</summary>
public class FactionTests
{
    /// <summary>Mundo carregado do static.db real (mesmo LoadStatic do jogo) — só para ler países/facções, sem mapa jogável.</summary>
    internal static World BuildReal()
    {
        var db = new MsSqliteDatabase("Data Source=data/static.db;Mode=ReadOnly");
        var units = new SqlUnitRepository(db);
        var w = new World(new DateOnly(2030, 1, 1), new DivisionStatCache(units), new ModifierEngine(units.GetModifiers()));
        new SqlWorldRepository(db).LoadStatic(w);
        return w;
    }

    private static Country ByTag(World w, string tag) => w.Countries.Values.Single(c => c.Tag == tag);

    [Fact]
    public void Membros_reais_carregados_em_NATO_e_OTSC()
    {
        var w = BuildReal();
        var nato = w.Factions["nato"];
        Assert.Contains(ByTag(w, "PRT").Id, nato.Members);
        Assert.Contains(ByTag(w, "USA").Id, nato.Members);
        var otsc = w.Factions["otsc"];
        Assert.Contains(ByTag(w, "RUS").Id, otsc.Members);
    }


    [Fact]
    public void Guerra_a_membro_da_NATO_chama_os_outros_membros_contra_o_agressor()
    {
        var w = BuildReal();
        var rus = ByTag(w, "RUS"); var est = ByTag(w, "EST"); var prt = ByTag(w, "PRT");
        var joined = new List<FactionJoinedWar>();
        w.Events.Subscribe<FactionJoinedWar>(e => joined.Add(e));

        var cmd = new DeclareWarCommand(rus.Id, est.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.True(w.AreAtWar(rus.Id, est.Id));
        Assert.True(w.AreAtWar(rus.Id, prt.Id));   // PRT é NATO: entra contra a Rússia, sem ter sido atacado
        Assert.Contains(joined, e => e.FactionId == "nato" && e.MemberCountryId == prt.Id && e.AgainstCountryId == rus.Id);
    }



}
