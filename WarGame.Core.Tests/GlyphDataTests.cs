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
    /// vale mais repetir treze palavras do que deixar a tabela pedir uma chapa que ninguém desenha.</summary>
    private static readonly string[] Desenhados =
    {
        "capacete", "lagarta", "obus", "asa", "ancora", "drone", "camiao",
        "fabrica", "livro", "frasco", "atomo", "bigorna", "estrada", "escudo", "roda",
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
