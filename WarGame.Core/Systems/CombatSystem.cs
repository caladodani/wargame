using WarGame.Core.Events;
using WarGame.Core.Model;
using WarGame.Core.Stats;

namespace WarGame.Core.Systems;

/// <summary>Port directo de combat_sim.py. Só corre nas regiões em World.ActiveBattles.</summary>
public sealed class CombatSystem : ISystem
{
    public string Name => "Combat";
    public float DamageScale { get; init; } = 0.45f;
    public int FrontWidth { get; init; } = 4;

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

            ResolveTick(w, att, def, ctxA, ctxD, 1f + region.Fort * w.Rule("fort_defense_per_level", 0.15f));
            b.Days++;
            foreach (var d in att.Concat(def)) if (d.Hp <= 0f) dead.Add(d.Id);

            bool defOut = !def.Any(d => d.CanFight);
            bool attOut = !att.Any(d => d.CanFight);
            if (defOut || attOut)
            {
                w.ActiveBattles.RemoveAt(i);
                w.Events.Publish(new BattleEnded(b.RegionId, defOut));
                if (defOut)
                {
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

    public void ResolveTick(World w, List<Division> att, List<Division> def, ModContext ctxA, ModContext ctxD, float fortMult = 1f)
    {
        var strA = SideStrength(w, att, ctxA, attacking: true);
        var strD = SideStrength(w, def, ctxD, attacking: false);
        if (fortMult != 1f) for (int i = 0; i < strD.Length; i++) strD[i] *= fortMult;
        Exchange(w, att, strA, def, "defense");
        Exchange(w, def, strD, att, "breakthrough");
        foreach (var d in att.Concat(def))
        {
            d.Org -= 4f * (1f - MathF.Min(1f, d.Supply));
            d.Org = MathF.Max(0f, d.Org); d.Hp = MathF.Max(0f, d.Hp);
            if (d.Hp <= 0f) w.Events.Publish(new DivisionDestroyed(d.Id));
        }
    }

    private float[] SideStrength(World w, List<Division> divs, ModContext ctx, bool attacking)
    {
        var out_ = new float[divs.Count];
        int excess = Math.Max(0, divs.Count - FrontWidth);
        for (int i = 0; i < divs.Count; i++)
        {
            var d = divs[i]; var st = w.Stats.Get(d.TemplateId);
            var (f1, m1) = w.Modifiers.Evaluate("str", st, ctx);
            var (f2, m2) = w.Modifiers.Evaluate(attacking ? "str_attacker" : "str_defender", st, ctx);
            float terrainAir = MathF.Max(0.1f, m1 * m2 + f1 + f2);
            float supply = 0.4f + 0.6f * MathF.Min(1f, d.Supply);
            float morale = 0.5f + d.Org / 200f;
            var (cf, cm) = w.Modifiers.Evaluate("command", st, ctx);
            float command = cm + cf - 0.15f * excess;
            out_[i] = MathF.Max(0.05f, terrainAir * supply * morale * MathF.Max(0.3f, command));
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
