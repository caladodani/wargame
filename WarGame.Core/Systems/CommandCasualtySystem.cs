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
/// O céu e o mar são iguais: o comandante de asa voa com as asas que manda e o de esquadra vai ao mar
/// com ela. Onde há combate de aviões ou de navios, o estado-maior dessa arma arrisca-se — e aí não há
/// exército para entregar a ninguém, o que se perde é o que o homem dava ao país inteiro enquanto está
/// fora. Foi por isso que a guerra aérea e a naval passaram a anunciar os combates
/// (AirCombatEnded/SeaCombatEnded): sem isso, um almirante morria de velho.
///
/// Nada disto está escrito em C#: a gravidade vem da tabela wound_kind (dias, peso no sorteio, se é
/// fatal, e de que arma é) e a probabilidade das regras wound_chance/wound_chance_air/wound_chance_sea
/// e wound_loss_mult.</summary>
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
        w.Events.Subscribe<AirCombatEnded>(e => OnMissionCombat(w, World.Air, e.CountryId, e.RegionId, e.Worse));
        w.Events.Subscribe<SeaCombatEnded>(e => OnMissionCombat(w, World.Sea, e.CountryId, e.RegionId, e.Worse));
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
                c.GeneralWoundKind.Remove(gen);
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

    /// <summary>Combate no céu de uma região ou no mar de uma costa: o estado-maior daquela arma andava lá
    /// dentro. Ao contrário do exército, não há grupo destacado nem interino a quem entregar nada — o
    /// comandante de asa é do estado-maior, e o que ele dava ao país deixa de contar enquanto está fora.
    ///
    /// Cada comandante da arma arrisca-se uma vez por combate; quem levou a pior parte do dia arrisca
    /// wound_loss_mult vezes mais, como em terra é a retirada que mata comandantes.</summary>
    private static void OnMissionCombat(World w, string domain, int countryId, int regionId, bool worse)
    {
        if (w.WoundKinds.Count == 0 || !w.Countries.TryGetValue(countryId, out var c)) return;
        float chance = w.Rule(World.WoundChanceRule(domain), 0.012f);
        if (chance <= 0f) return;
        if (worse) chance *= w.Rule("wound_loss_mult", 2f);

        foreach (var gen in c.Generals.Where(id => w.DomainOfGeneral(id) == domain).ToList())
        {
            if (w.IsWounded(countryId, gen)) continue;                 // já está fora: não se fere duas vezes
            if (w.Rng.NextDouble() >= chance) continue;
            StrikeStaff(w, c, gen, regionId);
        }
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

    /// <summary>A baixa de um comandante destacado: cai, e o exército dele passa a quem estiver livre.</summary>
    public static void Strike(World w, Country c, ArmyGroup g, string generalId, int regionId)
    {
        if (!Hit(w, c, generalId, regionId)) return;
        g.GeneralId = StandIn(w, c, generalId);
        w.Events.Publish(new CommandHandedOver(c.Id, g.Id, g.GeneralId));
        World.ApplyGenerals(w, c);
    }

    /// <summary>A baixa de um comandante do estado-maior — de asa, de esquadra, ou um homem de terra sem
    /// exército entregue. Não há comando para passar a ninguém: o que ele dava ao país sai com ele e volta
    /// quando ele voltar (ou nunca mais, se ficou lá).</summary>
    public static bool StrikeStaff(World w, Country c, string generalId, int regionId)
    {
        if (!Hit(w, c, generalId, regionId)) return false;
        World.ApplyGenerals(w, c);
        return true;
    }

    /// <summary>A baixa em si: sorteia a gravidade pelos pesos da tabela da ARMA dele e tira o homem de
    /// serviço — ou de vez. Devolve false quando não há gravidade nenhuma para sortear.</summary>
    private static bool Hit(World w, Country c, string generalId, int regionId)
    {
        var kind = Draw(w, w.DomainOfGeneral(generalId));
        if (kind is null) return false;

        if (kind.Fatal)
        {
            c.Generals.Remove(generalId);
            c.GeneralXp.Remove(generalId);
            c.GeneralWound.Remove(generalId);
            c.GeneralWoundKind.Remove(generalId);
            w.Events.Publish(new GeneralKilled(c.Id, generalId, regionId));
        }
        else
        {
            c.GeneralWound[generalId] = w.Clock.Day + Math.Max(1, kind.Days);
            c.GeneralWoundKind[generalId] = kind.Id;
            w.Events.Publish(new GeneralWounded(c.Id, generalId, kind.Id, Math.Max(1, kind.Days)));
        }
        return true;
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

    /// <summary>Gravidade sorteada pelos pesos da tabela: muitos arranhões, poucos caixões. Só entram as
    /// que servem esta arma (World.WoundIsFor) — o pára-quedas é do ar e a água é do mar.</summary>
    private static WoundKind? Draw(World w, string domain)
    {
        var kinds = w.WoundKinds.Values.Where(k => World.WoundIsFor(k, domain))
                                       .OrderBy(k => k.Fatal).ThenBy(k => k.Days).ThenBy(k => k.Id).ToList();
        float total = kinds.Sum(k => MathF.Max(0f, k.Weight));
        if (total <= 0f) return null;
        double roll = w.Rng.NextDouble() * total;
        foreach (var k in kinds)
        {
            roll -= MathF.Max(0f, k.Weight);
            if (roll <= 0d) return k;
        }
        return kinds[^1];
    }
}
