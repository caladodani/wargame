using WarGame.Core.Model;

namespace WarGame.Core.Systems;

/// <summary>AS CAMPANHAS DIPLOMÁTICAS: a diplomacia que se FAZ, e não só a que se tem.
///
/// Desde 0.3.83 que cada par de países tem uma opinião (<see cref="Relations"/>) — mas ela era só um
/// espelho: media a guerra, a fronteira, o material, e o jogador não tinha um único botão para a mexer.
/// No HoI4 tem: abre-se uma embaixada e a opinião sobe todos os dias, garante-se a independência de um
/// vizinho pequeno, paga-se propaganda dentro do país alheio. É poder político a comprar amizade.
///
/// Uma campanha custa `cost_start` à assinatura e `cost_day` todos os dias, e rende `magnitude` por dia
/// até ao tecto `cap`. Enquanto se paga, acumula; no dia em que se pára — por ordem do jogador, por falta
/// de poder político, por guerra entre os dois ou por um deles cair — a campanha fica parada e desfaz-se
/// ao ritmo de `diplo_decay_day` até desaparecer. Quem deixa a embaixada fechar perde o que ela ganhou.
///
/// O que cada campanha faz vem da coluna `effect` da tabela `diplo_action`, e o C# só sabe medir três
/// coisas: `opiniao` e `garantia` entregam pontos que o <see cref="Relations"/> lê como mais uma razão
/// (linhas `esforco` e `garantia` da tabela `opinion_source`), e `partido` empurra, dentro do país alvo,
/// a popularidade do partido mais parecido connosco no eixo político. Campanha nova = uma linha de SQL,
/// desde que caia num destes três feitios.
///
/// A garantia tem ainda dentes: <see cref="Deterred"/> diz à IA para não escolher como alvo um país que
/// alguém bastante maior do que ela prometeu defender.</summary>
public sealed class DiploDriveSystem : ISystem
{
    public string Name => "Diplomacy";

    public void Tick(World w)
    {
        List<DiploDrive>? dead = null;
        foreach (var d in w.DiploDrives)
        {
            w.DiploActions.TryGetValue(d.ActionId, out var def);
            w.Countries.TryGetValue(d.FromId, out var from);
            w.Countries.TryGetValue(d.ToId, out var to);
            bool alive = def is not null && from is not null && to is not null
                      && !from.Capitulated && !to.Capitulated && !w.AreAtWar(d.FromId, d.ToId);
            // e tem de haver com que pagar o dia: uma embaixada sem verba fecha sozinha
            if (alive && from!.Political < def!.CostDay) alive = false;
            if (!alive) d.Active = false;

            if (d.Active)
            {
                from!.Political -= def!.CostDay;
                d.Progress = MathF.Min(def.Cap, d.Progress + def.Magnitude);
                if (def.Effect == "partido") Boost(w, from, to!, def);
            }
            else
            {
                d.Progress -= MathF.Max(0.01f, w.Rule("diplo_decay_day", 0.4f));
                if (d.Progress <= 0f) (dead ??= new()).Add(d);
            }
        }
        if (dead is not null) foreach (var d in dead) w.DiploDrives.Remove(d);
        Ai(w);
    }

    /// <summary>A campanha aberta de `from` sobre `to` com esta acção, ou null.</summary>
    public static DiploDrive? Find(World w, int fromId, int toId, string actionId) =>
        w.DiploDrives.FirstOrDefault(d => d.FromId == fromId && d.ToId == toId && d.ActionId == actionId);

    /// <summary>Quantas campanhas este país tem em pé (as paradas já não custam nada e não contam).</summary>
    public static int Open(World w, int countryId) => w.DiploDrives.Count(d => d.FromId == countryId && d.Active);

    /// <summary>O que a campanha `effect` de `to` sobre `from` já acumulou — é isto que a opinião lê.</summary>
    public static float Earned(World w, int fromId, int toId, string effect)
    {
        float sum = 0f;
        foreach (var d in w.DiploDrives)
            if (d.FromId == fromId && d.ToId == toId && d.Progress > 0f
             && w.DiploActions.TryGetValue(d.ActionId, out var def) && def.Effect == effect) sum += d.Progress;
        return sum;
    }

    /// <summary>Alguém está a mexer na política de `countryId` a partir de fora? (campanha hostil activa)</summary>
    public static bool Meddling(World w, int fromId, int toId) =>
        w.DiploDrives.Any(d => d.FromId == fromId && d.ToId == toId && d.Active
                            && w.DiploActions.TryGetValue(d.ActionId, out var def) && def.Hostile);

    /// <summary>Atacar `targetId` traz-nos alguém grande em cima? Verdadeiro quando um garante do alvo tem
    /// pelo menos `diplo_guarantee_ratio` vezes as nossas divisões — é o dente da garantia, e é por isto
    /// que um país pequeno com um padrinho grande deixa de ser presa fácil.</summary>
    public static bool Deterred(World w, int aggressorId, int targetId)
    {
        int mine = w.Divisions.Values.Count(d => d.CountryId == aggressorId);
        float need = MathF.Max(1f, w.Rule("diplo_guarantee_ratio", 1.2f)) * MathF.Max(1, mine);
        foreach (var d in w.DiploDrives)
        {
            if (d.ToId != targetId || !d.Active || d.FromId == aggressorId) continue;
            if (!w.DiploActions.TryGetValue(d.ActionId, out var def) || def.Effect != "garantia") continue;
            if (w.Divisions.Values.Count(x => x.CountryId == d.FromId) >= need) return true;
        }
        return false;
    }

    /// <summary>A propaganda paga: dentro do país alvo sobe o partido cujo eixo é mais parecido com o do
    /// nosso governo, tirado proporcionalmente aos outros (a opinião continua a somar 100). Sem governo
    /// com linha na tabela dos partidos não há de quem fazer propaganda.</summary>
    private static void Boost(World w, Country from, Country to, DiploActionDef def)
    {
        if (Relations.Axis(w, from) is not float ours) return;
        string? best = null; float near = float.MaxValue;
        foreach (var p in w.PartyDefs.Values)
        {
            float gap = MathF.Abs(p.Axis - ours);
            if (best is null || gap < near || (gap == near && string.CompareOrdinal(p.Id, best) < 0))
            { near = gap; best = p.Id; }
        }
        if (best is null) return;
        to.Parties[best] = to.Parties.GetValueOrDefault(best) + def.Magnitude;
        World.NormalizeParties(to);
    }

    /// <summary>A IA também tem embaixadas: de semana a semana, um país com poder político de sobra abre
    /// uma campanha de melhorar relações sobre o vizinho de quem já gosta mais — que é como as amizades do
    /// mundo se formam sozinhas em vez de dependerem sempre do jogador.</summary>
    private static void Ai(World w)
    {
        if (w.Clock.Day <= 0 || w.Clock.Day % 7 != 0) return;   // de semana a semana, e nunca no dia de arranque
        string action = w.DiploActions.Values.FirstOrDefault(a => a.Effect == "opiniao")?.Id ?? "";
        if (action.Length == 0) return;
        var def = w.DiploActions[action];
        int max = Math.Max(1, (int)w.Rule("diplo_max_drives", 6f));
        foreach (var c in w.Countries.Values.ToList())
        {
            if (c.Capitulated || c.IsPlayer || c.Political < def.CostStart * 4f) continue;
            if (Open(w, c.Id) >= Math.Min(2, max)) continue;
            int best = 0; float bestOp = float.MinValue;
            foreach (var o in w.Countries.Values)
            {
                if (o.Id == c.Id || o.Capitulated || w.AreAtWar(c.Id, o.Id)) continue;
                if (!w.SharesBorder(c.Id, o.Id) || Find(w, c.Id, o.Id, action) is not null) continue;
                float op = Relations.Opinion(w, o.Id, c.Id);
                if (op > bestOp || (op == bestOp && o.Id < best)) { bestOp = op; best = o.Id; }
            }
            if (best == 0) continue;
            c.Political -= def.CostStart;
            w.DiploDrives.Add(new DiploDrive { FromId = c.Id, ToId = best, ActionId = action, SinceDay = w.Clock.Day });
        }
    }
}
