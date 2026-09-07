using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Systems;

/// <summary>Port directo de combat_sim.py. Só corre nas regiões em World.ActiveBattles.
///
/// Desde a largura de frente (Frontage), cada lado entra no dia com uma linha e uma reserva: só a linha bate e
/// só a linha apanha. A batalha continua enquanto houver alguém de pé — as reservas contam para isso.</summary>
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

    private static ModContext BuildContext(World w, Region r, int countryId)
    {
        var ctx = new ModContext().With("terrain", r.Terrain).With("country", w.Countries[countryId].Tag);
        if (r.River) ctx["river"] = "true";
        // Outros sistemas (Air, Cyber, Research) escrevem aqui via flags na região/país — ver AirSystem.
        foreach (var tech in w.Countries[countryId].Techs) ctx[$"tech:{tech}"] = "true";
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
        // superioridade aérea: razão de esquadrões modula a força (±air_combat_weight no máximo)
        if (att.Count > 0 && def.Count > 0
            && w.Countries.TryGetValue(att[0].CountryId, out var ac) && w.Countries.TryGetValue(def[0].CountryId, out var dc2))
        {
            float airTot = ac.AirPower + dc2.AirPower;
            if (airTot > 0f)
            {
                float weight = w.Rule("air_combat_weight", 0.15f);
                float mA = 1f + (ac.AirPower / airTot - 0.5f) * 2f * weight;
                float mD = 1f + (dc2.AirPower / airTot - 0.5f) * 2f * weight;
                for (int i = 0; i < strA.Length; i++) strA[i] *= mA;
                for (int i = 0; i < strD.Length; i++) strD[i] *= mD;
            }
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
                if (w.Countries.TryGetValue(d.CountryId, out var cc))
                    cc.WarExhaustion = MathF.Min(w.Rule("exhaustion_max", 30f),
                        cc.WarExhaustion + w.Rule("exhaustion_per_division", 2f));
            }
        }
    }

    /// <summary>Assalto anfíbio: quem ataca do outro lado de uma travessia marítima bate da praia e
    /// vale só naval_invasion_penalty da sua força. Fora disso, 1.</summary>
    public static float AmphibiousMult(World w, Division d, Region? battleRegion) =>
        battleRegion is not null && w.IsSeaHop(d.RegionId, battleRegion.Id) ? w.Rule("naval_invasion_penalty", 0.45f) : 1f;

    private float[] SideStrength(World w, List<Division> divs, ModContext ctx, bool attacking, Region? battleRegion = null)
    {
        var out_ = new float[divs.Count];
        for (int i = 0; i < divs.Count; i++)
        {
            var d = divs[i]; var st = w.Stats.Get(d.TemplateId);
            var (f1, m1) = w.Modifiers.Evaluate("str", st, ctx);
            var (f2, m2) = w.Modifiers.Evaluate(attacking ? "str_attacker" : "str_defender", st, ctx);
            float terrainAir = MathF.Max(0.1f, m1 * m2 + f1 + f2);
            float supply = 0.4f + 0.6f * MathF.Min(1f, d.Supply);
            float morale = 0.5f + d.Org / 200f;
            var (cf, cm) = w.Modifiers.Evaluate("command", st, ctx);
            float command = cm + cf;
            float veterancy = 1f + d.Xp / w.Rule("xp_max", 100f) * w.Rule("veterancy_bonus", 0.25f)
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
        }
        return out_;
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
}
