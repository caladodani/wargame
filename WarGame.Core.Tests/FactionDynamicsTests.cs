using WarGame.Core.Commands;
using WarGame.Core.Data;
using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Systems;
using Xunit;

namespace WarGame.Core.Tests;

/// <summary>Dinâmica de facções: fundar, convidar (IA aceita só com inimigo comum), aderir, sair, persistência.</summary>
public class FactionDynamicsTests
{
    /// <summary>3 países: 1 e 2 no mapa linear, 3 sem regiões (só diplomacia).</summary>
    private static World Setup()
    {
        var (w, _) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1, Manpower = 1e9f };
        return w;
    }

    [Fact]
    public void Create_Invite_CommonEnemyAccepts_OthersRejected()
    {
        var w = Setup();
        new CreateFactionCommand(1, "Pacto").Execute(w);
        string fid = Assert.Single(w.CustomFactionIds);
        Assert.Contains(1, w.Factions[fid].Members);

        // sem inimigo comum: recusa
        var rejected = new List<FactionInviteRejected>();
        w.Events.Subscribe<FactionInviteRejected>(rejected.Add);
        new InviteToFactionCommand(1, fid, 3).Execute(w);
        Assert.Single(rejected);
        Assert.DoesNotContain(3, w.Factions[fid].Members);

        // 1 e 3 em guerra com 2 → inimigo comum → aceita
        w.StartWar(1, 2); w.StartWar(3, 2);
        new InviteToFactionCommand(1, fid, 3).Execute(w);
        Assert.Contains(3, w.Factions[fid].Members);

        // em guerra com membro nunca é convidável
        Assert.NotNull(new InviteToFactionCommand(1, fid, 2).Validate(w));
    }

    [Fact]
    public void Join_PlayerNeedsCommonEnemy_LeaveBlockedAtWar()
    {
        var w = Setup();
        w.Countries[1].IsPlayer = true;
        new CreateFactionCommand(3, "Liga").Execute(w);
        string fid = w.CustomFactionIds[0];

        Assert.NotNull(new JoinFactionCommand(1, fid).Validate(w));   // sem inimigo comum
        w.StartWar(1, 2); w.StartWar(3, 2);
        Assert.Null(new JoinFactionCommand(1, fid).Validate(w));
        new JoinFactionCommand(1, fid).Execute(w);
        Assert.Contains(1, w.Factions[fid].Members);

        Assert.NotNull(new LeaveFactionCommand(1, fid).Validate(w));  // em guerra não se sai
        w.EndWar(1, 2);
        Assert.Null(new LeaveFactionCommand(1, fid).Validate(w));
        new LeaveFactionCommand(1, fid).Execute(w);
        Assert.DoesNotContain(1, w.Factions[fid].Members);
    }

    [Fact]
    public void AiSystem_InvitesCobelligerents()
    {
        var w = Setup();
        w.Register(new AiSystem());
        new CreateFactionCommand(1, "Coligação").Execute(w);
        string fid = w.CustomFactionIds[0];
        w.StartWar(1, 2); w.StartWar(3, 2);      // 3 luta contra o mesmo inimigo
        TestWorld.Days(w, 6);                     // ai_period_days
        Assert.Contains(3, w.Factions[fid].Members);
    }

    [Fact]
    public void SaveRoundTrip_RestoresCustomFactionAndMembership()
    {
        var (w, staticDb) = TestWorld.Build();
        TestWorld.LinearMap(w);
        w.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1 };
        new CreateFactionCommand(1, "Pacto").Execute(w);
        string fid = w.CustomFactionIds[0];
        w.StartWar(1, 2); w.StartWar(3, 2);
        new InviteToFactionCommand(1, fid, 3).Execute(w);

        string schema = string.Join(";\n", staticDb.Query("SELECT sql FROM sqlite_master WHERE sql IS NOT NULL AND type IN ('table','index')")
            .Select(r => ((string)r["sql"]!).Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ").Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS "))) + ";\n";
        using var save = new MsSqliteDatabase();
        SqlWorldRepository.EnsureSaveSchema(save, schema);
        var repo = new SqlWorldRepository(staticDb);
        repo.WriteSave(w, save);
        repo.WriteSave(w, save);   // segundo save não pode duplicar PKs

        var (w2, _) = TestWorld.Build();
        TestWorld.LinearMap(w2);
        w2.Countries[3] = new Country { Id = 3, Tag = "C", Name = "Gama", CapitalRegionId = 1 };
        repo.LoadSave(w2, save);
        Assert.Equal(fid, Assert.Single(w2.CustomFactionIds));
        Assert.Equal("Pacto", w2.Factions[fid].Name);
        Assert.Equal(new[] { 1, 3 }, w2.Factions[fid].Members.OrderBy(m => m));
    }
}
