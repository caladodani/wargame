using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Baixas no comando. Um comandante destacado ganhava experiência campanha fora e nunca lhe
/// acontecia nada — a guerra não lhe tocava. Agora, cada batalha que o exército dele trava é uma
/// hipótese de o perder: sai ferido por uns dias (e enquanto está no hospital não vale nada, nem ao
/// exército nem ao país) ou fica lá, e com ele a folha de serviço toda.
///
/// Caído o comandante, o exército não fica à espera: passa para as mãos do primeiro comandante livre do
/// estado-maior — o interino leva o seu próprio bónus, não o do homem que substituiu. Isso é o preço a
/// sério de deixar um marechal na primeira linha.
///
/// Nada disto está escrito em C#: a gravidade vem da tabela wound_kind (dias, peso no sorteio, se é
/// fatal) e a probabilidade das regras wound_chance e wound_loss_mult.</summary>
public sealed class CommandCasualtySystem : ISystem
{
    public string Name => "CommandCasualty";
    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);
        Recover(w);
    }

    /// <summary>Liga-se ao barramento. Idempotente por mundo (padrão do GeneralXpSystem).</summary>
    public void Bind(World w)
    {
        _bound = w;
        w.Events.Subscribe<BattleEnded>(e => OnBattle(w, e));
    }

    /// <summary>Quem já cumpriu o tempo de hospital volta ao serviço — e volta a contar para os stats.</summary>
    private static void Recover(World w)
    {
        foreach (var c in w.Countries.Values)
        {
            if (c.GeneralWound.Count == 0) continue;
            var back = c.GeneralWound.Where(kv => kv.Value <= w.Clock.Day).Select(kv => kv.Key).ToList();
            foreach (var gen in back)
            {
                c.GeneralWound.Remove(gen);
                w.Events.Publish(new GeneralRecovered(c.Id, gen));
            }
            if (back.Count > 0) World.ApplyGenerals(w, c);
        }
    }

    /// <summary>Uma batalha acabou: os comandantes dos exércitos que lá estavam arriscaram a pele. Quem
    /// perdeu arrisca mais (wound_loss_mult) — é a retirada que mata comandantes.</summary>
    private static void OnBattle(World w, BattleEnded e)
    {
        if (w.WoundKinds.Count == 0 || w.ArmyGroups.Count == 0) return;
        float chance = w.Rule("wound_chance", 0.03f);
        if (chance <= 0f) return;
        float loss = w.Rule("wound_loss_mult", 2f);
        int winner = e.AttackerWon ? e.AttackerCountryId : e.DefenderCountryId;

        foreach (int cid in new[] { e.AttackerCountryId, e.DefenderCountryId })
            Roll(w, cid, e.RegionId, chance * (cid == winner ? 1f : loss));
    }

    /// <summary>Sorteia a sorte dos comandantes deste país naquela região. Um exército conta uma vez, por
    /// muitas divisões que lá tenha: quem arrisca é o homem, não a contagem de unidades.</summary>
    private static void Roll(World w, int countryId, int regionId, float chance)
    {
        if (!w.Regions.TryGetValue(regionId, out var reg) || !w.Countries.TryGetValue(countryId, out var c)) return;

        var done = new HashSet<int>();
        foreach (int id in reg.DivisionIds.ToList())
        {
            if (!w.Divisions.TryGetValue(id, out var d) || d.CountryId != countryId || d.GroupId is not int gid) continue;
            if (!done.Add(gid) || !w.ArmyGroups.TryGetValue(gid, out var g) || g.GeneralId is not string gen) continue;
            if (w.IsWounded(countryId, gen)) continue;                 // já está fora: não se fere duas vezes
            if (w.Rng.NextDouble() >= chance) continue;
            Strike(w, c, g, gen, regionId);
        }
    }

    /// <summary>A baixa em si: sorteia a gravidade pelos pesos da tabela, tira o homem de serviço (ou de
    /// vez) e entrega o exército a quem estiver livre.</summary>
    public static void Strike(World w, Country c, ArmyGroup g, string generalId, int regionId)
    {
        var kind = Draw(w);
        if (kind is null) return;

        if (kind.Fatal)
        {
            c.Generals.Remove(generalId);
            c.GeneralXp.Remove(generalId);
            c.GeneralWound.Remove(generalId);
            w.Events.Publish(new GeneralKilled(c.Id, generalId, regionId));
        }
        else
        {
            c.GeneralWound[generalId] = w.Clock.Day + Math.Max(1, kind.Days);
            w.Events.Publish(new GeneralWounded(c.Id, generalId, kind.Id, Math.Max(1, kind.Days)));
        }

        g.GeneralId = StandIn(w, c, generalId);
        w.Events.Publish(new CommandHandedOver(c.Id, g.Id, g.GeneralId));
        World.ApplyGenerals(w, c);
    }

    /// <summary>Substituto: o primeiro comandante do estado-maior que não esteja ferido nem já a comandar
    /// outro exército. Sem ninguém livre, o exército fica sem comando até o jogador tratar disso.</summary>
    private static string? StandIn(World w, Country c, string fallen)
    {
        var busy = w.ArmyGroups.Values.Where(x => x.CountryId == c.Id && x.GeneralId is not null)
                                      .Select(x => x.GeneralId!).ToHashSet();
        foreach (var id in c.Generals)
            if (id != fallen && !busy.Contains(id) && !w.IsWounded(c.Id, id)) return id;
        return null;
    }

    /// <summary>Gravidade sorteada pelos pesos da tabela: muitos arranhões, poucos caixões.</summary>
    private static WoundKind? Draw(World w)
    {
        float total = w.WoundKinds.Values.Sum(k => MathF.Max(0f, k.Weight));
        if (total <= 0f) return null;
        double roll = w.Rng.NextDouble() * total;
        foreach (var k in w.WoundKinds.Values.OrderBy(k => k.Fatal).ThenBy(k => k.Days))
        {
            roll -= MathF.Max(0f, k.Weight);
            if (roll <= 0d) return k;
        }
        return w.WoundKinds.Values.Last();
    }
}
