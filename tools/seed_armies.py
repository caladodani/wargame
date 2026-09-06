"""Exército inicial → static.db (templates por país, divisões no dia 0, capital).

Uso: ~/.venvs/wargame-tools/bin/python tools/seed_armies.py [--db data/static.db]
(import_map.py chama seed() no fim; correr à mão só para re-semear sem refazer o mapa.)

Regras vêm da tabela `rule` (start_div_per_million, start_div_min, start_div_max) e de country_stat
(start_army_mult). Nada aqui é número de jogo, só a forma de os aplicar:
 - 4 templates genéricos por país + os de country_template desse país (características únicas);
 - divisões nomeadas de country_unit primeiro (brigadas reais), depois geradas até ao total
   ∝ população × start_army_mult, com mínimo/tecto; composição das geradas: 1 blindada por 6,
   1 infantaria AT por 4, 1 mecanizada por 8, resto infantaria;
 - colocação das geradas por região ∝ população (maiores restos); capital = região mais populosa.
"""
import argparse, sqlite3, unicodedata
from pathlib import Path

# (nome, [(unit_type_id, qty)]) — as três primeiras são as dos testes de calibração (CombatTests).
TEMPLATES = [
    ('Infantaria',    [(1, 6), (4, 2)]),
    ('Blindada',      [(3, 4), (2, 3), (4, 1)]),
    ('Infantaria AT', [(1, 5), (4, 1), (6, 2)]),
    ('Mecanizada',    [(2, 4), (1, 2), (4, 1)]),
]
INF, ARM, INF_AT, MEC = 0, 1, 2, 3


def kind(i):
    if i % 6 == 5: return ARM
    if i % 4 == 3: return INF_AT
    if i % 8 == 6: return MEC
    return INF


def norm(s):
    return ''.join(ch for ch in unicodedata.normalize('NFD', s or '') if unicodedata.category(ch) != 'Mn').lower().strip()


def seed(db, verbose=False):
    rule = dict(db.execute('SELECT key,value FROM rule'))
    per_m, dmin, dmax = rule['start_div_per_million'], int(rule['start_div_min']), int(rule['start_div_max'])
    db.execute('DELETE FROM start_division'); db.execute('DELETE FROM template_unit'); db.execute('DELETE FROM template')

    tid = 0; did = 0; warnings = []
    for cid, tag in db.execute('SELECT id,tag FROM country ORDER BY id').fetchall():
        regions = db.execute('SELECT id,name,population FROM region WHERE owner_id=? ORDER BY population DESC, id', (cid,)).fetchall()
        if not regions: continue
        capital = regions[0][0]
        db.execute('UPDATE country SET capital_region_id=? WHERE id=?', (capital, cid))
        stat = dict(db.execute('SELECT key,value FROM country_stat WHERE country_tag=?', (tag,)))

        # templates: genéricos + próprios do país
        by_name = {}; generic_ids = []
        for name, units in TEMPLATES:
            tid += 1; generic_ids.append(tid); by_name[norm(name)] = tid
            db.execute('INSERT INTO template VALUES (?,?,?)', (tid, cid, name))
            db.executemany('INSERT INTO template_unit VALUES (?,?,?)', [(tid, u, q) for u, q in units])
        for ctid, name in db.execute('SELECT id,name FROM country_template WHERE country_tag=? ORDER BY id', (tag,)).fetchall():
            units = db.execute('SELECT unit_type_id,qty FROM country_template_unit WHERE country_template_id=?', (ctid,)).fetchall()
            tid += 1; by_name[norm(name)] = tid
            db.execute('INSERT INTO template VALUES (?,?,?)', (tid, cid, name))
            db.executemany('INSERT INTO template_unit VALUES (?,?,?)', [(tid, u, q) for u, q in units])

        # divisões nomeadas (brigadas reais)
        region_by_name = {norm(n): rid for rid, n, _ in regions}
        named = 0
        for name, tname, rname in db.execute('SELECT name,template_name,region_name FROM country_unit WHERE country_tag=? ORDER BY id', (tag,)).fetchall():
            t = by_name.get(norm(tname))
            if t is None:
                warnings.append(f'{tag}: template "{tname}" da unidade "{name}" não existe — saltada'); continue
            rid = region_by_name.get(norm(rname)) if rname else None
            if rname and rid is None: warnings.append(f'{tag}: região "{rname}" da unidade "{name}" não existe — capital')
            did += 1; named += 1
            db.execute('INSERT INTO start_division VALUES (?,?,?,?,?)', (did, cid, t, rid or capital, name))

        # geradas até ao total
        pop = sum(p for _, _, p in regions)
        if pop <= 0: continue
        n = min(dmax, max(dmin, round(pop / 1e6 * per_m * stat.get('start_army_mult', 1.0))))
        n = max(0, n - named)
        if n == 0: continue
        quotas = [(rid, n * p / pop) for rid, _, p in regions]
        alloc = {rid: int(q) for rid, q in quotas}
        for rid, q in sorted(quotas, key=lambda x: -(x[1] - int(x[1])))[: n - sum(alloc.values())]:
            alloc[rid] += 1
        i = 0; counter = {}
        for rid, _, _ in regions:
            for _ in range(alloc[rid]):
                k = kind(i); t = generic_ids[k]; counter[k] = counter.get(k, 0) + 1
                did += 1
                db.execute('INSERT INTO start_division VALUES (?,?,?,?,?)', (did, cid, t, rid, f'{counter[k]}.ª {TEMPLATES[k][0]}'))
                i += 1
    db.commit()
    if verbose:
        for w in warnings: print('aviso:', w)
    return tid, did


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--db', default=str(Path(__file__).resolve().parent.parent / 'data' / 'static.db'))
    a = ap.parse_args()
    db = sqlite3.connect(a.db)
    t, d = seed(db, verbose=True)
    print(f'{t} templates, {d} divisões iniciais')
    db.close()


if __name__ == '__main__':
    main()
