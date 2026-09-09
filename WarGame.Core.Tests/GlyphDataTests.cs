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
    internal static readonly string[] Desenhados =
    {
        "capacete", "lagarta", "obus", "asa", "ancora", "drone", "camiao",
        "fabrica", "livro", "frasco", "atomo", "bigorna", "estrada", "escudo",
        "floco", "chuva", "sol", "folha", "globo", "caixa", "punho", "gente",
        "bomba", "alvo", "luneta", "roda",
        "espadas", "pomba", "bandeira", "medalha", "coluna", "coroa", "aperto", "fita",
        "galao", "taca", "barco", "estilhaco", "pasta", "corrente", "balanca", "caveira",
        "megafone", "penso", "gota", "cruz", "paraquedas", "onda",
        // a barra de cima: o cofre e o barril não vêm de tabela nenhuma, mas são desenhos como os outros
        "cofre", "barril",
        // o chão: um desenho por terreno
        "campo", "arvore", "cidade", "montanha", "duna", "gelo",
        // o chão do céu: a pista com as marcas de cabeceira e a manga de vento (campo de aviação)
        "pista",
        // o céu: a tempestade do tempo local e as tácticas de combate
        "raio", "gancho", "brecha", "muro", "mola",
        // as classes: os cascos e os modelos de avião, que também se vêem por chapa e não por emoji
        "submarino", "conves", "caca", "bombardeiro", "carga", "praia",
        // o aço que o céu deita ao fundo: o torpedo com a esteira atrás (missão de ataque naval)
        "torpedo",
        // a guerra submarina: o sonar da caça anti-submarina
        "sonar",
        // a oficina de aviões: as ranhuras da fuselagem e as peças que lá entram
        "helice", "canhao", "porao", "antena",
    };

    [Fact]
    public void Cada_edificio_pede_uma_chapa_que_existe()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.BuildingDefs);
        foreach (var d in w.BuildingDefs.Values)
            Assert.Contains(d.Glyph, Desenhados);
    }







    /// <summary>Os depósitos: cada recurso tem chapa que existe. O recurso vê-se na ficha do que a terra
    /// dá, ao lado do dinheiro e dos homens, e um nome mal escrito saía como roda dentada calada.</summary>
    [Fact]
    public void Cada_recurso_pede_uma_chapa_que_existe()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.ResourceDefs);
        foreach (var d in w.ResourceDefs.Values)
            Assert.Contains(d.Glyph, Desenhados);
    }

    /// <summary>O chão: cada terreno tem nome que se lê e chapa que existe. Um terreno sem chapa dava uma
    /// roda dentada calada no rodapé do mapa, que é o sítio onde o jogador olha mais vezes por dia.</summary>
    [Fact]
    public void Cada_terreno_tem_nome_e_uma_chapa_que_existe()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.TerrainDefs);
        foreach (var t in w.TerrainDefs.Values)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Name), $"terreno sem nome: {t.Id}");
            Assert.Contains(t.Glyph, Desenhados);
        }
    }


    /// <summary>Nenhum nome de chapa repetido na lista dos desenhados: um nome a dobrar é um `case` que
    /// nunca chega a correr, e o desenho que ele trazia perde-se sem dar sinal.</summary>
    [Fact]
    public void A_lista_de_desenhos_nao_tem_nomes_repetidos()
    {
        Assert.Equal(Desenhados.Length, Desenhados.Distinct().Count());
    }

}
