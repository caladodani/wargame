using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>As chapas desenhadas vivem no lado do Godot (src/Presentation/Glyph.cs), que estes testes não
/// vêem. O que se pode guardar daqui é o outro lado do contrato: que a base de dados pede uma chapa a cada
/// sítio onde há uma para desenhar, e que os nomes que pede são nomes de desenho — minúsculas sem acentos,
/// como o Glyph os conhece. Um ramo novo sem linha em tech_branch não dava erro nenhum no jogo: dava uma
/// roda dentada calada em cima da coluna dele, e é isso que aqui se recusa.</summary>
public class GlyphDataTests
{
    /// <summary>Os nomes que o Glyph.cs sabe desenhar, copiados à mão porque a lista de lá é do projecto
    /// Godot. Se lá se acrescentar um desenho, acrescenta-se aqui — é a única duplicação e é de propósito:
    /// vale mais repetir a lista do que deixar uma tabela pedir uma chapa que ninguém desenha.</summary>
    private static readonly string[] Desenhados =
    {
        "capacete", "lagarta", "obus", "asa", "ancora", "drone", "camiao",
        "fabrica", "livro", "frasco", "atomo", "bigorna", "estrada", "escudo",
        "floco", "chuva", "sol", "folha", "globo", "caixa", "punho", "gente",
        "bomba", "alvo", "luneta", "roda",
    };

    [Fact]
    public void Cada_edificio_pede_uma_chapa_que_existe()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.BuildingDefs);
        foreach (var d in w.BuildingDefs.Values)
            Assert.Contains(d.Glyph, Desenhados);
    }

    [Fact]
    public void Cada_ramo_da_arvore_tem_linha_em_tech_branch_com_chapa_que_existe()
    {
        var w = FactionTests.BuildReal();
        foreach (var branch in w.Techs.Values.Select(t => t.Branch).Distinct())
        {
            Assert.True(w.TechBranches.ContainsKey(branch), $"ramo sem linha em tech_branch: {branch}");
            Assert.Contains(w.TechBranches[branch].Glyph, Desenhados);
        }
    }

    /// <summary>As outras tabelas com coluna glyph: estações, modos de mapa e missões de ar e mar. Todas
    /// se desenham em sítios que o jogador vê a toda a hora — a fita dos modos, a chapa da estação, as
    /// fichas das asas e das esquadras — e todas partiriam em silêncio com um nome mal escrito.</summary>
    [Fact]
    public void Estacoes_modos_e_missoes_pedem_chapas_que_existem()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.SeasonDefs);
        Assert.NotEmpty(w.MapModeDefs);
        Assert.NotEmpty(w.AirMissionDefs);
        Assert.NotEmpty(w.NavalMissionDefs);
        foreach (var g in w.SeasonDefs.Values.Select(s => s.Glyph)
                           .Concat(w.MapModeDefs.Values.Select(m => m.Glyph))
                           .Concat(w.AirMissionDefs.Values.Select(m => m.Glyph))
                           .Concat(w.NavalMissionDefs.Values.Select(m => m.Glyph)))
            Assert.Contains(g, Desenhados);
    }

    /// <summary>Nenhum ramo a mais: uma linha em tech_branch que não é ramo de tecnologia nenhuma é uma
    /// coluna que nunca se desenha, e o mais certo é ser um nome mal escrito.</summary>
    [Fact]
    public void Nenhum_ramo_em_tech_branch_sem_tecnologias()
    {
        var w = FactionTests.BuildReal();
        var usados = w.Techs.Values.Select(t => t.Branch).ToHashSet();
        foreach (var id in w.TechBranches.Keys)
            Assert.True(usados.Contains(id), $"tech_branch sem tecnologias: {id}");
    }
}
