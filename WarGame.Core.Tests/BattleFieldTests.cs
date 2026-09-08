using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>As condições do campo (BattleField). O ecrã de batalha dizia-as numa linha corrida —
/// "Montanha · 3 dias · frente de 3 por lado · 🏰 forte 2 · 🌊 rio pelo meio" — e o que aqui se guarda é
/// que a barra de chapas que a substituiu não inventa números: o chão é o do combate, o forte é o que o
/// combate paga, a frente é a que a batalha usa e o tempo é o que o desgaste tira.</summary>
public class BattleFieldTests
{
    /// <summary>O rio de uma região é `init`: pôr-se um rio é fazer outra região, não mexer nesta.</summary>
    private static (World w, Region r) Build(string terrain = "mountain", bool river = false, int fort = 0)
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w, terrain: terrain);
        var r = new Region { Id = 1, Name = "R1", OwnerId = 1, InitialOwnerId = 1, ControllerId = 1,
                             Terrain = terrain, River = river, Population = 10_000_000, Fort = fort };
        r.Neighbours.Add(2);
        w.Regions[1] = r;
        return (w, r);
    }

    /// <summary>O chão e a frente estão sempre lá: não há batalha sem chão nem sem frente.</summary>
    [Fact]
    public void O_chao_e_a_frente_estao_sempre_no_campo()
    {
        var (w, r) = Build("plain");
        var parts = BattleField.Parts(w, r);
        Assert.Equal(2, parts.Count);
        Assert.Equal("frente", parts[^1].Name);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p.Note)));
    }

    /// <summary>O número do chão é o do GroundSystem, que é o do combate — e a explicação traz as duas
    /// pontas, o que vale a quem assalta e a quem espera.</summary>
    [Fact]
    public void O_chao_do_campo_e_o_do_combate()
    {
        var (w, r) = Build();
        var chao = BattleField.Parts(w, r)[0];
        Assert.Equal($"×{GroundSystem.Terrain(w, r, true):0.00}", chao.Value);
        Assert.Contains($"×{GroundSystem.Terrain(w, r, false):0.00}", chao.Note);
        Assert.Equal(w.TerrainDefs["mountain"].Name.ToLowerInvariant(), chao.Name);
    }

    /// <summary>O rio só aparece onde há rio, e o que ele custa é a diferença entre o mesmo chão com e sem
    /// ele — não um número escrito à mão.</summary>
    [Fact]
    public void O_rio_diz_se_pela_diferenca_do_mesmo_chao_sem_rio()
    {
        var (w, r) = Build();
        Assert.DoesNotContain(BattleField.Parts(w, r), p => p.Name == "rio");
        Assert.Equal(1f, BattleField.RiverBite(w, r));

        (w, r) = Build(river: true);
        float esperado = GroundSystem.Terrain(w, "mountain", true, true) / GroundSystem.Terrain(w, "mountain", false, true);
        Assert.Equal(esperado, BattleField.RiverBite(w, r), 3);
        var rio = Assert.Single(BattleField.Parts(w, r), p => p.Name == "rio");
        Assert.Equal($"×{esperado:0.00}", rio.Value);
    }

    /// <summary>O forte do campo é o que o combate paga a quem defende, e só aparece quando existe.</summary>
    [Fact]
    public void O_forte_do_campo_e_o_que_o_combate_paga()
    {
        var (w, r) = Build();
        Assert.DoesNotContain(BattleField.Parts(w, r), p => p.Name.StartsWith("forte"));
        r.Fort = 2;
        var forte = Assert.Single(BattleField.Parts(w, r), p => p.Name == "forte 2");
        Assert.Equal($"×{GroundSystem.FortDefence(w, r):0.00}", forte.Value);
    }

    /// <summary>A frente do campo é a mesma que a batalha usa para escolher quem bate e quem espera — e o
    /// rio aperta-a, como o Frontage manda.</summary>
    [Fact]
    public void A_frente_do_campo_e_a_da_batalha()
    {
        var (w, r) = Build("plain");
        var frente = Assert.Single(BattleField.Parts(w, r), p => p.Name == "frente");
        Assert.Equal($"{Frontage.Width(w, r)}", frente.Value);

        var (w2, r2) = Build("plain", river: true);
        var comRio = Assert.Single(BattleField.Parts(w2, r2), p => p.Name == "frente");
        Assert.Equal($"{Frontage.Width(w2, r2)}", comRio.Value);
        Assert.True(int.Parse(comRio.Value) < int.Parse(frente.Value));
    }

    /// <summary>O tempo do campo é o desgaste que o WeatherSystem tira mesmo, neste chão. Sem estação
    /// carregada não há parcela nenhuma — o mundo de teste anda com tempo neutro.</summary>
    [Fact]
    public void O_tempo_do_campo_e_o_desgaste_que_o_sistema_tira()
    {
        var (w, r) = Build();
        Assert.DoesNotContain(BattleField.Parts(w, r), p => p.Glyph == "floco");
        TestWorld.Season(w, "inverno");
        var inverno = w.SeasonDefs["inverno"];
        var tempo = Assert.Single(BattleField.Parts(w, r), p => p.Name == inverno.Name.ToLowerInvariant());
        float bite = WeatherSystem.BiteOn(w, r.Terrain);
        Assert.Equal(bite <= 0f ? "—" : $"−{bite:0.0}", tempo.Value);
        Assert.Contains($"×{inverno.MoveMult:0.00}", tempo.Note);
    }

    /// <summary>Toda a chapa que o campo pede é uma chapa que o Glyph sabe desenhar — em qualquer terreno do
    /// mundo a sério e em qualquer estação.</summary>
    [Fact]
    public void Todas_as_chapas_do_campo_existem()
    {
        var w = FactionTests.BuildReal();
        foreach (var estacao in w.SeasonDefs.Keys)
        {
            TestWorld.Season(w, estacao);
            foreach (var r in w.Regions.Values.Take(120))
                foreach (var p in BattleField.Parts(w, r))
                    Assert.Contains(p.Glyph, GlyphDataTests.Desenhados);
        }
    }

    /// <summary>A linha do rodapé é a mesma barra dita por palavras: uma parcela por chapa, pela mesma
    /// ordem. Assim o mapa e o ecrã de batalha nunca dizem coisas diferentes do mesmo sítio.</summary>
    [Fact]
    public void A_linha_do_rodape_diz_o_mesmo_que_as_chapas()
    {
        var (w, r) = Build(river: true, fort: 1);
        var parts = BattleField.Parts(w, r);
        string linha = BattleField.Line(w, r);
        Assert.Equal(parts.Count - 1, linha.Count(ch => ch == '·'));
        foreach (var p in parts) Assert.Contains($"{p.Name} {p.Value}", linha);
    }
}
