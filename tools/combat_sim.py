"""Simulador de calibração de combate — espelha o esquema SQLite planeado.
Tabelas em memória: unit_type, unit_stat, unit_tag, modifier, template, template_unit, division.
"""
import random, time, statistics
from collections import defaultdict

# ---------------- "Tabelas" ----------------
unit_type = {
    'inf':  dict(name='Infantaria',  supply=1.0, mobility=25),
    'mech': dict(name='Mecanizada',  supply=1.6, mobility=45),
    'arm':  dict(name='Blindados',   supply=2.2, mobility=40),
    'art':  dict(name='Artilharia',  supply=1.4, mobility=25),
    'aa':   dict(name='AA/Anti-drone', supply=1.1, mobility=25),
    'at':   dict(name='Anti-tanque', supply=1.2, mobility=25),
}
unit_stat = {  # unit_type_id -> {stat_key: value}
    'inf':  dict(soft_atk=6,  hard_atk=1,  defense=24, breakthrough=8,  armor=0,  piercing=5,  hardness=0.1, hp=25),
    'mech': dict(soft_atk=10, hard_atk=4,  defense=26, breakthrough=16, armor=15, piercing=20, hardness=0.5, hp=30),
    'arm':  dict(soft_atk=12, hard_atk=14, defense=12, breakthrough=28, armor=60, piercing=55, hardness=0.9, hp=20),
    'art':  dict(soft_atk=20, hard_atk=2,  defense=6,  breakthrough=6,  armor=0,  piercing=8,  hardness=0.2, hp=6),
    'aa':   dict(soft_atk=2,  hard_atk=1,  defense=10, breakthrough=2,  armor=5,  piercing=10, hardness=0.3, hp=8, air_deny=0.1),
    'at':   dict(soft_atk=2,  hard_atk=20, defense=8,  breakthrough=2,  armor=0,  piercing=70, hardness=0.2, hp=6),
}
unit_tag = {
    'inf': {'infantry', 'ground'}, 'mech': {'infantry', 'armored', 'ground'},
    'arm': {'armored', 'ground'}, 'art': {'support', 'ground'}, 'aa': {'support', 'ground'}, 'at': {'support', 'ground'},
}
# modifier(source_kind, condition, stat_key, op, value) — condição: (campo, valor) ou None
modifier = [
    ('terrain', ('terrain', 'forest'),   'str_attacker', 'mul', 0.8),   # terreno penaliza só o atacante
    ('terrain', ('terrain', 'urban'),    'str_attacker', 'mul', 0.6),
    ('terrain', ('terrain', 'mountain'), 'str_attacker', 'mul', 0.5),
    ('terrain', ('terrain', 'urban'),    'str_attacker:armored', 'mul', 0.6),   # blindados sofrem mais em urbano
    ('terrain', ('terrain', 'mountain'), 'str_attacker:armored', 'mul', 0.6),
    ('terrain', ('river', True),         'str_attacker', 'add', -0.3),
    ('terrain', ('terrain', 'urban'),    'str_defender', 'mul', 1.2),   # entrincheiramento
    ('terrain', ('terrain', 'mountain'), 'str_defender', 'mul', 1.3),
    ('air',     ('air_sup', 'own'),      'str', 'add', 0.25),
    ('air',     ('air_sup', 'enemy'),    'str', 'add', -0.25),
    ('tech',    ('tech', 'drones_1'),    'str', 'add', 0.10),
    ('cyber',   ('cyber_hit', True),     'command', 'mul', 0.9),
]
template = {
    'inf_div':  {'inf': 6, 'art': 2},
    'mech_div': {'mech': 6, 'art': 2, 'aa': 1},
    'arm_div':  {'arm': 4, 'mech': 3, 'art': 1},
    'militia':  {'inf': 4},
    'inf_at_div': {'inf': 5, 'art': 1, 'at': 2},
}
FRONT_WIDTH = 4  # divisões por lado sem penalidade

# ---------------- StatBlock / cache ----------------
_stat_cache = {}
def division_stats(tmpl_id):
    if tmpl_id in _stat_cache:
        return _stat_cache[tmpl_id]
    s = defaultdict(float); tags = set(); n = 0; mx = defaultdict(float)
    for ut, qty in template[tmpl_id].items():
        for k, v in unit_stat[ut].items():
            s[k] += v * qty
            mx[k] = max(mx[k], v)
        tags |= unit_tag[ut]; n += qty
    s['hardness'] /= n
    s['armor']    = 0.3 * mx['armor']    + 0.7 * s['armor'] / n      # estilo HoI
    s['piercing'] = 0.5 * mx['piercing'] + 0.5 * s['piercing'] / n
    s['tags'] = tags
    _stat_cache[tmpl_id] = dict(s)
    return _stat_cache[tmpl_id]

# ---------------- ModifierEngine ----------------
def apply_mods(ctx, stats, key):
    flat, mul = 0.0, 1.0
    for _, cond, skey, op, val in modifier:
        base, _, tag = skey.partition(':')
        if base != key: continue
        if tag and tag not in stats['tags']: continue
        if cond and ctx.get(cond[0]) != cond[1]: continue
        if op == 'add': flat += val
        else: mul *= val
    return flat, mul

# ---------------- Divisão ----------------
class Division:
    __slots__ = ('tmpl', 'hp', 'org', 'supply')
    def __init__(self, tmpl, supply=1.0):
        self.tmpl, self.hp, self.org, self.supply = tmpl, 100.0, 100.0, supply
    @property
    def stats(self): return division_stats(self.tmpl)
    def alive(self): return self.org >= 10 and self.hp > 0

def side_strength(divs, ctx, attacking):
    """Str = Base × Terreno × Supr × Ar × Moral × Comando (média do lado)"""
    out = []
    excess = max(0, len(divs) - FRONT_WIDTH)
    for d in divs:
        st = d.stats
        flat, mul = apply_mods(ctx, st, 'str')
        f2, m2 = apply_mods(ctx, st, 'str_attacker' if attacking else 'str_defender'); flat += f2; mul *= m2
        terrain_air = max(0.1, mul + flat)
        supply = 0.4 + 0.6 * min(1.0, d.supply)
        morale = 0.5 + d.org / 200
        cf, cm = apply_mods(ctx, st, 'command')
        command = ctx.get('general', 1.0) * cm + cf - 0.15 * excess
        out.append(max(0.05, terrain_air * supply * morale * max(0.3, command)))
    return out

def resolve_tick(att, dfn, ctx_a, ctx_d, rng, K=0.45):
    """HoI-like: cada ponto de ataque coberto pela defesa = 10% dano, descoberto = 40%."""
    str_a = side_strength(att, ctx_a, True)
    str_d = side_strength(dfn, ctx_d, False)
    def exchange(src, src_str, tgt, tgt_defkey):
        if not tgt: return
        for d, s in zip(src, src_str):
            st = d.stats
            t = rng.choice(tgt); ts = t.stats
            atk = st['soft_atk'] * (1 - ts['hardness']) + st['hard_atk'] * ts['hardness']
            if st['piercing'] < ts['armor']: atk *= 0.5
            hits = s * atk * (d.hp / 100)
            absorb = ts[tgt_defkey] * (t.hp / 100)
            covered = min(hits, absorb); uncovered = max(0.0, hits - absorb)
            dmg = (covered * 0.1 + uncovered * 0.4) * K * rng.uniform(0.6, 1.4)
            t.org -= dmg * 2
            t.hp -= dmg * (1 - ts['hardness'] * 0.5)
    exchange(att, str_a, dfn, 'defense')
    exchange(dfn, str_d, att, 'breakthrough')
    for d in att + dfn:
        d.org -= 4 * (1 - min(1.0, d.supply))          # atrição sem suprimento
        d.org = max(0.0, d.org); d.hp = max(0.0, d.hp)

def battle(att, dfn, ctx_a, ctx_d, seed=0, max_days=60):
    rng = random.Random(seed)
    for day in range(1, max_days + 1):
        resolve_tick(att, dfn, ctx_a, ctx_d, rng)
        att = [d for d in att if d.alive()] or att
        dfn_alive = [d for d in dfn if d.alive()]
        if not dfn_alive: return 'ATT', day
        if not any(d.alive() for d in att): return 'DEF', day
        dfn = dfn_alive
    return 'STALL', max_days

def scenario(name, att_tpl, dfn_tpl, ctx_a=None, ctx_d=None, sup_a=1.0, sup_d=1.0, n=200):
    ctx_a = ctx_a or {'terrain': 'plain'}; ctx_d = ctx_d or {'terrain': 'plain'}
    res = defaultdict(list)
    for seed in range(n):
        att = [Division(t, sup_a) for t in att_tpl]
        dfn = [Division(t, sup_d) for t in dfn_tpl]
        w, days = battle(att, dfn, ctx_a, ctx_d, seed)
        res[w].append(days)
    tot = sum(len(v) for v in res.values())
    line = f"{name:<48}"
    for w in ('ATT', 'DEF', 'STALL'):
        pct = 100 * len(res[w]) / tot
        md = statistics.median(res[w]) if res[w] else 0
        line += f" {w} {pct:5.1f}% ({md:>2.0f}d)"
    print(line)

if __name__ == '__main__':
    print("=== CALIBRAÇÃO (200 seeds/cenário) — atacante vs defensor ===")
    I, M, A, Mi = 'inf_div', 'mech_div', 'arm_div', 'militia'
    scenario("2 inf vs 2 inf, planície",                 [I]*2, [I]*2)
    scenario("3 inf vs 2 inf, planície",                 [I]*3, [I]*2)
    scenario("2 blind vs 2 inf, planície",               [A]*2, [I]*2)
    scenario("2 blind vs 2 inf, urbano",                 [A]*2, [I]*2, {'terrain':'urban'}, {'terrain':'urban'})
    scenario("2 blind vs 2 inf, montanha",               [A]*2, [I]*2, {'terrain':'mountain'}, {'terrain':'mountain'})
    scenario("2 inf vs 2 inf, rio",                      [I]*2, [I]*2, {'terrain':'plain','river':True})
    scenario("2 mech vs 2 inf, sup. aérea própria",      [M]*2, [I]*2, {'terrain':'plain','air_sup':'own'}, {'terrain':'plain','air_sup':'enemy'})
    scenario("2 inf vs 2 inf, defensor sem suprimento",  [I]*2, [I]*2, sup_d=0.0)
    scenario("2 inf vs 2 inf, general 1.3 vs 1.0",       [I]*2, [I]*2, {'terrain':'plain','general':1.3})
    scenario("6 inf vs 2 inf (largura excedida)",        [I]*6, [I]*2)
    scenario("2 blind vs 2 mech, planície",              [A]*2, [M]*2)
    scenario("2 inf vs 3 milícia",                       [I]*2, [Mi]*3)
    scenario("2 blind vs 2 inf+AT, planície",            [A]*2, ['inf_at_div']*2)
    scenario("2 blind vs 2 inf+AT, montanha",            [A]*2, ['inf_at_div']*2, {'terrain':'mountain'}, {'terrain':'mountain'})
    scenario("2 inf vs 2 inf, ciber no defensor",        [I]*2, [I]*2, ctx_d={'terrain':'plain','cyber_hit':True})

    print("\n=== ESCALA: 5000 divisões, 300 batalhas ativas, 1 tick ===")
    rng = random.Random(1)
    divs = [Division(rng.choice(list(template))) for _ in range(5000)]
    battles = [(divs[i*8:i*8+4], divs[i*8+4:i*8+8]) for i in range(300)]
    ctx = {'terrain': 'plain'}
    t0 = time.perf_counter()
    for a, d in battles: resolve_tick(a, d, ctx, ctx, rng)
    ms = (time.perf_counter() - t0) * 1000
    print(f"tick: {ms:.1f} ms em Python puro (C# esperado ~10-20x mais rápido)")
