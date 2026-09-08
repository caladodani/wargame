#!/usr/bin/env python3
"""Coze a arte de interface: o grão das chapas e as teclas de metal dos botões.

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

Os BOTÕES saem noutro molde, e esse é mesmo de 9 fatias: uma tecla tem contorno, aresta de luz em cima
e sombra em baixo, e essas três coisas não podem esticar com o tamanho do botão. São dois ficheiros —
button.png e button_pressed.png — em cinzentos, para o jogo os multiplicar pela cor do papel do botão
(aço, latão, vermelho). O premido não é o normal mais escuro: troca as pontas do degradê, que é o que
o olho lê como afundado.

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


# --- Teclas de metal ------------------------------------------------------------------------------
# Lado do ladrilho do botão e as fatias do 9-slice (esquerda, direita, cima, baixo). A fatia de baixo é
# maior porque leva a sombra interior além do contorno. Estes números têm de bater certo com os do
# MetalButton.cs — são eles que dizem ao Godot o que estica e o que fica.
BOTAO = 64
FATIA = (6, 6, 6, 8)
CHANFRO = 3          # canto cortado, em pixels: uma tecla de metal não tem canto vivo

CONTORNO = 0.16      # o risco preto à volta, que separa a tecla do painel
ARESTA = 1.00        # a linha de luz por baixo do contorno de cima
TOPO, FUNDO = 0.86, 0.58   # degradê do miolo, de cima para baixo
SOMBRA = 0.38        # a sombra interior da base


def tecla(n, rng, premido=False):
    """Uma tecla em cinzentos. O valor é o que sobra da cor: o jogo multiplica isto pela cor do papel."""
    esq, dir_, cima, baixo = FATIA
    y = np.arange(n, dtype=np.float32)[:, None]

    # Degradê do miolo. Uma luz que vem de cima deixa a face clara em cima e escura em baixo; premida,
    # a face vira-se para dentro e a ordem inverte-se. Escurece 8% no premido, que uma tecla afundada
    # também apanha menos luz.
    t = np.clip((y - cima) / max(n - cima - baixo - 1, 1), 0.0, 1.0)
    face = (FUNDO + (TOPO - FUNDO) * t) * 0.92 if premido else TOPO + (FUNDO - TOPO) * t
    v = np.repeat(face, n, axis=1)

    # Grão do aço, muito mais fraco do que o das chapas: numa tecla de 48 px o grão dos painéis lia-se
    # como sujidade em vez de metal.
    campo = np.clip(0.72 * sem_costura(n, rng, 9.0, 0.7) + 0.55 * sem_costura(n, rng, 13.0, 13.0), -2.6, 2.6) / 2.6
    v *= 1.0 - 0.04 * (1.0 - campo) / 2.0

    # Relevo: luz de um lado, sombra do outro. Premida, troca — é a mesma tecla vista de dentro.
    luz, esc = (n - 2, 1) if premido else (1, n - 2)
    v[luz, :] = ARESTA
    v[esc, :] = SOMBRA
    v[esc - 1, :] = (v[esc - 1, :] + SOMBRA) / 2.0
    v[:, 1] = np.minimum(v[:, 1] * 1.12, 1.0)
    v[:, n - 2] *= 0.82

    v[0, :] = v[n - 1, :] = v[:, 0] = v[:, n - 1] = CONTORNO
    v = np.clip(v, 0.0, 1.0)

    # Canto chanfrado: transparente em vez de contorno, senão o canto fica um degrau preto.
    a = np.full((n, n), 255, np.uint8)
    ii, jj = np.mgrid[0:n, 0:n]
    for oy, ox in ((0, 0), (0, n - 1), (n - 1, 0), (n - 1, n - 1)):
        a[np.abs(ii - oy) + np.abs(jj - ox) < CHANFRO] = 0

    px = (v * 255).round().astype(np.uint8)
    return np.dstack([px, px, px, a]), v


def bandas(v):
    """Os três números que separam uma tecla de um rectângulo cinzento: a aresta de luz, o miolo e o
    contorno. Uma imagem lisa dá os três iguais — e é isso que o smoke tem de conseguir ver."""
    esq, dir_, cima, baixo = FATIA
    return v[1].mean(), v[cima:len(v) - baixo, esq:v.shape[1] - dir_].mean(), v[-1].mean()


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

    for nome, premido in (('button.png', False), ('button_pressed.png', True)):
        rgba, v = tecla(BOTAO, np.random.default_rng(20260908), premido)
        Image.fromarray(rgba, 'RGBA').save(out / nome, optimize=True)
        aresta, miolo, contorno = bandas(v)
        print(f'tecla {BOTAO}×{BOTAO} {"premida" if premido else "solta"} · fatias {FATIA}'
              f' · aresta {aresta:.2f}, miolo {miolo:.2f}, contorno {contorno:.2f}')
        print(f'   assets/ui/{nome}  {(out / nome).stat().st_size / 1e3:.0f} KB')


if __name__ == '__main__':
    main()
