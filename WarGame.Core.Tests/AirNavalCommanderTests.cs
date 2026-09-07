using WarGame.Core.Commands;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Comandantes de asa e de esquadra. O estado-maior só tinha generais de terra: um país podia
/// ter três exércitos bem comandados e nem uma cadeira para o homem que manda subir os caças ou para o
/// almirante que governa a linha de batalha. Agora cada comandante tem uma arma (general.domain), cada
/// arma tem as suas cadeiras (general_slots, air_general_slots, navy_general_slots) e uma nomeação de asa
/// ou de esquadra paga-se também com a experiência dessa arma — dinheiro sozinho não compra quem sabe voar.
///
/// A regra que isto tudo respeita: encher o comando de terra não pode impedir que se chame um almirante,
/// e o que um comandante multiplica tem de ser da arma dele (senão o efeito não chegava a lado nenhum,
/// porque o motor aéreo só lê as chaves do ar e o naval só lê as do mar).</summary>
public class AirNavalCommanderTests
{
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.GeneralDefs["ter_a"] = new GeneralDef("ter_a", "Ofensiva", "attack", 1.10f, 100f);
        w.GeneralDefs["ter_b"] = new GeneralDef("ter_b", "Defesa", "defense", 1.10f, 100f);
        w.GeneralDefs["ter_c"] = new GeneralDef("ter_c", "Indústria", "industry", 1.08f, 100f);
        w.GeneralDefs["ar_a"] = new GeneralDef("ar_a", "Caça", "air_losses", 0.92f, 100f, Domain: World.Air, Xp: 30f);
        w.GeneralDefs["ar_b"] = new GeneralDef("ar_b", "Bombardeamento", "air_bombing", 1.10f, 100f, Domain: World.Air, Xp: 30f);
        w.GeneralDefs["ar_c"] = new GeneralDef("ar_c", "Material", "air_upkeep", 0.90f, 100f, Domain: World.Air, Xp: 20f);
        w.GeneralDefs["mar_a"] = new GeneralDef("mar_a", "Esquadra", "naval_losses", 0.92f, 100f, Domain: World.Sea, Xp: 30f);
        w.GeneralDefs["mar_b"] = new GeneralDef("mar_b", "Corso", "naval_blockade", 1.12f, 100f, Domain: World.Sea, Xp: 30f);
        w.Rules["general_slots"] = 2f;
        w.Rules["air_general_slots"] = 1f;
        w.Rules["navy_general_slots"] = 1f;
        var c = w.Countries[1];
        c.Money = 5000f; c.AirXp = 200f; c.NavyXp = 200f; c.ArmyXp = 200f;
        return w;
    }

    [Fact]
    public void EachArmSitsInItsOwnChairs()
    {
        var w = Setup();
        new HireGeneralCommand(1, "ter_a").Execute(w);
        new HireGeneralCommand(1, "ter_b").Execute(w);
        var c = w.Countries[1];

        // comando de terra cheio: mais nenhum general de terra entra...
        Assert.Equal("estado-maior completo", new HireGeneralCommand(1, "ter_c").Validate(w));
        // ...mas a asa e a esquadra continuam com as cadeiras delas vazias
        Assert.Null(new HireGeneralCommand(1, "ar_a").Validate(w));
        Assert.Null(new HireGeneralCommand(1, "mar_a").Validate(w));

        new HireGeneralCommand(1, "ar_a").Execute(w);
        Assert.Equal("estado-maior completo", new HireGeneralCommand(1, "ar_b").Validate(w));
        Assert.Null(new HireGeneralCommand(1, "mar_a").Validate(w));   // o ar cheio não fecha o mar

        Assert.Equal(2, w.GeneralsInService(c, World.Land));
        Assert.Equal(1, w.GeneralsInService(c, World.Air));
        Assert.Equal(0, w.GeneralsInService(c, World.Sea));
        Assert.Equal(2, w.GeneralSlots(World.Land));
        Assert.Equal(1, w.GeneralSlots(World.Air));
        Assert.Equal(World.Air, w.DomainOfGeneral("ar_a"));
        Assert.Equal(World.Land, w.DomainOfGeneral("nao_existe"));     // um id desconhecido conta como de terra
    }

    [Fact]
    public void AWingCommanderCostsFlyingHoursOnTopOfTheMoney()
    {
        var w = Setup();
        var c = w.Countries[1];
        c.AirXp = 12f;

        Assert.Equal("faltam 18 de experiência aérea", new HireGeneralCommand(1, "ar_a").Validate(w));
        Assert.Null(new HireGeneralCommand(1, "ter_a").Validate(w));   // o de terra não pede nada disto

        c.AirXp = 30f;
        Assert.Null(new HireGeneralCommand(1, "ar_a").Validate(w));
        c.Money = 10f;
        Assert.Equal("pontos de produção insuficientes", new HireGeneralCommand(1, "ar_a").Validate(w));
    }

    [Fact]
    public void HiringSpendsOnlyThePocketOfItsOwnArm()
    {
        var w = Setup();
        var c = w.Countries[1];
        new HireGeneralCommand(1, "mar_a").Execute(w);

        Assert.Equal(170f, c.NavyXp, 2);          // 200 − 30
        Assert.Equal(200f, c.AirXp, 2);
        Assert.Equal(200f, c.ArmyXp, 2);
        Assert.Equal(4900f, c.Money, 2);

        new HireGeneralCommand(1, "ter_a").Execute(w);
        Assert.Equal(200f, c.ArmyXp, 2);          // um general de terra não custa experiência nenhuma
    }

    [Fact]
    public void WhatTheyMultiplyReachesTheArmTheyCommand()
    {
        var w = Setup();
        var c = w.Countries[1];
        Assert.Equal(1f, c.Stat("air_losses", 1f), 3);
        Assert.Equal(1f, c.Stat("naval_blockade", 1f), 3);

        new HireGeneralCommand(1, "ar_a").Execute(w);
        new HireGeneralCommand(1, "mar_b").Execute(w);

        Assert.Equal(0.92f, c.Stat("air_losses", 1f), 3);      // é o que o AirMissionSystem pergunta ao país
        Assert.Equal(1.12f, c.Stat("naval_blockade", 1f), 3);  // e o NavalMissionSystem para o bloqueio
        Assert.Equal(1f, c.Stat("naval_escort", 1f), 3);       // o que ninguém comanda fica como estava

        new DismissGeneralCommand(1, "ar_a").Execute(w);
        Assert.Equal(1f, c.Stat("air_losses", 1f), 3);
    }

    [Fact]
    public void DismissingFreesAChairOfThatArmOnly()
    {
        var w = Setup();
        new HireGeneralCommand(1, "ar_a").Execute(w);
        new HireGeneralCommand(1, "ter_a").Execute(w);
        new HireGeneralCommand(1, "ter_b").Execute(w);
        var c = w.Countries[1];

        Assert.Equal("estado-maior completo", new HireGeneralCommand(1, "ar_b").Validate(w));
        new DismissGeneralCommand(1, "ter_a").Execute(w);
        Assert.Equal("estado-maior completo", new HireGeneralCommand(1, "ar_b").Validate(w));  // libertou-se terra
        new DismissGeneralCommand(1, "ar_a").Execute(w);
        Assert.Null(new HireGeneralCommand(1, "ar_b").Validate(w));
        Assert.Equal(1, w.GeneralsInService(c, World.Land));
        Assert.Equal(0, w.GeneralsInService(c, World.Air));
    }

    [Fact]
    public void TheAiFillsTheThreeCommandsAndNeverHiresWhatItCannotPay()
    {
        var w = Setup();
        var c = w.Countries[1];
        c.Money = 20000f; c.AirXp = 30f; c.NavyXp = 0f;
        w.Register(new AiSystem());

        for (int i = 0; i < 12; i++) w.Tick();

        Assert.Equal(2, w.GeneralsInService(c, World.Land));
        Assert.Equal(1, w.GeneralsInService(c, World.Air));   // a cadeira de asa é uma e chegava para uma
        Assert.Equal(0, w.GeneralsInService(c, World.Sea));   // sem experiência naval não nomeia almirante
        Assert.All(c.Generals, id => Assert.True(World.Xp(c, w.DomainOfGeneral(id)) >= 0f));

        c.NavyXp = 100f;
        w.Tick();
        Assert.Equal(1, w.GeneralsInService(c, World.Sea));
    }

    [Fact]
    public void TheRealStaffHasCommandersForTheThreeArms()
    {
        var w = FactionTests.BuildReal();
        Assert.Equal(3, w.GeneralDefs.Values.Count(g => g.Domain == World.Air));
        Assert.Equal(3, w.GeneralDefs.Values.Count(g => g.Domain == World.Sea));
        Assert.True(w.GeneralDefs.Values.Count(g => g.Domain == World.Land) > 20);

        Assert.Equal(3, w.GeneralSlots(World.Land));
        Assert.Equal(2, w.GeneralSlots(World.Air));
        Assert.Equal(2, w.GeneralSlots(World.Sea));

        var arms = new Dictionary<string, string[]>
        {
            [World.Air] = new[] { "air_losses", "air_bombing", "air_upkeep" },
            [World.Sea] = new[] { "naval_losses", "naval_upkeep", "naval_blockade", "naval_escort", "naval_patrol" },
        };
        foreach (var (domain, keys) in arms)
            foreach (var g in w.GeneralDefs.Values.Where(g => g.Domain == domain))
            {
                Assert.Contains(g.StatKey, keys);
                bool cheaper = g.StatKey.EndsWith("_losses") || g.StatKey.EndsWith("_upkeep");
                Assert.True(cheaper ? g.Mult < 1f : g.Mult > 1f, $"{g.Id}: {g.StatKey}={g.Mult} com o sinal trocado");
                Assert.True(g.Xp > 0f, $"{g.Id} não pede experiência da arma");
                Assert.NotEqual("", g.Note);
            }
    }

    [Fact]
    public void ARealAdmiralIsHiredWithMoneyAndSeaTimeAndReachesTheFleet()
    {
        var w = FactionTests.BuildReal();
        var prt = w.Countries.Values.Single(c => c.Tag == "PRT");
        var def = w.GeneralDefs["gen_mar_corso"];
        prt.Money = 5000f; prt.NavyXp = 0f;

        Assert.StartsWith("faltam ", new HireGeneralCommand(prt.Id, def.Id).Validate(w));
        prt.NavyXp = def.Xp;
        var cmd = new HireGeneralCommand(prt.Id, def.Id);
        Assert.Null(cmd.Validate(w));
        cmd.Execute(w);

        Assert.Contains(def.Id, prt.Generals);
        Assert.Equal(0f, prt.NavyXp, 2);
        Assert.Equal(def.Mult, prt.Stat("naval_blockade", 1f), 3);
        Assert.Equal(1f, prt.Stat("air_bombing", 1f), 3);
        Assert.Contains(w.GeneralPool(prt), g => g.Domain == World.Air);   // e a asa continua por chamar
    }
}
