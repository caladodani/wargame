using System.Collections.Generic;
using System.Linq;
using WarGame.Core.Model;
using WarGame.Core.Systems;

namespace WarGame.Presentation;

/// <summary>As contas por trás dos mostradores da barra de cima.
///
/// É a assinatura do HoI4: nenhum número da barra é um número e mais nada — o dedo pousa em cima e ele
/// abre-se em parcelas. De onde vem o rendimento de hoje, quantas fábricas o território dá e quantas vêm
/// dos edifícios, quantos homens entram por dia e porque é que o bolso não cresce mais. Sem isto, um
/// jogador que vê "+48,3/dia" não tem por onde perceber o que fazer para ter mais: o número é um oráculo.
///
/// Tudo aqui é derivado — não guarda estado, não entra no save, e cada conta é a mesma que o sistema faz.
/// Onde a conta de um sistema é uma fórmula (as fábricas, o pool de homens), repete-se a fórmula pelas
/// regras da base de dados e não por números escritos à mão: uma regra que mude na BD muda a explicação
/// no mesmo dia em que muda o jogo.
///
/// Um varrimento só: as regiões visitam-se uma vez em <see cref="Scan"/> e todos os textos vivem do que
/// ele trouxe. A barra refresca-se a cada dia e o mundo tem 2988 regiões — três varrimentos por dia para
/// escrever três tooltips seria pagar caro por uma coisa que ninguém está a ler.</summary>
public static class Breakdown
{
    /// <summary>O que um varrimento às regiões controladas traz: terra nossa e terra ocupada em separado
    /// (rendem de maneira diferente e recrutam de maneira diferente), os níveis de edifício que abrem
    /// fábricas, e quantas obras estão a ocupar uma fábrica civil agora.</summary>
    public readonly record struct Parts(int OwnRegions, float OwnIncome, float OwnPop,
                                        int HeldRegions, float HeldIncome, float HeldPop,
                                        Dictionary<string, int> Yards, int Sites)
    {
        public int Regions => OwnRegions + HeldRegions;
        public float Income => OwnIncome + HeldIncome;
        public float Pop => OwnPop + HeldPop;
    }

    /// <summary>Uma passagem pelas regiões do país. A população da terra ocupada entra pela fatia que a
    /// política de ocupação daquele povo deixa recrutar — é a mesma conta do ManpowerSystem.</summary>
    public static Parts Scan(World w, int pid)
    {
        int own = 0, held = 0, sites = 0;
        float ownInc = 0f, heldInc = 0f, ownPop = 0f, heldPop = 0f;
        var yards = new Dictionary<string, int>();
        foreach (var r in w.Regions.Values)
        {
            if (r.ControllerId != pid) continue;
            float pop = r.Population * OccupationSystem.ManpowerMult(w, r);
            float inc = EconomySystem.RegionIncome(w, r);
            if (r.OwnerId == pid) { own++; ownInc += inc; ownPop += pop; }
            else { held++; heldInc += inc; heldPop += pop; }
            foreach (var (bid, lvl) in r.Buildings)
                if (lvl > 0 && w.BuildingDefs.TryGetValue(bid, out var def) && def.Yard.Length > 0)
                    yards[def.Yard] = yards.GetValueOrDefault(def.Yard) + lvl;
            if (r.OwnerId == pid && Industry.Working(r)) sites++;
        }
        return new Parts(own, ownInc, ownPop, held, heldInc, heldPop, yards, sites);
    }

    /// <summary>O cofre: quanto rende a terra nossa, quanto rende a terra tomada, e os dois multiplicadores
    /// de país que já lá estão dentro. É aqui que se vê que ocupar mal rende pouco.</summary>
    public static string Money(Country p, in Parts s)
    {
        string t = $"Cofre: {p.Money:0.0} pontos.\nDe onde vem o rendimento de hoje:";
        t += $"\n· terra nossa ({Reg(s.OwnRegions)}): {Sign(s.OwnIncome)}/dia";
        if (s.HeldRegions > 0) t += $"\n· terra ocupada ({Reg(s.HeldRegions)}): {Sign(s.HeldIncome)}/dia";
        t += $"\n· soma: {Sign(s.Income)}/dia";
        t += $"\nJá contados: indústria ×{p.Stat("industry"):0.00}, estabilidade ×{p.StabilityFactor:0.00}.";
        return t;
    }

    /// <summary>O poder político: quanto se tem, o que entra por dia e de onde vem cada parcela — e o que
    /// isto dá para comprar hoje. Um número político sem preços ao lado não diz nada a ninguém: o que o
    /// jogador quer saber ao pousar o dedo é se já chega para a lei que anda a namorar.</summary>
    public static string Political(World w, Country p)
    {
        string t = $"Poder político: {p.Political:0.0} de um tecto de {w.Rule("political_max", 1500f):0}.";
        t += "\nÉ a moeda da política — leis, gabinete, decisões, pactos e pretextos de guerra. Nunca se troca por aço.";
        t += "\nDe onde vem o dia de hoje:";
        foreach (var part in PoliticsSystem.Parts(w, p.Id))
            t += $"\n· {part.Label}: {Sign(part.Points)}/dia";
        t += $"\n· soma: {Sign(PoliticsSystem.Gain(w, p.Id))}/dia";
        t += $"\nPreços: mudar de lei {w.Rule("law_change_cost", 30f):0}"
           + $", justificar guerra {w.Rule("justify_cost", 25f):0}"
           + $", pacto de não-agressão {w.Rule("nap_cost", 20f):0}.";
        if (p.Political >= w.Rule("political_max", 1500f) - 1f) t += "\nEstá no tecto: o que entra hoje perde-se.";
        return t;
    }

    /// <summary>A tensão mundial: o termómetro do mundo e quem o está a aquecer, parcela a parcela, mais as
    /// portas que ela abre. É o número que explica por que é que uma coisa que ontem era impensável hoje
    /// passa em câmara — e sem as parcelas seria o oráculo mais opaco da barra.</summary>
    public static string Tension(World w)
    {
        float t = WorldTension.Of(w);
        var parts = WorldTension.Parts(w);
        string s = $"Tensão mundial: {t:0} de 100 — {WorldTension.Mood(w)}.";
        if (parts.Count == 0) s += "\nO mundo está quieto: ninguém em guerra, ninguém a cair, ninguém a arranjar pretexto.";
        else
        {
            s += "\nQuem a está a puxar:";
            foreach (var part in parts.Take(6)) s += $"\n· {part.Label}: +{part.Points:0.0}";
            if (parts.Count > 6) s += $"\n· e mais {parts.Count - 6} razões";
        }
        s += "\nO que ela abre:";
        s += Door(w, "voluntários para guerras alheias", "volunteer_min_tension", 15f, t);
        s += Door(w, "justificar guerra a quem está longe", "justify_far_tension", 25f, t);
        foreach (var law in w.Laws.Values.Where(l => l.MinTension > 0f).OrderBy(l => l.MinTension).ThenBy(l => l.Id).Take(4))
            s += $"\n· {law.Name}: {(t >= law.MinTension ? "aberto" : $"fechado (falta chegar a {law.MinTension:0})")}";
        return s;
    }

    private static string Door(World w, string what, string key, float fallback, float now)
    {
        float need = w.Rule(key, fallback);
        return $"\n· {what}: {(now >= need ? "aberto" : $"fechado (falta chegar a {need:0})")}";
    }

    /// <summary>Os homens: o que entra por dia, de que população vem e onde está o tecto. Um bolso cheio
    /// deita fora o que entra, e isso é o que ninguém percebia a olhar para o número parado.</summary>
    public static string Men(World w, Country p, in Parts s)
    {
        float conscription = p.Stat("conscription");
        float cap = s.Pop * w.Rule("manpower_cap_share", 0.05f) * conscription;
        float daily = s.Pop / 1e6f * w.Rule("manpower_per_million_daily", 60f) * conscription * p.StabilityFactor;
        string t = $"Homens: {Men(p.Manpower)} por chamar, de um tecto de {Men(cap)}.";
        t += $"\n· gente nossa: {Men(s.OwnPop)}";
        if (s.HeldRegions > 0) t += $"\n· gente ocupada que se deixa recrutar: {Men(s.HeldPop)}";
        t += $"\n· entram +{Men(daily)}/dia (recrutamento ×{conscription:0.00}, estabilidade ×{p.StabilityFactor:0.00})";
        if (p.Manpower >= cap - 1f) t += "\nO bolso está no tecto: o que entra hoje perde-se.";
        return t;
    }

    /// <summary>As divisões: quantas se batem, quantas marcham, quantas estão paradas e quantas vêm a
    /// caminho. A fila separa o que ainda gasta fábrica do que já está pronto à espera de recrutas — são
    /// duas esperas diferentes e a barra dizia-lhes o mesmo nome.</summary>
    public static string Divisions(World w, Country p)
    {
        int total = 0, fighting = 0, moving = 0, cut = 0;
        foreach (var d in w.Divisions.Values)
        {
            if (d.CountryId != p.Id) continue;
            total++;
            if (w.InBattle(d.Id)) fighting++;
            else if (d.Path.Count > 0) moving++;
            if (d.Cut) cut++;
        }
        string t = $"Divisões: {total} no terreno.";
        t += $"\n· em combate: {fighting} · a marchar: {moving} · paradas: {total - fighting - moving}";
        if (cut > 0) t += $"\n· cortadas do abastecimento: {cut}";
        if (p.Queue.Count > 0)
        {
            int unfinished = Industry.Unfinished(w, p);
            t += $"\n· na fila: {p.Queue.Count} — {unfinished} em construção";
            if (p.Queue.Count > unfinished) t += $", {p.Queue.Count - unfinished} à espera de recrutas";
        }
        return t;
    }

    /// <summary>Uma das três filas de fábricas, aberta nas parcelas de que nasce: a base que todo o país
    /// tem, o que o tamanho do território dá e o que os edifícios acrescentam. É a conta do Industry.Of,
    /// pelas mesmas regras da base de dados.</summary>
    public static string Factories(World w, in Parts s, string yard, int busy, int have, string title, string doing)
    {
        float bas = yard switch { "civil" => w.Rule("factory_civil_base", 2f),
                                  "militar" => w.Rule("factory_mil_base", 2f), _ => 0f };
        float perRegion = yard switch { "civil" => w.Rule("factory_civil_per_region", 0.25f),
                                        "militar" => w.Rule("factory_mil_per_region", 0.15f), _ => 0f };
        int levels = s.Yards.GetValueOrDefault(yard);
        float perBuilding = w.Rule("factory_per_building", 1f);

        string t = $"{title}: {busy} de {have} {doing}.";
        if (bas > 0f) t += $"\n· base do país: {bas:0.#}";
        if (perRegion > 0f) t += $"\n· território: {Reg(s.Regions)} × {perRegion:0.##} = {s.Regions * perRegion:0.#}";
        t += $"\n· edifícios: {levels} {(levels == 1 ? "nível" : "níveis")} × {perBuilding:0.##} = {levels * perBuilding:0.#}";
        int free = have - busy;
        t += free > 0 ? $"\n{free} {(free == 1 ? "está livre" : "estão livres")}." : "\nNenhuma está livre.";
        return t;
    }

    /// <summary>Uma das três medalhas: o bolso, o tecto, o que se aprende a seguir e de onde é que a
    /// experiência daquela arma vem — cada arma aprende no sítio dela e nenhuma aprende pelas outras.</summary>
    public static string Medal(World w, Country p, string domain)
    {
        float have = World.Xp(p, domain);
        float max = domain switch { World.Air => w.Rule("air_xp_max", 400f),
                                    World.Sea => w.Rule("navy_xp_max", 400f), _ => w.Rule("army_xp_max", 600f) };
        string arm = domain switch { World.Air => "do ar", World.Sea => "do mar", _ => "do exército" };
        string t = $"Experiência {arm}: {have:0.0} no bolso, de um tecto de {max:0}.";

        if (ArmyXpSystem.Next(w, p, domain) is string id && w.ArmyDoctrines.TryGetValue(id, out var d))
            t += $"\n· já dá para: {d.Name} (custa {d.Cost:0})";
        else
        {
            var next = w.ArmyDoctrines.Values.Where(x => w.DomainOf(x) == domain && !p.Doctrines.Contains(x.Id))
                                             .OrderBy(x => x.Cost).ThenBy(x => x.Id).FirstOrDefault();
            t += next is null ? "\n· não há mais escolas por aprender nesta arma."
                              : $"\n· a seguir: {next.Name} (custa {next.Cost:0}) — faltam {next.Cost - have:0.0}";
        }
        t += $"\n· escolas já aprendidas nesta arma: {p.Doctrines.Count(x => w.ArmyDoctrines.TryGetValue(x, out var y) && w.DomainOf(y) == domain)}";

        if (domain == World.Land)
        {
            int inBattle = w.ActiveBattles.SelectMany(b => b.Attackers.Concat(b.Defenders))
                            .Count(x => w.Divisions.TryGetValue(x, out var dv) && dv.CountryId == p.Id);
            float gain = inBattle * w.Rule("army_xp_per_battle_day", 0.4f)
                       + (w.Divisions.Values.Any(dv => dv.CountryId == p.Id) ? w.Rule("army_xp_per_day", 0.1f) : 0f);
            t += $"\n· hoje entra +{gain:0.0}: {inBattle} em combate × {w.Rule("army_xp_per_battle_day", 0.4f):0.##}, mais o treino de quem tem tropa no terreno";
        }
        else t += domain == World.Air ? "\n· ganha-se a voar: cada missão de ar e cada avião perdido ensinam."
                                      : "\n· ganha-se a navegar: cada missão do mar e cada navio perdido ensinam.";
        return t;
    }

    private static string Reg(int n) => n == 1 ? "1 região" : $"{n} regiões";
    private static string Sign(float v) => $"{(v < 0f ? "" : "+")}{v:0.0}";
    private static string Men(float m) =>
        m >= 1e6f ? $"{m / 1e6f:0.0}M" : m >= 1e3f ? $"{m / 1e3f:0.0}k" : $"{m:0}";
}
