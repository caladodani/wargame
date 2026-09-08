using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>Governos no exílio (HoI4: governments in exile). Até aqui um país que capitulava saía do mundo:
/// perdia a terra, o exército dissolvia-se, e daí em diante era uma cor no mapa e mais nada. Não é o que a
/// guerra ensina — a Polónia, a Noruega, a Holanda e a Grécia caíram todas e nenhuma desapareceu: o governo
/// embarcou para casa de um aliado e continuou a existir em papel, à espera do dia de voltar.
///
/// O que um governo no exílio tem é legitimidade (0..1). Chega com exile_legitimacy_start, sobe
/// exile_legitimacy_per_day enquanto quem o acolhe se bate contra quem lhe ocupa a capital, e desce
/// exile_legitimacy_decay nos dias em que essa guerra não existe — um governo de que ninguém se lembra
/// apaga-se sozinho. A zero acaba-se, e o país fica capitulado de vez.
///
/// Volta quando a capital é libertada por mão amiga (quem lá manda é o anfitrião ou aliado de facção dele) e
/// a legitimidade chegou a exile_return_legitimacy. Nesse dia quem libertou devolve-lhe as regiões dele que
/// tem na mão, e o governo traz consigo um exército de exílio proporcional à legitimidade com que voltou
/// (exile_return_divisions) — os que se juntaram a ele lá fora. Volta em paz: as guerras dele acabaram no
/// dia em que caiu e não se reabrem por si.
///
/// Não decide nada sozinho: o anfitrião é um aliado de facção (é a facção que dá o direito de asilo, como no
/// HoI4), e todos os números vêm de linhas da tabela rule.</summary>
public sealed class ExileSystem : ISystem
{
    public string Name => "Exile";
    private World? _bound;

    public void Tick(World w)
    {
        if (!ReferenceEquals(_bound, w)) Bind(w);

        float up = w.Rule("exile_legitimacy_per_day", 0.01f);
        float down = w.Rule("exile_legitimacy_decay", 0.005f);
        float need = w.Rule("exile_return_legitimacy", 0.6f);

        foreach (var c in w.Countries.Values.OrderBy(x => x.Id).ToList())
        {
            if (!c.InExile) continue;
            if (c.ExileDay == w.Clock.Day) continue;          // embarcou hoje: o primeiro dia não conta
            if (!Keep(w, c)) continue;                        // anfitrião caiu: muda de casa ou acaba

            int? foe = Occupier(w, c);
            bool fighting = foe is int f && w.AreAtWar(c.ExileHostId!.Value, f);
            c.ExileLegitimacy = Math.Clamp(c.ExileLegitimacy + (fighting ? up : -down), 0f, 1f);

            if (c.ExileLegitimacy <= 0f) { End(w, c); continue; }
            if (c.ExileLegitimacy >= need && Liberator(w, c) is int lib) Restore(w, c, lib);
        }
    }

    /// <summary>Liga-se ao barramento. Idempotente por mundo (padrão do PrisonerSystem). Só apanha
    /// capitulações daqui para a frente: um país já capitulado num save antigo não vai para o exílio de
    /// repente — o governo dele já se tinha desfeito quando ninguém guardava esta memória.</summary>
    public void Bind(World w)
    {
        _bound = w;
        w.Events.Subscribe<CountryCapitulated>(e => Form(w, e.CountryId));
    }

    /// <summary>O governo embarca. Sem aliado de facção de pé não há para onde ir: o país capitula e
    /// acabou-se ali.</summary>
    private static void Form(World w, int countryId)
    {
        if (!w.Countries.TryGetValue(countryId, out var c) || c.ExileHostId is not null) return;
        if (Host(w, c) is not int host) return;

        c.ExileHostId = host;
        c.ExileDay = w.Clock.Day;
        c.ExileLegitimacy = Math.Clamp(w.Rule("exile_legitimacy_start", 0.2f), 0f, 1f);
        w.Events.Publish(new GovernmentExiled(c.Id, host));
    }

    /// <summary>Aliado de facção que ainda está de pé e pode acolher: primeiro quem se bate contra quem
    /// ocupa a capital (é a casa que faz sentido — a guerra dele é a nossa), depois o maior exército, e o
    /// id mais baixo a desempatar para o mundo não depender de sementes.</summary>
    public static int? Host(World w, Country c)
    {
        int? foe = Occupier(w, c);
        Country? best = null;
        int bestFight = -1, bestArmy = -1;
        foreach (var h in w.Countries.Values.OrderBy(x => x.Id))
        {
            if (h.Id == c.Id || h.Capitulated || !w.SameFaction(c.Id, h.Id)) continue;
            int fight = foe is int f && w.AreAtWar(h.Id, f) ? 1 : 0;
            int army = w.Divisions.Values.Count(d => d.CountryId == h.Id);
            if (best is null || fight > bestFight || (fight == bestFight && army > bestArmy))
                { best = h; bestFight = fight; bestArmy = army; }
        }
        return best?.Id;
    }

    /// <summary>Quem manda hoje na capital dele, ou null se a capital não existe. É este o país contra quem
    /// a legitimidade se ganha e é dele que ela tem de ser tirada.</summary>
    public static int? Occupier(World w, Country c) =>
        w.Regions.TryGetValue(c.CapitalRegionId, out var r) && r.ControllerId != c.Id ? r.ControllerId : null;

    /// <summary>Mantém o anfitrião: se caiu, procura-se outra casa; sem casa nenhuma, o exílio acaba.
    /// false = este governo já não está em exílio e não se mexe mais neste dia.</summary>
    private static bool Keep(World w, Country c)
    {
        var host = w.Countries.GetValueOrDefault(c.ExileHostId!.Value);
        if (host is not null && !host.Capitulated) return true;

        int old = c.ExileHostId!.Value;
        if (Host(w, c) is not int next) { End(w, c); return false; }
        c.ExileHostId = next;
        w.Events.Publish(new ExileMoved(c.Id, old, next));
        return true;
    }

    /// <summary>Quem libertou a capital, se foi mão amiga: o anfitrião ou aliado de facção dele. null = a
    /// capital continua em mãos que não devolvem nada.</summary>
    private static int? Liberator(World w, Country c)
    {
        if (Occupier(w, c) is not int now) return null;
        int host = c.ExileHostId!.Value;
        return now == host || w.SameFaction(host, now) ? now : null;
    }

    /// <summary>Acabou-se sem regresso. O país fica capitulado; o que se apaga é o governo.</summary>
    private static void End(World w, Country c)
    {
        c.ExileHostId = null;
        c.ExileDay = null;
        c.ExileLegitimacy = 0f;
        w.Events.Publish(new ExileEnded(c.Id));
    }

    /// <summary>O regresso: quem libertou devolve as regiões de origem dele que a facção tem na mão (as que
    /// ainda estão com o inimigo continuam com ele — libertar é outro dia de guerra), o país deixa de estar
    /// capitulado e recebe o exército de exílio na capital.</summary>
    private static void Restore(World w, Country c, int liberator)
    {
        int regions = 0;
        foreach (var r in w.Regions.Values)
        {
            if (r.InitialOwnerId != c.Id) continue;
            if (r.ControllerId != liberator && !w.SameFaction(liberator, r.ControllerId)) continue;
            r.OwnerId = c.Id; r.ControllerId = c.Id;
            r.Resistance = 0f; r.Integration = 0f;         // em casa não se ocupa ninguém
            regions++;
        }

        float legit = c.ExileLegitimacy;
        c.Capitulated = false;
        c.CapitulatedDay = null;
        c.ExileHostId = null;
        c.ExileDay = null;
        c.ExileLegitimacy = 0f;

        int divisions = Raise(w, c, legit);
        w.Events.Publish(new GovernmentReturned(c.Id, liberator, regions, divisions));
    }

    /// <summary>O exército que voltou com o governo: exile_return_divisions à legitimidade cheia, à conta
    /// dela, e pelo menos uma — um governo que volta sem uma guarda não voltou. Nascem na capital (ou na
    /// melhor região que lhe devolveram) com o modelo mais barato que o país sabe fazer, e não custam homens
    /// nenhuns ao país: estes homens já andavam fora com ele.</summary>
    private static int Raise(World w, Country c, float legitimacy)
    {
        int want = Math.Max(1, (int)MathF.Round(w.Rule("exile_return_divisions", 4f) * legitimacy));
        int where = ProductionSystem.SpawnRegion(w, c);
        if (where < 0) return 0;

        var template = w.Units.GetTemplates(c.Id).OrderBy(t => w.TemplateCost(t.Id)).ThenBy(t => t.Id).FirstOrDefault();
        if (template is null) return 0;

        float org = w.Rule("new_division_org", 40f);
        for (int i = 0; i < want; i++)
            w.AddDivision(new Division
            {
                Id = w.NewDivisionId(), CountryId = c.Id, TemplateId = template.Id, RegionId = where,
                Org = org, Hp = 100f, Supply = 1f,
            });
        return want;
    }
}
