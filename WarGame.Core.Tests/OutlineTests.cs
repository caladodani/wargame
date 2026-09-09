using WarGame.Core.Data;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>A coluna do estado (o "outliner" do HoI4): cada família de coisas a andar dá uma linha com
/// barra e dias, as secções vêm da tabela, e o que não está a andar aparece como ranhura vazia — que é
/// metade do que aquela coluna serve.</summary>
public class OutlineTests
{
    private static (World w, Country c) Small()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        return (w, w.Countries[1]);
    }

    [Fact]
    public void As_seccoes_vem_da_tabela_por_ordem_e_com_chapa()
    {
        var (w, _) = Small();
        var secs = Outline.Sections(w);
        Assert.NotEmpty(secs);
        for (int i = 1; i < secs.Count; i++)
            Assert.True(secs[i - 1].Sort <= secs[i].Sort, "secções fora de ordem");
        foreach (var s in secs)
        {
            Assert.NotEqual("", s.Name);
            Assert.Contains(s.Glyph, GlyphDataTests.Desenhados);
            Assert.NotEqual("", s.Target);
        }
    }

    [Fact]
    public void O_foco_em_curso_da_uma_linha_com_os_dias_que_faltam()
    {
        var (w, c) = Small();
        w.Focuses["f1"] = new Focus("f1", 1, "Rearmar", "", 20, null, 1);
        c.CurrentFocus = "f1"; c.FocusProgress = 5f;

        var row = Assert.Single(Outline.Rows(w, c, "foco"));
        Assert.Equal("Rearmar", row.Title);
        Assert.Equal(0.25f, row.Progress, 0.01f);
        Assert.Equal(15, row.Days);
        Assert.True(row.HasBar);
    }

    [Fact]
    public void Sem_foco_escolhido_a_coluna_diz_que_falta_um()
    {
        var (w, c) = Small();
        w.Focuses["f1"] = new Focus("f1", 1, "Rearmar", "", 20, null, 1);
        var row = Assert.Single(Outline.Rows(w, c, "foco"));
        Assert.Equal("Sem foco", row.Title);
        Assert.False(row.HasBar);
        // um país sem focos nenhuns na base de dados não leva linha nenhuma: não é falta, é não haver
        Assert.Empty(Outline.Rows(w, w.Countries[2], "foco"));
    }

    [Fact]
    public void Cada_linha_de_investigacao_da_a_conta_dos_dias_e_o_laboratorio_parado_aparece()
    {
        var (w, c) = Small();
        var tech = w.Techs.Values.First();
        c.Research[tech.Id] = tech.Cost / 2f;

        var rows = Outline.Rows(w, c, "investigacao");
        var line = rows[0];
        Assert.Equal(tech.Name, line.Title);
        Assert.Equal(0.5f, line.Progress, 0.01f);
        Assert.True(line.Days > 0);
        // as ranhuras que sobram são linhas sem barra: é assim que se vê que há laboratórios parados
        int free = ResearchSystem.FreeSlots(w, c);
        Assert.Equal(free, rows.Count(r => !r.HasBar));
        Assert.Equal(1 + free, rows.Count);
    }

    [Fact]
    public void A_fila_de_producao_traz_nome_progresso_e_o_que_a_trava()
    {
        var (w, c) = Small();
        float cost = w.OrderCost(new ProductionOrder { TemplateId = TestWorld.Inf });
        c.Queue.Add(new ProductionOrder { TemplateId = TestWorld.Inf, Progress = cost / 4f });

        var row = Assert.Single(Outline.Rows(w, c, "producao"));
        Assert.Equal("Inf", row.Title);
        Assert.Equal(0.25f, row.Progress, 0.01f);
        Assert.NotEqual("", row.Note);
    }

    [Fact]
    public void As_obras_de_uma_provincia_aparecem_com_a_provincia_e_o_salto_para_o_mapa()
    {
        var (w, c) = Small();
        var r = w.Regions[2];
        r.Building = true; r.BuildProgress = 6f;
        r.RailBuilding = true; r.RailProgress = 10f;

        var rows = Outline.Rows(w, c, "obras");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, x => Assert.Equal(2, x.RegionId));
        Assert.All(rows, x => Assert.Equal("R2", x.Note));
        // ordenadas pelo que acaba primeiro: a via férrea (10 dias) antes das estradas (24)
        Assert.Equal("Via férrea", rows[0].Title);
        Assert.Equal(10, rows[0].Days);
        Assert.Equal("Estradas", rows[1].Title);
        Assert.Equal(24, rows[1].Days);
        // obra em terra que não é nossa não é obra nossa
        Assert.Empty(Outline.Rows(w, w.Countries[2], "obras"));
    }

    [Fact]
    public void Um_exercito_mostra_divisoes_postura_frente_e_o_plano_de_batalha()
    {
        var (w, c) = Small();
        var g = new ArmyGroup { Id = 1, CountryId = 1, Name = "1.º Exército", FrontCountryId = 2,
                                Stance = GroupStance.Advance, Planning = w.Rule("planning_max", 1f) / 2f };
        w.ArmyGroups[1] = g;
        TestWorld.AddDivision(w, 10, 1, TestWorld.Inf, 1).GroupId = 1;
        g.Divisions.Add(10);

        var row = Assert.Single(Outline.Rows(w, c, "exercitos"));
        Assert.Equal("1.º Exército", row.Title);
        Assert.Contains("1 div", row.Note);
        Assert.Contains("a avançar", row.Note);
        Assert.Contains("Beta", row.Note);
        Assert.Equal(0.5f, row.Progress, 0.01f);

        // parado e sem frente não tem plano nenhum: a linha fica sem barra
        g.Stance = GroupStance.Hold; g.FrontCountryId = null;
        Assert.False(Outline.Rows(w, c, "exercitos")[0].HasBar);
    }

    [Fact]
    public void As_asas_no_ceu_e_as_esquadras_no_mar_dizem_onde_estao_e_quantas_sao()
    {
        var (w, c) = Small();
        var air = new AirMission { CountryId = 1, RegionId = 3, MissionId = w.AirMissionDefs.Keys.First(), Name = "Asa Azul" };
        air.Wings = 12f; w.AirMissions.Add(air);
        var sea = new NavalMission { CountryId = 1, RegionId = 2, MissionId = w.NavalMissionDefs.Keys.First() };
        sea.Ships = 4f; w.NavalMissions.Add(sea);

        var a = Assert.Single(Outline.Rows(w, c, "ar"));
        Assert.Equal("Asa Azul", a.Title);
        Assert.Contains("R3", a.Note);
        Assert.Contains("12", a.Note);
        Assert.Equal(3, a.RegionId);
        Assert.False(a.HasBar);          // uma asa no céu não está a caminho de lado nenhum

        var s = Assert.Single(Outline.Rows(w, c, "mar"));
        Assert.Contains("R2", s.Note);
        Assert.Contains("4", s.Note);
        Assert.Equal(2, s.RegionId);
    }

    [Fact]
    public void Uma_missao_com_prazo_mostra_a_meta_e_os_dias_que_restam()
    {
        var (w, c) = Small();
        w.DecisionDefs["m"] = new DecisionDef("m", "Escalar a fábrica", "industria", "", 0f, 0f, 0f, 0f,
                                              0, 0, 30, "factories", 4f, 50f, 2f, 20f, 3f, "fabrica", 1);
        w.ActiveDecisions.Add(new ActiveDecision { CountryId = 1, DecisionId = "m", UntilDay = 30,
                                                   MissionUntil = w.Clock.Day + 12, GoalBase = Decisions.Metric(w, c, "factories") });

        var row = Assert.Single(Outline.Rows(w, c, "missoes"));
        Assert.Equal("Escalar a fábrica", row.Title);
        Assert.Equal(12, row.Days);
        Assert.NotEqual("", row.Note);
        Assert.Equal(0f, row.Progress, 0.01f);
    }

    [Fact]
    public void A_coluna_inteira_traz_todas_as_seccoes_e_a_conta_bate_certo()
    {
        var (w, c) = Small();
        c.Queue.Add(new ProductionOrder { TemplateId = TestWorld.Inf });
        w.Regions[1].Building = true;

        var board = Outline.Board(w, 1);
        Assert.Equal(Outline.Sections(w).Count, board.Count);
        Assert.Equal(board.Sum(b => b.Rows.Count), Outline.Count(w, 1));
        foreach (var (sec, rows) in board)
            Assert.All(rows, r => Assert.Equal(sec.Id, r.Section));
        Assert.Contains("secções", Outline.Smoke(w, 1));
        // país que não existe não rebenta a coluna
        Assert.Empty(Outline.Board(w, 999));
    }

    [Fact]
    public void As_seccoes_do_mundo_a_serio_estao_todas_ligadas()
    {
        var w = FactionTests.BuildReal();
        var secs = Outline.Sections(w);
        Assert.Equal(8, secs.Count);
        var c = w.Countries.Values.First();
        // toda a secção semeada tem de ser uma que a coluna sabe encher: uma secção que ninguém lê seria
        // uma faixa muda no ecrã, e é isso que aqui se recusa
        string[] known = { "foco", "investigacao", "producao", "obras", "exercitos", "ar", "mar", "missoes" };
        foreach (var s in secs)
        {
            Assert.Contains(s.Id, known);
            Outline.Rows(w, c, s.Id);   // não rebenta em mundo a sério
        }
    }
}
