"""Exército inicial → static.db (templates por país, divisões no dia 0, capital).

Uso: ~/.venvs/wargame-tools/bin/python tools/seed_armies.py [--db data/static.db]
(import_map.py chama seed() no fim; correr à mão só para re-semear sem refazer o mapa.)

Regras vêm da tabela `rule` (start_div_per_million, start_div_min, start_div_max) — nada aqui é
número de jogo, só a forma de os aplicar:
 - 4 templates por país (mesma composição para todos; o jogador afina depois);
 - nº de divisões ∝ população do país, com mínimo/tecto; composição: 1 blindada por 6,
   1 infantaria AT por 4, 1 mecanizada por 8, resto infantaria;
 - colocação por região ∝ população (maiores restos), capital = região mais populosa.
"""
import argparse, sqlite3
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


def seed(db):
    rule = dict(db.execute('SELECT key,value FROM rule'))
    per_m, dmin, dmax = rule['start_div_per_million'], int(rule['start_div_min']), int(rule['start_div_max'])
    db.execute('DELETE FROM start_division'); db.execute('DELETE FROM template_unit'); db.execute('DELETE FROM template')

    tid = 0; did = 0
    for cid, in db.execute('SELECT id FROM country ORDER BY id').fetchall():
        regions = db.execute('SELECT id,population FROM region WHERE owner_id=? ORDER BY population DESC, id', (cid,)).fetchall()
        if not regions: continue
        db.execute('UPDATE country SET capital_region_id=? WHERE id=?', (regions[0][0], cid))
        ids = []
        for name, units in TEMPLATES:
            tid += 1; ids.append(tid)
            db.execute('INSERT INTO template VALUES (?,?,?)', (tid, cid, name))
            db.executemany('INSERT INTO template_unit VALUES (?,?,?)', [(tid, u, q) for u, q in units])
        pop = sum(p for _, p in regions)
        if pop <= 0: continue
        n = min(dmax, max(dmin, round(pop / 1e6 * per_m)))
        # maiores restos: quota por região ∝ população
        quotas = [(rid, n * p / pop) for rid, p in regions]
        alloc = {rid: int(q) for rid, q in quotas}
        for rid, q in sorted(quotas, key=lambda x: -(x[1] - int(x[1])))[: n - sum(alloc.values())]:
            alloc[rid] += 1
        i = 0
        for rid, _ in regions:
            for _ in range(alloc[rid]):
                did += 1
                db.execute('INSERT INTO start_division VALUES (?,?,?,?)', (did, cid, ids[kind(i)], rid))
                i += 1
    db.commit()
    return tid, did


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--db', default=str(Path(__file__).resolve().parent.parent / 'data' / 'static.db'))
    a = ap.parse_args()
    db = sqlite3.connect(a.db)
    t, d = seed(db)
    print(f'{t} templates, {d} divisões iniciais')
    db.close()


if __name__ == '__main__':
    main()
