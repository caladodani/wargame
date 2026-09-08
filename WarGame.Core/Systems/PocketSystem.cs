using WarGame.Core.Events;
using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>O cerco, como no HoI4: uma bolsa não é só tropa a comer pouco — é tropa condenada. Até aqui
/// cortar a retaguarda ao inimigo dava-lhe supply_pocket e mais nada: as divisões cercadas ficavam ali,
/// a bater com metade da força, para sempre. Fechar um cerco — a manobra que decide as campanhas — não
/// tinha prémio nenhum, e o jogador que fosse cercado não perdia nada por ignorar o aviso.
///
/// Agora o cerco cobra por dias. Uma divisão cortada (Division.Cut, escrito pelo SupplySystem no mesmo
/// sítio onde decide a fome, para cortado querer dizer o mesmo nos dois lados) tem pocket_grace dias de
/// respiro — o tempo de romper para fora ou de alguém abrir o corredor. Passado isso perde
/// pocket_attrition de efectivo e pocket_org de organização por dia: as munições acabaram, os feridos não
/// saem, os camiões estão parados. E ao fim de pocket_surrender dias com a bolsa fechada, a divisão baixa
/// as armas: rende-se a quem fechou o anel, e esses homens vão para os campos dele (PrisonerSystem, pelo
/// evento DivisionSurrendered).
///
/// Quem decide se a bolsa está fechada é o Pockets, e a bolsa inteira decide de uma vez. Antes a pergunta
/// era por divisão — "tenho um vizinho amigo?" — e num caldeirão de várias regiões cada divisão via a terra
/// da do lado e achava que tinha por onde romper: um cerco grande, o do HoI4, nunca capitulava. Agora a
/// saída tem de ser para fora do caldeirão, e só terra de aliado de facção serve: dentro do anel já é tudo
/// bolsa. É a mesma bolsa que o mapa desenha, por isso o que se vê é o que se cobra.
///
/// Quem apanha o cerco é quem manda no anel: o país em guerra connosco que controla mais regiões à volta.
/// Sem anel nenhum — uma ilha sem porto, uma bolsa cujo cerco já se desfez — não há a quem entregar, e a
/// divisão continua a definhar em vez de se render ao vazio. Quando o último dos nossos sai da região, a
/// terra passa para quem cercou: ninguém precisa de assaltar uma praça que já se rendeu.
///
/// Regras: pocket_grace, pocket_attrition, pocket_org, pocket_surrender.</summary>
public sealed class PocketSystem : ISystem
{
    public string Name => "Pocket";

    public void Tick(World w)
    {
        if (w.Divisions.Count == 0) return;
        float grace = w.Rule("pocket_grace", 3f);
        float bleed = w.Rule("pocket_attrition", 4f), orgLoss = w.Rule("pocket_org", 8f);
        int surrender = (int)w.Rule("pocket_surrender", 21f);

        // os caldeirões de hoje, uma vez só: quem está em qual, se está fechado e a quem se entrega
        var pot = new Dictionary<int, Pocket>();
        foreach (var p in Pockets.All(w))
            foreach (int id in p.DivisionIds) pot[id] = p;

        // recolhe primeiro: render uma divisão muta w.Divisions e a região onde ela está
        List<(Division D, int? Captor)>? gone = null;
        foreach (var d in w.Divisions.Values)
        {
            if (!d.Cut) { d.PocketDays = 0; continue; }
            d.PocketDays++;
            if (d.PocketDays <= grace) continue;

            d.Hp = MathF.Max(0f, d.Hp - bleed);
            d.Org = MathF.Max(0f, d.Org - orgLoss);
            // O anel é o da bolsa toda: uma divisão no meio do caldeirão não tem inimigo à porta, mas
            // entrega-se a quem fechou o cerco na mesma. Sem anel — a guerra acabou, o cerco desfez-se ao
            // longe — não há a quem entregar e a bolsa definha em vez de render as armas ao vazio.
            var mine = pot.GetValueOrDefault(d.Id);
            int? captor = mine.RingCountryId ?? Ring(w, d);
            bool baixaAsArmas = surrender > 0 && d.PocketDays >= surrender && mine.Sealed && captor is not null;
            if (d.Hp <= 0f || baixaAsArmas) (gone ??= new()).Add((d, captor));
        }
        if (gone is null) return;
        foreach (var (d, captor) in gone) Fall(w, d, captor);
    }

    /// <summary>Quem fechou o anel: dos países em guerra com este, o que controla mais regiões à volta da
    /// bolsa (a própria região conta — uma divisão cercada em terreno já tomado rende-se a quem o tomou).
    /// Desempate pelo id, para o mesmo mundo dar sempre a mesma resposta.</summary>
    public static int? Ring(World w, Division d)
    {
        if (!w.Regions.TryGetValue(d.RegionId, out var here)) return null;
        var count = new Dictionary<int, int>();
        foreach (int n in here.Neighbours.Append(here.Id))
        {
            if (!w.Regions.TryGetValue(n, out var nb)) continue;
            int c = nb.ControllerId;
            if (c == d.CountryId || !w.AreAtWar(d.CountryId, c)) continue;
            count[c] = count.GetValueOrDefault(c) + 1;
        }
        if (count.Count == 0) return null;
        return count.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
    }

    /// <summary>A bolsa desfaz-se: prisioneiros para quem cercou (se houver anel), a divisão sai do mundo e,
    /// se era a última dos nossos ali, a região muda de mãos sem um tiro.</summary>
    private static void Fall(World w, Division d, int? ring)
    {
        int region = d.RegionId;
        if (ring is int captor)
        {
            // a rendição vem antes da baixa: assim os homens contam-se ao captor certo, e o
            // DivisionDestroyed que se segue não os apanha outra vez (a região ainda é do próprio país)
            w.Events.Publish(new DivisionSurrendered(d.Id, captor, d.CountryId, region));
            w.Events.Publish(new DivisionDestroyed(d.Id));
            w.RemoveDivision(d.Id);
            if (w.Regions.TryGetValue(region, out var r) && r.ControllerId == d.CountryId
                && !r.DivisionIds.Any(id => w.Divisions.TryGetValue(id, out var o) && w.CanTraverse(o.CountryId, r)))
            {
                int old = r.ControllerId;
                r.ControllerId = captor;
                w.Events.Publish(new RegionCaptured(region, old, captor));
            }
            return;
        }
        w.Events.Publish(new DivisionDestroyed(d.Id));
        w.RemoveDivision(d.Id);
    }

    /// <summary>Divisões de um país que estão em cerco a sério (já para lá do respiro). Estado derivado,
    /// para os avisos e para as fichas — não guarda nada.</summary>
    public static List<Division> Of(World w, int countryId)
    {
        float grace = w.Rule("pocket_grace", 3f);
        return w.Divisions.Values
            .Where(d => d.CountryId == countryId && d.Cut && d.PocketDays > grace)
            .OrderByDescending(d => d.PocketDays).ThenBy(d => d.Id).ToList();
    }

    /// <summary>Dias que faltam até esta divisão baixar as armas, ou null enquanto a bolsa tiver saída.</summary>
    public static int? DaysToSurrender(World w, Division d)
    {
        int surrender = (int)w.Rule("pocket_surrender", 21f);
        if (surrender <= 0 || !d.Cut) return null;
        var mine = Pockets.Of(w, d.CountryId).FirstOrDefault(p => p.DivisionIds.Contains(d.Id));
        if (!mine.Sealed || (mine.RingCountryId ?? Ring(w, d)) is null) return null;
        return Math.Max(0, surrender - d.PocketDays);
    }
}
