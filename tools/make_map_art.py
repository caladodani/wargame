#!/usr/bin/env python3
"""Coze o fundo do mapa: oceano e relevo, em Robinson, alinhados ao milímetro com as regiões.

Porquê existe: até aqui o mapa era só polígonos de cor lisa e, fora deles, o clear-color do Godot.
Não havia oceano nenhum — a "água" era o vazio. E a terra não tinha relevo: o Brasil e os Himalaias
pintavam-se do mesmo tom chapado. É o que separa um mapa político de uma carta de guerra.

Duas imagens, com papéis diferentes:

  ocean.png   RGBA, vai POR BAIXO de tudo. Azul fundo ao largo, plataforma mais clara junto à costa
              (a distância à terra sai da própria máscara, não de tabelas), grão fino por cima. Fora
              do contorno da projecção fica transparente: a berma do mundo é a berma do mapa.

  relief.png  Cinzento, vai POR CIMA dos polígonos em multiplicação. Branco = não mexe. Só escurece,
              e só em terra — o oceano da fonte fica branco de propósito, senão a multiplicação
              sujava a água toda. Assim a cor do dono continua a ser a cor do dono e o terreno
              aparece por baixo dela, como no HOI4.

Fonte do relevo: Natural Earth "Shaded Relief" 1:50m (SR_50M), domínio público
(https://www.naturalearthdata.com/about/terms-of-use/). Nada de arte de terceiros no repositório:
o que se comete são estas duas imagens cozidas, e este ficheiro diz exactamente de onde vieram.

A máscara de terra sai do static.db, não do Natural Earth. É de propósito: o que tem de casar é o
fundo com os polígonos que o jogo desenha, e esses já passaram por simplificação e por descarte de
ilhas pequenas. Usar o vector original deixava franjas de relevo em água e água por baixo de terra.

Uso:
    tools/make_map_art.py --sr ~/ne-raster/SR_50M.tif       # descarrega-se à parte, ver README
    tools/make_map_art.py --sr ... --relief-width 6144      # se o PNG ficar grande de mais
"""
import argparse
import sqlite3
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from pyproj import Transformer
from scipy import ndimage

Image.MAX_IMAGE_PIXELS = None
HERE = Path(__file__).resolve().parent
REPO = HERE.parent

# Tem de ser o mesmo --world-width com que o import_map.py projectou as regiões, senão o fundo
# fica deslocado da terra. Se um mudar, muda o outro.
WORLD_WIDTH = 8000.0

# Valor do oceano no SR_50M: a fonte pinta a água a cinzento uniforme, não a branco. É o nível a
# que se normaliza o resto — o que for mais escuro do que isto é sombra de montanha.
SR_SEA = 206.0

DEEP = np.array([10, 24, 41], dtype=np.float32)     # mar alto
SHELF = np.array([30, 62, 90], dtype=np.float32)    # plataforma junto à costa
SHELF_KM = 700.0                                    # alcance da plataforma junto a costa a sério


def rings(db):
    """Anéis de todas as regiões, já em unidades Godot (Robinson, y para baixo)."""
    out = []
    for (blob,) in db.execute('SELECT points FROM region_polygon'):
        n = len(blob) // 8
        v = struct.unpack(f'<{2 * n}f', blob)
        out.append([(v[2 * i], v[2 * i + 1]) for i in range(n)])
    return out


def world_bounds():
    """Extremos do mundo em unidades Godot. Saem da projecção, não dos dados: assim o fundo cobre o
    mapa todo mesmo que nenhuma região chegue aos pólos."""
    tf = Transformer.from_crs('EPSG:4326', 'ESRI:54030', always_xy=True)
    xmax_m = tf.transform(180, 0)[0]
    scale = WORLD_WIDTH / (2 * xmax_m)
    ymax = abs(tf.transform(0, 90)[1]) * scale
    return WORLD_WIDTH / 2, ymax, scale


def land_mask(size, xmax, ymax, polys):
    """Terra a branco. Desenhada a partir dos mesmos anéis que o jogo desenha."""
    w, h = size
    img = Image.new('L', size, 0)
    d = ImageDraw.Draw(img)
    sx = w / (2 * xmax)
    sy = h / (2 * ymax)
    for ring in polys:
        d.polygon([((x + xmax) * sx, (y + ymax) * sy) for x, y in ring], fill=255)
    return np.asarray(img) > 127


def sample_relief(size, xmax, ymax, scale, sr_path):
    """Relevo da fonte equirectangular, amostrado por Robinson inverso pixel a pixel.

    Vai-se buscar ao contrário — para cada pixel de saída, qual o lon/lat — porque a projecção
    directa deixaria buracos: Robinson estica os pólos e um varrimento em lon/lat não cobre a saída
    de forma uniforme."""
    w, h = size
    src = np.asarray(Image.open(sr_path)).astype(np.float32)
    sh, sw = src.shape

    gx = (np.arange(w) + 0.5) / w * (2 * xmax) - xmax
    gy = (np.arange(h) + 0.5) / h * (2 * ymax) - ymax
    mx, my = np.meshgrid(gx, gy)

    inv = Transformer.from_crs('ESRI:54030', 'EPSG:4326', always_xy=True)
    lon, lat = inv.transform(mx / scale, -my / scale)          # y para baixo → metros para cima
    dentro = np.isfinite(lon) & np.isfinite(lat) & (np.abs(lat) <= 90.0001) & (np.abs(lon) <= 180.0001)

    # Fora do contorno de Robinson o inverso devolve infinito; zera-se antes da conta para não
    # transbordar o int32 (o `dentro` é que decide, não estes valores).
    lon0 = np.nan_to_num(lon, nan=0.0, posinf=0.0, neginf=0.0)
    lat0 = np.nan_to_num(lat, nan=0.0, posinf=0.0, neginf=0.0)
    cx = np.clip(((lon0 + 180) / 360 * sw).astype(np.int32), 0, sw - 1)
    cy = np.clip(((90 - lat0) / 180 * sh).astype(np.int32), 0, sh - 1)
    return src[cy, cx], dentro


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--sr', required=True, help='SR_50M.tif do Natural Earth (domínio público)')
    ap.add_argument('--db', default=str(REPO / 'data' / 'static.db'))
    ap.add_argument('--out', default=str(REPO / 'assets' / 'map'))
    # O SR_50M dá 30 px/grau e aguentava 8192, mas o que manda é a VRAM do telemóvel: 8192×4155 sem
    # compressão são 136 MB, e mesmo em ASTC ficavam 34. A 4096 são ~8,5 MB comprimidos e o relevo
    # é camada de sombra, não detalhe que se leia ao pixel — perde-se pouco e cabe.
    ap.add_argument('--relief-width', type=int, default=4096)
    ap.add_argument('--ocean-width', type=int, default=2048,
                    help='só gradientes e grão: mais do que isto é peso sem imagem')
    ap.add_argument('--strength', type=float, default=0.55,
                    help='quanto do relevo entra na multiplicação (1 = sombra crua da fonte)')
    a = ap.parse_args()

    xmax, ymax, scale = world_bounds()
    aspect = ymax / xmax
    rw, rh = a.relief_width, int(round(a.relief_width * aspect))
    ow, oh = a.ocean_width, int(round(a.ocean_width * aspect))
    print(f'mundo ±{xmax:.0f} × ±{ymax:.0f} un · relevo {rw}×{rh} · oceano {ow}×{oh}')

    db = sqlite3.connect(a.db)
    polys = rings(db)
    print(f'{len(polys)} anéis de terra do static.db')

    # ---- relevo
    terra = land_mask((rw, rh), xmax, ymax, polys)
    sr, dentro = sample_relief((rw, rh), xmax, ymax, scale, a.sr)
    # Normalizar ao nível do mar da fonte: o que for mais claro do que a água não escurece nada.
    sombra = np.clip(sr / SR_SEA, 0.0, 1.0)
    sombra = 1.0 - (1.0 - sombra) * a.strength
    rel = np.where(terra & dentro, sombra, 1.0)
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    Image.fromarray((rel * 255).round().astype(np.uint8), 'L').save(out / 'relief.png', optimize=True)

    # ---- oceano
    terra_o = land_mask((ow, oh), xmax, ymax, polys)
    _, dentro_o = sample_relief((ow, oh), xmax, ymax, scale, a.sr)
    # Plataforma continental por DENSIDADE de terra à volta, não por distância à terra mais próxima.
    # Com distância, um rochedo perdido no Pacífico abria a mesma plataforma que a costa do Brasil e
    # o oceano ficava semeado de auréolas azuis-claras à volta de cada ilhota. A densidade só sobe
    # onde há massa de terra: continente dá plataforma, penedo não dá quase nada.
    km_por_px = (2 * xmax / ow) * (40075.0 / WORLD_WIDTH)
    dens = ndimage.gaussian_filter(terra_o.astype(np.float32), sigma=SHELF_KM / km_por_px)
    t = (1.0 - np.clip(dens * 3.2, 0, 1) ** 0.8)[..., None]
    agua = SHELF * (1 - t) + DEEP * t

    # Grão fino: sem isto o degradê fica com bandas visíveis no telemóvel.
    rng = np.random.default_rng(20260907)
    agua += rng.normal(0, 2.2, agua.shape).astype(np.float32)

    rgba = np.zeros((oh, ow, 4), dtype=np.uint8)
    rgba[..., :3] = np.clip(agua, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.where(dentro_o, 255, 0)
    Image.fromarray(rgba, 'RGBA').save(out / 'ocean.png', optimize=True)

    for f in ('relief.png', 'ocean.png'):
        print(f'   assets/map/{f}  {(out / f).stat().st_size / 1e6:.1f} MB')


if __name__ == '__main__':
    main()
