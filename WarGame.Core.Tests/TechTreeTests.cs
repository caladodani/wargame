using WarGame.Core.Model;
using WarGame.Core.Stats;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A árvore de investigação inteira, lida da base de dados. O utilizador disse que a pesquisa do
/// exército estava curta — a Aviação e a Marinha tinham 31 degraus cada e a Infantaria tinha três —, e
/// estes testes são o que impede a árvore de voltar a encolher: cada ramo de terra tem de ter fundura, cada
/// degrau tem de fazer alguma coisa (ou ser porta para outro), e ninguém pode pedir um degrau que não
/// existe.</summary>
public class TechTreeTests
{
    /// <summary>Os ramos de terra: os que o exército investiga. Vêm da coluna arm da tech_branch, não de uma
    /// lista em código — um ramo novo entra aqui por dizer 'terra' na sua linha.</summary>
    private static string[] Terra(World w) =>
        w.TechBranches.Values.Where(b => b.Arm == "terra").Select(b => b.Id).ToArray();

    [Fact]
    public void A_arvore_de_terra_tem_fundura()
    {
        var (w, _) = TestWorld.Build();
        var comuns = w.Techs.Values.Where(t => t.CountryTag is null).ToList();
        string[] terra = Terra(w);
        Assert.True(terra.Length >= 8, $"só {terra.Length} ramos de terra — o exército ficou sem ramos");

        foreach (string ramo in terra)
        {
            int n = comuns.Count(t => t.Branch == ramo);
            Assert.True(n >= 6, $"o ramo {ramo} só tem {n} degraus comuns — a árvore de terra voltou a encolher");
            Assert.True(w.TechBranches.ContainsKey(ramo), $"o ramo {ramo} não tem linha em tech_branch");
            Assert.NotEmpty(w.TechBranches[ramo].Glyph);
        }
        Assert.True(comuns.Count(t => terra.Contains(t.Branch)) >= 55);
    }

    [Fact]
    public void Todo_o_degrau_faz_alguma_coisa_ou_e_porta_para_outro()
    {
        var (w, db) = TestWorld.Build();
        // uma ficha com todas as marcas que os modificadores pedem: assim um degrau preso a "montanha" ou a
        // "airborne" também conta. As marcas vêm da própria tabela — as unidades que as usam nascem dos
        // ficheiros de país, que não correm aqui
        var ficha = new StatBlock();
        foreach (var r in db.Query("SELECT DISTINCT required_tag FROM modifier WHERE required_tag IS NOT NULL"))
            ficha.Tags.Add((string)r["required_tag"]!);
        string[] chaves = { "str", "str_attacker", "str_defender", "command" };
        var vazio = new ModContext();

        foreach (var t in w.Techs.Values)
        {
            if (w.TechEffects.ContainsKey(t.Id)) continue;                       // muda o país
            var ctx = new ModContext().With("tech:" + t.Id, "true");
            if (chaves.Any(k => w.Modifiers.Evaluate(k, ficha, ctx) != w.Modifiers.Evaluate(k, ficha, vazio)))
                continue;                                                         // muda o combate
            Assert.True(w.Techs.Values.Any(o => o.Requires == t.Id),
                        $"o degrau {t.Id} não muda nada nem abre caminho a ninguém");
        }
    }

    [Fact]
    public void Ninguem_pede_um_degrau_que_nao_existe_nem_anda_as_voltas()
    {
        var (w, _) = TestWorld.Build();
        foreach (var t in w.Techs.Values)
        {
            if (t.Requires is null) continue;
            Assert.True(w.Techs.ContainsKey(t.Requires), $"{t.Id} pede {t.Requires}, que não existe");

            var visto = new HashSet<string> { t.Id };
            for (string? cur = t.Requires; cur is not null; cur = w.Techs[cur].Requires)
                Assert.True(visto.Add(cur), $"a corrente de {t.Id} anda às voltas em {cur}");
        }
    }

    /// <summary>As tecnologias com que os países começam têm de existir e de ser deles: um arranque que
    /// aponta para um degrau apagado deixava o país a olhar para um nome sem ficha.</summary>
    [Fact]
    public void Os_arranques_apontam_para_degraus_que_existem()
    {
        var (w, db) = TestWorld.Build();
        foreach (var r in db.Query("SELECT country_tag,tech_id FROM country_tech"))
        {
            string id = (string)r["tech_id"]!, tag = (string)r["country_tag"]!;
            Assert.True(w.Techs.ContainsKey(id), $"{tag} começa com {id}, que não existe");
            var t = w.Techs[id];
            Assert.True(t.CountryTag is null || t.CountryTag == tag, $"{tag} começa com {id}, que é programa de {t.CountryTag}");
        }
    }
}
