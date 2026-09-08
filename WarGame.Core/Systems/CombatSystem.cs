using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Systems;

/// <summary>Port directo de combat_sim.py. Só corre nas regiões em World.ActiveBattles.
///
/// Desde a largura de frente (Frontage), cada lado entra no dia com uma linha e uma reserva: só a linha bate e
/// só a linha apanha. A batalha continua enquanto houver alguém de pé — as reservas contam para isso.</summary>
/// <summary>Uma parcela do balanço de um lado: o nome que se lê e o multiplicador que ela vale. 1 é
/// neutro, abaixo de 1 tira força, acima de 1 dá.</summary>
public readonly record struct CombatFactor(string Name, float Mult);

/// <summary>O balanço de um lado num dia de batalha: a força média da linha e as parcelas de que ela
/// nasce, pela ordem em que se aplicam.</summary>
public sealed record CombatSide(float Strength, List<CombatFactor> Factors);

public sealed class CombatSystem : ISystem
{
    public string Name => "Combat";
    public float DamageScale { get; init; } = 0.45f;

    public void Tick(World w)
    {
        var dead = new HashSet<int>();
        for (int i = w.ActiveBattles.Count - 1; i >= 0; i--)
        {
            var b = w.ActiveBattles[i];
            var region = w.Regions[b.RegionId];
            var att = b.Attackers.Select(id => w.Divisions[id]).Where(d => d.CanFight).ToList();
            var def = b.Defenders.Select(id => w.Divisions[id]).Where(d => d.CanFight).ToList();

            var ctxA = BuildContext(w, region, b.AttackerCountryId);
            var ctxD = BuildContext(w, region, region.ControllerId);

            // Largura de frente: só a linha se bate; o resto fica em reserva e entra quando estes caírem.
            var (lineA, _) = Frontage.Split(w, region, att);
            var (lineD, _) = Frontage.Split(w, region, def);
            ResolveTick(w, lineA, lineD, ctxA, ctxD, 1f + region.Fort * w.Rule("fort_defense_per_level", 0.15f), region);
            b.Days++;
            foreach (var d in att.Concat(def)) if (d.Hp <= 0f) dead.Add(d.Id);

            bool defOut = !def.Any(d => d.CanFight);
            bool attOut = !att.Any(d => d.CanFight);
            if (defOut || attOut)
            {
                w.ActiveBattles.RemoveAt(i);
                w.Events.Publish(new BattleEnded(b.RegionId, defOut, b.AttackerCountryId, region.ControllerId));
                // Batalha travada até ao fim conta para as condecorações de quem lá ficou vivo.
                foreach (int id in b.Attackers.Concat(b.Defenders))
                    if (w.Divisions.TryGetValue(id, out var vet) && vet.Hp > 0f) vet.Battles++;
                if (defOut)
                {
                    foreach (int id in b.Attackers)
                        if (w.Divisions.TryGetValue(id, out var win) && win.Hp > 0f) win.Captures++;
                    int old = region.ControllerId;
                    region.ControllerId = b.AttackerCountryId;
                    CaptureDamage(w, region);
                    w.NoteWarProgress(old, region.ControllerId);
                    w.Events.Publish(new RegionCaptured(region.Id, old, region.ControllerId));
                }
            }
        }
        // Divisões destruídas saem do mundo aqui (o evento já foi publicado em ResolveTick).
        foreach (var id in dead) w.RemoveDivision(id);
    }

    /// <summary>Captura danifica a infraestrutura (capture_infra_hit, chão infra_min) e mata a obra em curso.</summary>
    public static void CaptureDamage(World w, Region r)
    {
        r.Infrastructure = MathF.Max(w.Rule("infra_min", 0.3f), r.Infrastructure - w.Rule("capture_infra_hit", 0.15f));
        r.Building = false; r.BuildProgress = 0f;
        r.Fort = Math.Max(0, r.Fort - 1);
        r.FortBuilding = false; r.FortProgress = 0f;
    }

    /// <summary>O contexto do lado neste chão: terreno, rio, bandeira, tecnologias e a seca de combustível.
    /// É o que a tabela modifier lê. Público porque o ecrã de batalha explica o dia com o mesmo contexto com
    /// que o dia se bateu — dois contextos diferentes seriam duas verdades.</summary>
    public static ModContext BuildContext(World w, Region r, int countryId)
    {
        var ctx = new ModContext().With("terrain", r.Terrain).With("country", w.Countries[countryId].Tag);
        if (r.River) ctx["river"] = "true";
        // o céu de hoje aqui entra como o chão e o rio: quem assalta debaixo de um nevão paga-o na tabela
        if (Weather.Id(w, r) is { Length: > 0 } sky) ctx["weather"] = sky;
        // Outros sistemas (Air, Cyber, Research) escrevem aqui via flags na região/país — ver AirSystem.
        foreach (var tech in w.Countries[countryId].Techs) ctx[$"tech:{tech}"] = "true";
        // A seca de combustível entra aqui e mais nada: o que ela custa está na tabela modifier, e hoje
        // custa metade da força aos blindados. Um exército a pé não dá por nada — e é esse o ponto.
        if (w.Countries[countryId].FuelOut) ctx["fuel_out"] = "true";
        return ctx;
    }

    public void ResolveTick(World w, List<Division> att, List<Division> def, ModContext ctxA, ModContext ctxD, float fortMult = 1f, Region? battleRegion = null)
    {
        var strA = SideStrength(w, att, ctxA, attacking: true, battleRegion);
        var strD = SideStrength(w, def, ctxD, attacking: false);
        if (fortMult != 1f) for (int i = 0; i < strD.Length; i++) strD[i] *= fortMult;
        // intel (rede_info): quem tem intel sobre o país do outro lado bate mais forte
        float intelMult = w.Rule("intel_combat_bonus", 1.05f);
        if (att.Count > 0 && def.Count > 0)
        {
            int attC = att[0].CountryId, defC = def[0].CountryId;
            if (w.HasIntel(attC, defC)) for (int i = 0; i < strA.Length; i++) strA[i] *= intelMult;
            if (w.HasIntel(defC, attC)) for (int i = 0; i < strD.Length; i++) strD[i] *= intelMult;
        }
        // superioridade aérea: razão de esquadrões modula a força (±air_combat_weight no máximo). O poder
        // aéreo nacional conta sempre, mas as asas destacadas para o céu desta região contam por cima —
        // é o que faz valer a pena concentrar a aviação numa frente em vez de a espalhar pelo mundo.
        if (att.Count > 0 && def.Count > 0
            && w.Countries.TryGetValue(att[0].CountryId, out var ac) && w.Countries.TryGetValue(def[0].CountryId, out var dc2))
        {
            float airA = ac.AirPower, airD = dc2.AirPower;
            if (battleRegion is not null)
            {
                airA += AirMissionSystem.Superiority(w, battleRegion.Id, ac.Id);
                airD += AirMissionSystem.Superiority(w, battleRegion.Id, dc2.Id);
            }
            float airTot = airA + airD;
            if (airTot > 0f)
            {
                float weight = w.Rule("air_combat_weight", 0.15f);
                float mA = 1f + (airA / airTot - 0.5f) * 2f * weight;
                float mD = 1f + (airD / airTot - 0.5f) * 2f * weight;
                for (int i = 0; i < strA.Length; i++) strA[i] *= mA;
                for (int i = 0; i < strD.Length; i++) strD[i] *= mD;
            }
            // apoio próximo: as asas que batem no chão somam força a quem ali combate (tecto air_support_max)
            if (battleRegion is not null)
            {
                float supA = 1f + AirMissionSystem.Support(w, battleRegion.Id, ac.Id);
                float supD = 1f + AirMissionSystem.Support(w, battleRegion.Id, dc2.Id);
                for (int i = 0; i < strA.Length; i++) strA[i] *= supA;
                for (int i = 0; i < strD.Length; i++) strD[i] *= supD;
            }
        }
        // tácticas: a última parcela do lado inteiro, e a única que muda de dia para dia sem ninguém mandar.
        // Precisa de saber ONDE se combate — sem região não há tabela nem chão que a escolha, e a batalha
        // trava-se como se travava (é o caminho dos testes que chamam o ResolveTick à mão).
        if (battleRegion is not null)
        {
            float tacA = Tactics.Mult(w, battleRegion, attacking: true);
            float tacD = Tactics.Mult(w, battleRegion, attacking: false);
            for (int i = 0; i < strA.Length; i++) strA[i] *= tacA;
            for (int i = 0; i < strD.Length; i++) strD[i] *= tacD;
        }
        Exchange(w, att, strA, def, "defense");
        Exchange(w, def, strD, att, "breakthrough");
        float xpGain = w.Rule("xp_per_battle_day", 1f), xpMax = w.Rule("xp_max", 100f);
        foreach (var d in att.Concat(def))
        {
            d.Xp = MathF.Min(xpMax, d.Xp + xpGain);
            d.Org -= 4f * (1f - MathF.Min(1f, d.Supply));
            d.Org = MathF.Max(0f, d.Org); d.Hp = MathF.Max(0f, d.Hp);
            if (d.Hp <= 0f)
            {
                w.Events.Publish(new DivisionDestroyed(d.Id));
                // O cansaço de guerra é de quem enterra os seus: uma divisão voluntária morre longe, mas
                // quem a perde é a casa dela, não o anfitrião que a estava a comandar.
                if (w.Countries.TryGetValue(d.HomeId, out var cc))
                    cc.WarExhaustion = MathF.Min(w.Rule("exhaustion_max", 30f),
                        cc.WarExhaustion + w.Rule("exhaustion_per_division", 2f));
            }
        }
    }

    /// <summary>Assalto anfíbio: quem ataca do outro lado de uma travessia marítima bate da praia e
    /// vale só naval_invasion_penalty da sua força. Fora disso, 1.
    ///
    /// Menos para os fuzileiros: a marca `anfibio` na ficha (tabela unit_tag, nunca no código) troca a
    /// penalização pela de naval_invasion_marine. É o que os torna fuzileiros — sair do barco a bater é o
    /// trabalho deles, e uma brigada de infantaria a fazer o mesmo chega à praia desfeita.</summary>
    public static float AmphibiousMult(World w, Division d, Region? battleRegion) =>
        battleRegion is not null && w.IsSeaHop(d.RegionId, battleRegion.Id)
            ? (IsMarine(w, d) ? w.Rule("naval_invasion_marine", 0.8f) : w.Rule("naval_invasion_penalty", 0.45f))
            : 1f;

    /// <summary>Fuzileiros? A marca vem da tabela. Template partido não é fuzileiro, mas também não rebenta
    /// com a batalha.</summary>
    public static bool IsMarine(World w, Division d)
    {
        try { return w.Stats.Get(d.TemplateId).Tags.Contains("anfibio"); }
        catch { return false; }
    }

    /// <summary>A força com que cada divisão de um lado se bate hoje. `parts`, quando vem, recebe a soma de
    /// cada parcela ao longo da linha — é por aí que o ecrã de batalha explica o resultado sem repetir uma
    /// única conta: as parcelas que ele mostra são estas, não uma segunda versão delas.</summary>
    public static float[] SideStrength(World w, List<Division> divs, ModContext ctx, bool attacking,
                                       Region? battleRegion = null, List<CombatFactor>? parts = null)
    {
        var out_ = new float[divs.Count];
        for (int i = 0; i < divs.Count; i++)
        {
            var d = divs[i]; var st = w.Stats.Get(d.TemplateId);
            // O contexto é do lado, mas ser voluntário é da divisão: quem veio de fora bate-se pior na guerra
            // dos outros, e quanto pior é uma linha da tabela modifier que decide (VolunteerSystem).
            if (d.IsVolunteer) ctx["volunteer"] = "true"; else ctx.Remove("volunteer");
            var (f1, m1) = w.Modifiers.Evaluate("str", st, ctx);
            var (f2, m2) = w.Modifiers.Evaluate(attacking ? "str_attacker" : "str_defender", st, ctx);
            float terrainAir = MathF.Max(0.1f, m1 * m2 + f1 + f2);
            float supply = 0.4f + 0.6f * MathF.Min(1f, d.Supply);
            float morale = 0.5f + d.Org / 200f;
            var (cf, cm) = w.Modifiers.Evaluate("command", st, ctx);
            float command = cm + cf;
            // veterania por graus da tabela (Veterancy): recruta não leva nada, elite leva o bónus inteiro
            float veterancy = 1f + Veterancy.Bonus(w, d)
                              + MedalSystem.Bonus(w, d);   // condecorações: veteranos batem-se melhor
            // doutrina militar (leis grupo doctrine): country stat attack/defense, 1 por omissão
            string statKey = attacking ? "attack" : "defense";
            // doutrina do país × o comandante que estiver destacado ao grupo desta divisão
            float doctrine = (w.Countries.TryGetValue(d.CountryId, out var dc) ? dc.Stat(statKey) : 1f) * w.CommandMult(d, statKey);
            float amphibious = attacking ? AmphibiousMult(w, d, battleRegion) : 1f;
            // trincheira: os dias de mãos quietas neste chão só valem a quem espera o assalto (EntrenchSystem)
            float dug = attacking ? 1f : EntrenchSystem.Bonus(w, d);
            // plano de batalha: o que o estado-maior preparou enquanto a frente esteve quieta (BattlePlanSystem)
            float plan = BattlePlanSystem.Bonus(w, d);
            out_[i] = MathF.Max(0.05f, terrainAir * supply * morale * veterancy * doctrine * amphibious * dug * plan * MathF.Max(0.3f, command));
            if (parts is null) continue;
            Add(parts, "terreno, rio e tecnologia", terrainAir);
            Add(parts, "abastecimento", supply);
            Add(parts, "organização", morale);
            Add(parts, "veterania e condecorações", veterancy);
            Add(parts, "doutrina e comandante", doctrine);
            Add(parts, "estado-maior", MathF.Max(0.3f, command));
            if (attacking) Add(parts, "assalto anfíbio", amphibious);
            else Add(parts, "trincheira", dug);
            Add(parts, "plano de batalha", plan);
        }
        ctx.Remove("volunteer");   // o contexto é do lado: não fica sujo com a última divisão que passou
        return out_;
    }

    /// <summary>Soma uma parcela ao acumulador, pela ordem em que apareceu a primeira vez.</summary>
    private static void Add(List<CombatFactor> parts, string name, float mult)
    {
        for (int i = 0; i < parts.Count; i++)
            if (parts[i].Name == name) { parts[i] = parts[i] with { Mult = parts[i].Mult + mult }; return; }
        parts.Add(new CombatFactor(name, mult));
    }

    private void Exchange(World w, List<Division> src, float[] srcStr, List<Division> tgt, string tgtDefKey)
    {
        if (tgt.Count == 0) return;
        for (int i = 0; i < src.Count; i++)
        {
            var d = src[i]; var st = w.Stats.Get(d.TemplateId);
            var t = tgt[w.Rng.Next(tgt.Count)]; var ts = w.Stats.Get(t.TemplateId);
            float atk = st["soft_atk"] * (1f - ts["hardness"]) + st["hard_atk"] * ts["hardness"];
            if (st["piercing"] < ts["armor"]) atk *= 0.5f;
            float hits = srcStr[i] * atk * (d.Hp / 100f);
            float absorb = ts[tgtDefKey] * (t.Hp / 100f);
            float covered = MathF.Min(hits, absorb), uncovered = MathF.Max(0f, hits - absorb);
            float dmg = (covered * 0.1f + uncovered * 0.4f) * DamageScale * (0.6f + 0.8f * (float)w.Rng.NextDouble());
            t.Org -= dmg * 2f;
            t.Hp -= dmg * (1f - ts["hardness"] * 0.5f);
        }
    }

    /// <summary>Porque é que este lado está a ganhar ou a perder: a força média com que a linha dele se bate
    /// hoje, aberta nas parcelas que a fazem.
    ///
    /// É a assinatura do HoI4 — passar o dedo por uma batalha e ver a lista de modificadores, linha a linha:
    /// o terreno, o rio, o abastecimento, a trincheira, o forte, o céu. Sem isto uma batalha perdida é um
    /// azar; com isto é uma lição, e é a diferença entre um jogo que se aprende e um que se sofre.
    ///
    /// Nada aqui é uma segunda versão das contas: as parcelas de divisão vêm do próprio SideStrength (o
    /// acumulador `parts`), e as do lado inteiro — forte, informações, céu — são as mesmas expressões do
    /// ResolveTick, aplicadas na mesma ordem. A força devolvida é a média da linha já com elas dentro.</summary>
    public static CombatSide Explain(World w, Region r, List<Division> line, bool attacking, int countryId, int enemyId)
    {
        var parts = new List<CombatFactor>();
        if (line.Count == 0) return new CombatSide(0f, parts);

        var ctx = BuildContext(w, r, countryId);
        var str = SideStrength(w, line, ctx, attacking, r, parts);
        for (int i = 0; i < parts.Count; i++) parts[i] = parts[i] with { Mult = parts[i].Mult / line.Count };
        float strength = str.Average();

        // as parcelas do lado inteiro, pela ordem do ResolveTick
        if (!attacking && r.Fort > 0)
        {
            float m = 1f + r.Fort * w.Rule("fort_defense_per_level", 0.15f);
            parts.Add(new CombatFactor($"fortificações (nível {r.Fort})", m)); strength *= m;
        }
        if (w.Countries.ContainsKey(enemyId) && w.HasIntel(countryId, enemyId))
        {
            float m = w.Rule("intel_combat_bonus", 1.05f);
            parts.Add(new CombatFactor("informações sobre o inimigo", m)); strength *= m;
        }
        if (w.Countries.TryGetValue(countryId, out var me) && w.Countries.TryGetValue(enemyId, out var foe))
        {
            float mine = me.AirPower + AirMissionSystem.Superiority(w, r.Id, me.Id);
            float theirs = foe.AirPower + AirMissionSystem.Superiority(w, r.Id, foe.Id);
            if (mine + theirs > 0f)
            {
                float m = 1f + (mine / (mine + theirs) - 0.5f) * 2f * w.Rule("air_combat_weight", 0.15f);
                parts.Add(new CombatFactor("superioridade aérea", m)); strength *= m;
            }
            float sup = 1f + AirMissionSystem.Support(w, r.Id, me.Id);
            if (sup != 1f) { parts.Add(new CombatFactor("apoio aéreo próximo", sup)); strength *= sup; }
        }
        if (Tactics.Of(w, r, attacking) is TacticDef tac)
        {
            float m = Tactics.Mult(w, r, attacking);
            parts.Add(new CombatFactor($"táctica: {tac.Name}" + (Tactics.Countered(w, r, attacking) ? " (lida)" : ""), m));
            strength *= m;
        }
        return new CombatSide(strength, parts);
    }
}
