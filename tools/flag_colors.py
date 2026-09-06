#!/usr/bin/env python3
"""Descarrega bandeiras (flagcdn w80) para assets/flags/<TAG>.png e gera
data/flag_colors.sql com a cor dominante de cada bandeira (UPDATE country SET color).
O import_map.py aplica esse SQL no fim; correr isto só quando mudarem países/bandeiras.

Uso: ~/.venvs/wargame-tools/bin/python tools/flag_colors.py --ne ~/ne [--db data/static.db]
"""
import argparse, collections, json, sqlite3, time, urllib.request
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent.parent

def dominant(path):
    im = Image.open(path).convert('RGB')
    cnt = collections.Counter()
    for r, g, b in im.getdata():
        cnt[(r // 24 * 24 + 12, g // 24 * 24 + 12, b // 24 * 24 + 12)] += 1
    (r, g, b), _ = cnt.most_common(1)[0]
    return '#%02x%02x%02x' % (min(r, 255), min(g, 255), min(b, 255))

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ne', required=True)
    ap.add_argument('--db', default=str(HERE / 'data' / 'static.db'))
    a = ap.parse_args()

    d = json.load(open(Path(a.ne).expanduser() / 'ne_50m_admin_0_countries.geojson'))
    iso2 = {}
    for f in d['features']:
        p = f['properties']
        c = p.get('ISO_A2_EH') or p.get('ISO_A2')
        if c and c != '-99': iso2[p['ADM0_A3']] = c

    db = sqlite3.connect(a.db)
    tags = [r[0] for r in db.execute('SELECT tag FROM country ORDER BY id')]
    out = HERE / 'assets' / 'flags'; out.mkdir(parents=True, exist_ok=True)
    miss = []
    for tag in tags:
        a2 = iso2.get(tag)
        if not a2: miss.append(tag); continue
        dst = out / f'{tag}.png'
        if dst.exists(): continue
        url = f'https://flagcdn.com/w80/{a2.lower()}.png'
        try:
            with urllib.request.urlopen(url, timeout=15) as r: dst.write_bytes(r.read())
        except Exception as e: miss.append(f'{tag}({e})')
        time.sleep(0.05)

    lines = ['-- Cor principal da bandeira por país (gerado de assets/flags/*.png; aplicado no fim do import_map.py).']
    n = 0
    for tag in tags:
        p = out / f'{tag}.png'
        if not p.exists(): continue
        c = dominant(p)
        lines.append(f"UPDATE country SET color='{c}' WHERE tag='{tag}';")
        db.execute('UPDATE country SET color=? WHERE tag=?', (c, tag)); n += 1
    db.commit()
    (HERE / 'data' / 'flag_colors.sql').write_text('\n'.join(lines) + '\n')
    print(f'{n} cores; sem bandeira: {miss}')

if __name__ == '__main__':
    main()
