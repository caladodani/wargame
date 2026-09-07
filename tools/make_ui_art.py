#!/usr/bin/env python3
"""Coze o grão das chapas: um ladrilho de aço escovado, para multiplicar por cima dos painéis.

Porquê existe: `Ui.Box` é um StyleBoxFlat — uma cor lisa, a mesma do primeiro ao último pixel. O
PanelFrame já lhe punha cantoneiras e rebites, mas por baixo deles não havia chapa nenhuma: as
cantoneiras estavam agarradas a um rectângulo pintado. É a diferença entre um painel de metal e um
rectângulo cinzento — a superfície.

A imagem sai em cinzento e entra no jogo em MULTIPLICAÇÃO, nunca como cor própria. É de propósito: os
98 sítios que chamam `Ui.Box` escolhem cada um a sua cor, e um painel que trouxesse a cor da textura
atropelava-os a todos. Multiplicar por um cinzento que ronda o branco deixa a cor de cada painel como
está e só lhe acrescenta a superfície.

O que sai é só o miolo — grão escovado, mais comprido do que alto, a marca da escova no aço laminado,
entre 0.90 e 1.00 (escurece no máximo 10%). Repete-se sem costura porque o jogo o ladrilha num único
DrawTextureRect por painel.

A sombra da aresta (a chapa embutida na caixilharia) NÃO vem daqui. Era o caso para uma imagem de 9
fatias, mas 9 fatias com o miolo ladrilhado são ~88 chamadas de desenho por painel, e são 18 painéis.
Fica em vectorial no PanelGrain: quatro quadriláteros com degradê, 4 chamadas, sem pixels, nítida em
qualquer tamanho de ecrã — e nos cantos as duas sombras multiplicam-se uma pela outra, que é
exactamente o que uma chapa embutida faz.

Nada de arte de terceiros: isto é ruído com filtros, cozido aqui, e o estilo (chapa + latão + rebites)
é convenção do género, não ficheiros da Paradox.

Uso:
    tools/make_ui_art.py
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

REPO = Path(__file__).resolve().parent.parent

# Quanto o grão pode escurecer (0.10 = 10%). Acima disto deixa de ser superfície e passa a sujidade:
# lê-se como mancha no painel em vez de metal.
GRAO = 0.10


def sem_costura(n, rng, sigma_x, sigma_y):
    """Ruído filtrado que dá a volta: filtra-se em modo 'wrap', por isso o lado direito continua no
    esquerdo. Sem isto, ladrilhar deixava uma linha visível a cada repetição."""
    g = rng.normal(0, 1, (n, n)).astype(np.float32)
    g = ndimage.gaussian_filter(g, sigma=(sigma_y, sigma_x), mode='wrap')
    s = g.std()
    return g / s if s > 1e-6 else g


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=str(REPO / 'assets' / 'ui'))
    # 128 é o mínimo em que a escova ainda tem comprimento que se veja e a repetição não se apanha a
    # olho num painel de telemóvel. Potência de dois porque é assim que se ladrilha sem surpresas.
    ap.add_argument('--tile', type=int, default=128)
    a = ap.parse_args()

    n = a.tile
    rng = np.random.default_rng(20260907)

    # Escova: muito esticada na horizontal, quase nada na vertical. Por cima, manchas largas — o aço
    # não é uniforme, e sem elas o grão lê-se como televisão sem sinal.
    escova = sem_costura(n, rng, sigma_x=9.0, sigma_y=0.7)
    manchas = sem_costura(n, rng, sigma_x=13.0, sigma_y=13.0)
    campo = np.clip(0.72 * escova + 0.55 * manchas, -2.6, 2.6) / 2.6      # ≈ [-1, 1]
    grao = np.clip(1.0 - GRAO * (1.0 - campo) / 2.0, 0.0, 1.0)            # ≈ [1-GRAO, 1]

    px = (grao * 255).round().astype(np.uint8)
    out = Path(a.out); out.mkdir(parents=True, exist_ok=True)
    Image.fromarray(np.repeat(px[..., None], 3, axis=2), 'RGB').save(out / 'plate.png', optimize=True)

    # A costura tem de ser mais suave do que um passo normal entre vizinhos, senão vê-se a repetição.
    costura = float(np.abs(grao[:, 0] - grao[:, -1]).mean())
    passo = float(np.abs(np.diff(grao, axis=1)).mean())
    f = out / 'plate.png'
    print(f'grão {n}×{n} · {grao.min():.3f}–{grao.max():.3f} (média {grao.mean():.3f})'
          f' · costura {costura:.5f} contra passo normal {passo:.5f}')
    print(f'   assets/ui/plate.png  {f.stat().st_size / 1e3:.0f} KB')


if __name__ == '__main__':
    main()
