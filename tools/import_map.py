"""Natural Earth → static.db
Uso: ~/.venvs/wargame-tools/bin/python tools/import_map.py --ne ~/ne --out data/static.db [--target 3000] [--min-per-country 3]

Etapas:
 1. admin-1 (10m) agrupado por país; k-means nos centróides até ao orçamento de regiões por país (∝ área)
 2. união dos polígonos, limpeza, simplificação, projecção Robinson → unidades Godot
 3. terreno por intersecção com ne_10m_geography_regions_polys + heurísticas de latitude / densidade
 4. população: populated_places (10m se existir) dentro da região + resto do POP_EST 30 % ∝ área, 70 % ∝ cidades
 5. rio: intersecta rivers_lake_centerlines (50m)
 6. vizinhos: STRtree, polígonos que se tocam
 7. escreve schema.sql + seed_units.sql + seed_tech.sql + country/region/region_polygon/region_neighbour
 8. país: name = NAME_PT, industry automática por PIB per capita, depois data/countries/*.sql (características únicas)
 9. seed_armies.py: templates (genéricos + country_template), exército inicial (country_unit nomeadas + geradas), capitais
"""
import argparse, json, math, sqlite3, struct, sys, time
from collections import defaultdict
from pathlib import Path

import numpy as np
from shapely.geometry import shape, Point, MultiPolygon, Polygon
from shapely.ops import unary_union, transform
from shapely.strtree import STRtree
from shapely.validation import make_valid
from sklearn.cluster import KMeans
from pyproj import Transformer

HERE = Path(__file__).resolve().parent.parent

FEATURE_TERRAIN = {
    'Range/mtn': 'mountain', 'Foothills': 'mountain', 'Gorge': 'mountain',
    'Desert': 'desert', 'Depression': 'desert',
    'Tundra': 'tundra',
    'Plain': 'plain', 'Lowland': 'plain', 'Basin': 'plain', 'Valley': 'plain', 'Wetlands': 'plain', 'Delta': 'plain',
    'Plateau': 'plain',  # os planaltos do NE (Brasil, Meseta, Decão) são enormes e maioritariamente aráveis
}
# Fração da área da região que uma classe do NE tem de cobrir para mandar; as cordilheiras do NE são
# polígonos gigantes (Andes, Rochosas, "Sistema Ibérico") — exigir maioria clara para ser montanha.
TERRAIN_MIN_SHARE = {'mountain': 0.55, 'desert': 0.4, 'tundra': 0.4, 'plain': 0.25}
PALETTE7 = ['#e6a0a0', '#a0c8e6', '#a0e6b4', '#e6dca0', '#c8a0e6', '#e6b4a0', '#a0e6e0']


def load(ne, name):
    with open(Path(ne) / f'{name}.geojson', encoding='utf-8') as f:
        return json.load(f)['features']


def approx_area_km2(g):
    """area_sqkm do NE vem a 0 — aproximar: graus² × cos(lat) × 111² km²."""
    return g.area * math.cos(math.radians(g.centroid.y)) * 111.32 ** 2


def clean(g):
    g = make_valid(g)
    if g.geom_type == 'GeometryCollection':
        g = unary_union([p for p in g.geoms if p.geom_type in ('Polygon', 'MultiPolygon')])
    return g


def to_rings(g, min_area):
    """Anéis exteriores (buracos ignorados), descartando ilhotas minúsculas."""
    polys = list(g.geoms) if g.geom_type == 'MultiPolygon' else [g]
    polys.sort(key=lambda p: p.area, reverse=True)
    out = []
    for i, p in enumerate(polys):
        if i > 0 and p.area < min_area: continue
        pts = list(p.exterior.coords)[:-1]
        if len(pts) >= 3: out.append(pts)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ne', default=str(HERE.parent / 'ne'))
    ap.add_argument('--out', default=str(HERE / 'data' / 'static.db'))
    ap.add_argument('--target', type=int, default=3000)
    ap.add_argument('--min-per-country', type=int, default=3, help='regiões mínimas por país (se o NE tiver admin-1 que chegue)')
    ap.add_argument('--world-width', type=float, default=8000.0, help='largura do mapa em unidades Godot')
    ap.add_argument('--sea-max-km', type=float, default=3200.0, help='alcance máximo de uma ligação marítima')
    ap.add_argument('--preview', default=str(HERE / 'data' / 'map_preview.png'))
    a = ap.parse_args()
    t0 = time.time()

    admin1 = load(a.ne, 'ne_10m_admin_1_states_provinces')
    adm0 = {f['properties']['ADM0_A3']: f['properties'] for f in load(a.ne, 'ne_50m_admin_0_countries')}
    geo = load(a.ne, 'ne_10m_geography_regions_polys')
    rivers = [shape(f['geometry']) for f in load(a.ne, 'ne_50m_rivers_lake_centerlines') if f['geometry']]
    places_src = 'ne_10m_populated_places' if (Path(a.ne) / 'ne_10m_populated_places.geojson').exists() else 'ne_50m_populated_places'
    places = [(Point(f['geometry']['coordinates']), f['properties'].get('POP_MAX') or 0)
              for f in load(a.ne, places_src) if f['geometry']]
    place_tree = STRtree([p for p, _ in places])
    def places_pop(g):
        return sum(places[i][1] for i in place_tree.query(g, predicate='contains'))

    # ---- 1. agrupar admin-1 por país
    by_country = defaultdict(list)
    for f in admin1:
        if not f['geometry']: continue
        p = f['properties']
        if p['adm0_a3'] in ('ATA', 'ATF', 'HMD', 'BVT', 'SGS'): continue  # Antárctida e ilhas remotas
        g = clean(shape(f['geometry']))
        by_country[p['adm0_a3']].append((g, p['name'] or p.get('name_en') or '?', approx_area_km2(g)))

    country_area = {c: sum(x[2] for x in v) or 1 for c, v in by_country.items()}

    # orçamento ∝ √(área × população), com mínimo por país: só área dava 156 países com
    # uma região (Portugal inteiro era "Beja") e 23 à Argélia. Um país pequeno e denso
    # vale mais regiões do que um deserto grande. Tecto = nº de admin-1 que o NE tem.
    country_pop = {c: float(adm0.get(c, {}).get('POP_EST') or 0) for c in by_country}
    weight = {c: math.sqrt(country_area[c] * max(country_pop[c], 1e5)) for c in by_country}
    total_w = sum(weight.values())

    # Países ao detalhe máximo (todas as admin-1): os que o jogador quer jogar a sério.
    FULL_DETAIL = {'PRT', 'BRA'}

    def budgets(k):
        return {c: len(by_country[c]) if c in FULL_DETAIL else
                min(len(by_country[c]), max(a.min_per_country, round(k * weight[c] / total_w)))
                for c in by_country}
    k = a.target
    for _ in range(20):
        b = budgets(k); s = sum(b.values())
        if abs(s - a.target) < 15: break
        k *= a.target / s
    print(f'{len(admin1)} admin-1 → {sum(b.values())} regiões em {len(b)} países')

    # ---- 2. clusters + união
    regions = []  # dict(name, country, geom_ll, area)
    for c, units in by_country.items():
        n = b[c]
        if n >= len(units):
            groups = [[u] for u in units]
        else:
            cents = np.array([[u[0].centroid.x, u[0].centroid.y] for u in units])
            labels = KMeans(n_clusters=n, n_init=4, random_state=0).fit_predict(cents)
            groups = [[u for u, l in zip(units, labels) if l == g] for g in range(n)]
        for grp in groups:
            geom = clean(unary_union([u[0] for u in grp]))
            if geom.is_empty: continue
            name = max(grp, key=lambda u: (places_pop(u[0]), u[2]))[1] if len(grp) > 1 else grp[0][1]  # a mais povoada dá o nome
            regions.append(dict(name=name, country=c, geom=geom, area=sum(u[2] for u in grp)))

    # ---- 3. terreno
    geo_geoms = [clean(shape(f['geometry'])) for f in geo if f['geometry']]
    geo_cls = [f['properties']['FEATURECLA'] for f in geo if f['geometry']]
    geo_tree = STRtree(geo_geoms)
    for r in regions:
        g = r['geom']; scores = defaultdict(float)
        for i in geo_tree.query(g, predicate='intersects'):
            t = FEATURE_TERRAIN.get(geo_cls[i])
            if t:
                try: scores[t] += g.intersection(geo_geoms[i]).area
                except Exception: pass
        lat = abs(g.centroid.y)
        best = max(scores, key=scores.get) if scores else None
        belt_forest = 48 <= lat <= 63 or (lat < 8 and -80 < g.centroid.x < 60)  # boreal / equatorial (o NE não tem "floresta")
        if best and best != 'plain' and scores[best] > g.area * TERRAIN_MIN_SHARE[best]: terrain = best
        elif lat > 63: terrain = 'tundra'
        elif belt_forest: terrain = 'forest'
        else: terrain = 'plain'
        r['terrain'] = terrain

    # ---- 4. população
    # cidades dentro da região + resto do POP_EST do país repartido 30 % pela área e 70 % pela
    # população urbana (a área sozinha dava a Beja quatro vezes o Porto)
    country_place_pop = defaultdict(float)
    for r in regions:
        r['pop_places'] = places_pop(r['geom']); country_place_pop[r['country']] += r['pop_places']
    for r in regions:
        c = r['country']; cpop = float(adm0.get(c, {}).get('POP_EST') or 0)
        rest = max(0.0, cpop - country_place_pop[c])
        share_area = r['area'] / country_area[c] if country_area[c] else 0
        share_city = r['pop_places'] / country_place_pop[c] if country_place_pop[c] else share_area
        r['pop'] = int(r['pop_places'] + rest * (0.3 * share_area + 0.7 * share_city))
        if r['area'] > 0 and r['pop'] / r['area'] > 300:
            r['terrain'] = 'urban'  # metrópole manda, seja qual for o relevo

    # ---- 5. rios
    river_tree = STRtree(rivers)
    for r in regions:
        r['river'] = int(len(river_tree.query(r['geom'], predicate='intersects')) > 0)

    # ---- 6. vizinhos (em lon/lat, com pequeno buffer para tolerar arestas quase coincidentes)
    geoms = [r['geom'] for r in regions]
    tree = STRtree(geoms)
    neigh = defaultdict(set)
    for i, g in enumerate(geoms):
        gb = g.buffer(0.02)
        for j in tree.query(gb, predicate='intersects'):
            if j != i: neigh[i].add(int(j)); neigh[int(j)].add(i)

    # ---- 6b. mar: regiões costeiras + ligações marítimas (sea_link)
    # Costeira = fronteira da região toca a linha de costa (fronteira da união de toda a terra —
    # buracos da união são lagos, também contam). Ligação = par de costeiras a ≤ sea-max-km cujo
    # segmento centróide→centróide (amostrado de 40 em 40 km, pontas excluídas) é maioritariamente
    # água — assim Hamburgo→Veneza morre em terra mas Gibraltar, Báltico e até Suez/Panamá passam.
    t0 = time.time()
    import shapely
    land = shapely.union_all([g.buffer(0) for g in geoms])
    coastline = land.boundary
    shapely.prepare(coastline); shapely.prepare(land)
    coastal = [bool(coastline.intersects(g.buffer(0.01))) for g in geoms]
    cidx = [i for i, c in enumerate(coastal) if c]
    lonlat = np.array([[g.centroid.x, g.centroid.y] for g in geoms])

    def haversine_km(p, q):
        la1, la2 = math.radians(p[1]), math.radians(q[1])
        dla, dlo = la2 - la1, math.radians(q[0] - p[0])
        h = math.sin(dla / 2) ** 2 + math.cos(la1) * math.cos(la2) * math.sin(dlo / 2) ** 2
        return 2 * 6371 * math.asin(min(1, math.sqrt(h)))

    pairs, pts, owner = [], [], []   # owner[k] = índice do par a que o ponto k pertence
    for ai in range(len(cidx)):
        for bi in range(ai + 1, len(cidx)):
            i, j = cidx[ai], cidx[bi]
            if j in neigh[i]: continue
            p, q = lonlat[i], lonlat[j]
            dlon = q[0] - p[0]
            if dlon > 180: dlon -= 360   # antimeridiano (Bering): caminho curto dá a volta
            elif dlon < -180: dlon += 360
            km = haversine_km(p, (p[0] + dlon, q[1]))
            if km > a.sea_max_km or km < 1: continue
            n = max(5, int(km / 40))
            for t in np.linspace(0.15, 0.85, n):   # pontas fora: os centróides estão em terra
                lon = p[0] + dlon * t
                if lon > 180: lon -= 360
                elif lon < -180: lon += 360
                pts.append((lon, p[1] + (q[1] - p[1]) * t)); owner.append(len(pairs))
            pairs.append((i, j, km))
    on_land = shapely.contains_xy(land, np.array([p[0] for p in pts]), np.array([p[1] for p in pts]))
    landfrac = defaultdict(lambda: [0, 0])
    for k, o in enumerate(owner):
        landfrac[o][0] += int(on_land[k]); landfrac[o][1] += 1
    sea_links = [(i, j, km) for k, (i, j, km) in enumerate(pairs)
                 if landfrac[k][0] / landfrac[k][1] <= 0.45]
    print(f'mar: {len(cidx)} costeiras, {len(pairs)} pares testados, {len(sea_links)} ligações | {time.time() - t0:.0f}s')

    # ---- 2b. projecção Robinson → unidades Godot (y para baixo)
    tf = Transformer.from_crs('EPSG:4326', 'ESRI:54030', always_xy=True)
    xmax = tf.transform(180, 0)[0]; scale = a.world_width / (2 * xmax)
    def proj(x, y, z=None):
        px, py = tf.transform(x, y); return px * scale, -py * scale
    simp_tol = 0.08  # graus
    for r in regions:
        g = r['geom'].simplify(simp_tol, preserve_topology=True)
        g = clean(transform(proj, g))
        r['rings'] = to_rings(g, min_area=(0.15 * scale * 111_000) ** 2 * 1.2)  # ilhas < ~30 km² descartadas
        c = g.centroid; r['cx'], r['cy'] = c.x, c.y

    # ---- 7. escrever
    out = Path(a.out); out.parent.mkdir(parents=True, exist_ok=True)
    if out.exists(): out.unlink()
    db = sqlite3.connect(out)
    db.executescript((HERE / 'data' / 'schema.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_units.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_tech.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_world.sql').read_text(encoding='utf-8'))

    country_ids = {}
    for i, c in enumerate(sorted(by_country), start=1):
        p = adm0.get(c, {})
        country_ids[c] = i
        db.execute('INSERT INTO country(id,tag,name,color,name_en,gdp_md) VALUES (?,?,?,?,?,?)',
                   (i, c, p.get('NAME_PT') or p.get('NAME') or c, PALETTE7[(p.get('MAPCOLOR7') or i) % 7],
                    p.get('NAME') or c, float(p.get('GDP_MD') or 0)))

    for rid, r in enumerate(regions, start=1):
        r['id'] = rid
        db.execute('INSERT INTO region(id,name,owner_id,terrain,river,population,infrastructure,centroid_x,centroid_y,coastal) VALUES (?,?,?,?,?,?,?,?,?,?)',
                   (rid, r['name'], country_ids[r['country']], r['terrain'], r['river'], r['pop'], 1.0, r['cx'], r['cy'],
                    int(coastal[rid - 1])))
        for k, ring in enumerate(r['rings']):
            blob = struct.pack(f'<{2 * len(ring)}f', *[v for pt in ring for v in pt])
            db.execute('INSERT INTO region_polygon(region_id,ring_index,points) VALUES (?,?,?)', (rid, k, blob))
    for i, ns in neigh.items():
        for j in ns:
            db.execute('INSERT OR IGNORE INTO region_neighbour VALUES (?,?)', (regions[i]['id'], regions[j]['id']))
    for i, j, km in sea_links:
        db.execute('INSERT OR IGNORE INTO sea_link(region_id,neighbour_id,km) VALUES (?,?,?)',
                   (regions[i]['id'], regions[j]['id'], round(km)))
    db.commit()

    # ---- 8. países: industry automática por PIB per capita (√ da razão para a média mundial, 0.4..2.5),
    #         depois os ficheiros de características únicas (data/countries/*.sql) por cima
    pop_w = sum(float(adm0.get(c, {}).get('POP_EST') or 0) for c in by_country) or 1
    gdp_w = sum(float(adm0.get(c, {}).get('GDP_MD') or 0) for c in by_country) or 1
    for c, i in country_ids.items():
        p = adm0.get(c, {}); pop = float(p.get('POP_EST') or 0); gdp = float(p.get('GDP_MD') or 0)
        ratio = (gdp / pop) / (gdp_w / pop_w) if pop > 0 and gdp > 0 else 1.0
        db.execute('INSERT OR REPLACE INTO country_stat VALUES (?,?,?)', (c, 'industry', round(min(2.5, max(0.4, math.sqrt(ratio))), 2)))
    for f in sorted((HERE / 'data' / 'countries').glob('*.sql')):
        db.executescript(f.read_text(encoding='utf-8'))
    # recursos por região: depende da tabela region, por isso só no fim
    db.executescript((HERE / 'data' / 'seed_resources.sql').read_text(encoding='utf-8'))
    db.commit()

    # ---- 9. exército inicial + capitais (tools/seed_armies.py; re-semeável à parte)
    sys.path.insert(0, str(HERE / 'tools')); import seed_armies
    ntpl, ndiv = seed_armies.seed(db)
    print(f'{ntpl} templates, {ndiv} divisões iniciais')

    npts = db.execute('SELECT SUM(LENGTH(points))/8 FROM region_polygon').fetchone()[0]
    terr = db.execute('SELECT terrain,COUNT(*) FROM region GROUP BY terrain').fetchall()
    print(f'{len(regions)} regiões, {len(country_ids)} países, {npts} vértices, {sum(len(v) for v in neigh.values())//2} adjacências')
    print('terreno:', dict(terr), f'| {time.time()-t0:.0f}s')
    db.close()

    # ---- preview
    try:
        import matplotlib; matplotlib.use('Agg'); import matplotlib.pyplot as plt
        from matplotlib.patches import Polygon as MP
        tcol = dict(plain='#c8d8a0', forest='#4f7942', urban='#888888', mountain='#a08060', desert='#e0c080', tundra='#dfe8ee')
        fig, ax = plt.subplots(figsize=(24, 12)); ax.set_facecolor('#1b2a41')
        for r in regions:
            for ring in r['rings']:
                ax.add_patch(MP(ring, closed=True, fc=tcol[r['terrain']], ec='#333', lw=0.2))
        ax.set_xlim(-a.world_width/2, a.world_width/2); ax.set_ylim(a.world_width/4, -a.world_width/4); ax.set_aspect('equal'); ax.axis('off')
        fig.savefig(a.preview, dpi=80, bbox_inches='tight', facecolor='#1b2a41'); print('preview →', a.preview)
    except Exception as e:
        print('preview falhou:', e)


if __name__ == '__main__':
    main()
