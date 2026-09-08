using WarGame.Core.Model;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A ficha de combate de uma divisão desenha-se a partir da tabela unit_stat_def: cada número que
/// a divisão tem tem lá uma linha com o nome que se lê, a frase que diz o que ele faz na conta e a chapa que
/// o representa. O desenho vive do lado do Godot e estes testes não o vêem; o que se pode guardar daqui é o
/// contrato de dados — que nenhum stat chega à ficha sem nome, que nenhuma linha da tabela é nome de stat
/// nenhum, e que a chapa que a linha pede é uma que o Glyph sabe desenhar.</summary>
public class UnitStatDefTests
{
    /// <summary>Os dois números que não vêm de unit_stat: saem de colunas de unit_type e são compostos pelo
    /// DivisionStatCache (a marcha é a do batalhão mais lento, o peso na retaguarda é a soma).</summary>
    private static readonly string[] Derivados = { "mobility", "supply_use" };

    [Fact]
    public void Cada_stat_de_unidade_tem_linha_na_tabela_da_ficha()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.UnitStatDefs);
        var chaves = w.Units.AllUnitTypes().SelectMany(u => u.Stats.All.Keys).Concat(Derivados).Distinct();
        foreach (var k in chaves)
            Assert.True(w.UnitStatDefs.ContainsKey(k), $"stat sem linha em unit_stat_def: {k}");
    }

    /// <summary>Nenhuma linha a mais: uma linha que não é chave de stat nenhuma é uma linha da ficha que
    /// mostraria sempre zero, e o mais certo é ser um nome mal escrito.</summary>
    [Fact]
    public void Nenhuma_linha_da_ficha_sem_stat_que_lhe_corresponda()
    {
        var w = FactionTests.BuildReal();
        var chaves = w.Units.AllUnitTypes().SelectMany(u => u.Stats.All.Keys).Concat(Derivados).ToHashSet();
        foreach (var k in w.UnitStatDefs.Keys)
            Assert.True(chaves.Contains(k), $"unit_stat_def sem stat: {k}");
    }

    /// <summary>Nome e frase preenchidos, e ordem sem empates: a ficha lê-se de cima para baixo e duas
    /// linhas com o mesmo `sort` trocam de sítio de cada vez que se abre o cartão.</summary>
    [Fact]
    public void A_ficha_tem_nome_frase_e_ordem_sem_empates()
    {
        var w = FactionTests.BuildReal();
        foreach (var d in w.UnitStatDefs.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name), $"{d.Key} sem nome");
            Assert.False(string.IsNullOrWhiteSpace(d.Note), $"{d.Key} sem frase");
            Assert.InRange(d.Digits, 0, 3);
        }
        var sorts = w.UnitStatDefs.Values.Select(d => d.Sort).ToList();
        Assert.Equal(sorts.Count, sorts.Distinct().Count());
    }

    /// <summary>A chapa de cada linha é um desenho que existe. Um nome mal escrito não dá erro nenhum: dá
    /// uma roda dentada calada ao lado do número.</summary>
    [Fact]
    public void Cada_linha_da_ficha_pede_uma_chapa_que_existe()
    {
        var w = FactionTests.BuildReal();
        foreach (var d in w.UnitStatDefs.Values)
            Assert.Contains(d.Glyph, GlyphDataTests.Desenhados);
    }

    /// <summary>Os oito números com que se bate e se aguenta estão todos na ficha — é a tabela do HoI4 e é
    /// o que faz o cartão da divisão valer alguma coisa. Se alguém puser um deles a `shown` 0, o cartão
    /// cala-o e ninguém dá por isso.</summary>
    [Fact]
    public void Os_numeros_do_combate_estao_todos_a_mostra()
    {
        var w = FactionTests.BuildReal();
        foreach (var k in new[] { "soft_atk", "hard_atk", "piercing", "armor", "defense", "breakthrough", "hardness", "hp" })
        {
            Assert.True(w.UnitStatDefs.TryGetValue(k, out var d), $"sem linha: {k}");
            Assert.True(d!.Shown, $"número de combate escondido da ficha: {k}");
        }
    }
}
