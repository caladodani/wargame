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

    /// <summary>As classes de casco e de avião: cada linha de ship_class e plane_class pede uma chapa que
    /// alguém desenha. Vêem-se nas fichas do estaleiro e do hangar, uma por modelo, e ficavam caladas — a
    /// chapa que não existe sai como roda dentada e não como erro. O porta-aviões trouxe a coluna glyph ao
    /// ship_class e é por isso que esta rede se estende agora ao mar.</summary>
    [Fact]
    public void Cascos_e_avioes_pedem_chapas_que_existem()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.ShipClasses);
        Assert.NotEmpty(w.PlaneClasses);
        foreach (var g in w.ShipClasses.Values.Select(s => s.Glyph)
                           .Concat(w.PlaneClasses.Values.Select(p => p.Glyph)))
            Assert.Contains(g, Desenhados);
    }

    /// <summary>As quatro tabelas que só agora deixaram o emoji: os géneros da crónica, as pastas do
    /// gabinete, as políticas de ocupação e as gravidades de baixa. Todas se vêem em fichas e listas — a
    /// linha do tempo do Jornal, as cadeiras do gabinete, os botões da ocupação, as camas da enfermaria — e
    /// todas partiriam caladas com um nome mal escrito, que sai como roda dentada e não como erro.</summary>
    [Fact]
    public void Cronica_gabinete_ocupacao_e_baixas_pedem_chapas_que_existem()
    {
        var w = FactionTests.BuildReal();
        Assert.NotEmpty(w.ChronicleKinds);
        Assert.NotEmpty(w.CabinetSlots);
        Assert.NotEmpty(w.OccupationPolicyDefs);
        Assert.NotEmpty(w.WoundKinds);
        foreach (var g in w.ChronicleKinds.Values.Select(k => k.Glyph)
                           .Concat(w.CabinetSlots.Select(c => c.Glyph))
                           .Concat(w.OccupationPolicyDefs.Values.Select(o => o.Glyph))
                           .Concat(w.WoundKinds.Values.Select(k => k.Glyph)))
            Assert.Contains(g, Desenhados);
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

    /// <summary>Nenhum terreno de região sem linha na tabela: uma região com um terreno que a tabela não
    /// conhece anda pelo mapa com o preço de marcha por omissão e sem nome que se leia.</summary>
    [Fact]
    public void Nenhuma_regiao_com_terreno_fora_da_tabela()
    {
        var w = FactionTests.BuildReal();
        foreach (var terreno in w.Regions.Values.Select(r => r.Terrain).Distinct())
            Assert.True(w.TerrainDefs.ContainsKey(terreno), $"região com terreno sem linha: {terreno}");
    }

    /// <summary>Nenhum nome de chapa repetido na lista dos desenhados: um nome a dobrar é um `case` que
    /// nunca chega a correr, e o desenho que ele trazia perde-se sem dar sinal.</summary>
    [Fact]
    public void A_lista_de_desenhos_nao_tem_nomes_repetidos()
    {
        Assert.Equal(Desenhados.Length, Desenhados.Distinct().Count());
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
