#!/usr/bin/env python3
"""Escreve o manifesto da actualização (wargame.json) ao lado do APK.

É o ficheiro que o jogo lê no arranque (UpdateChip.ManifestUrl) para saber se há versão nova. O tamanho
vem do APK que se está mesmo a publicar: se o manifesto subir antes do APK, ou se o APK subir cortado, o
jogo compara os bytes e recusa-se a instalar meio ficheiro — por isso o manifesto escreve-se DEPOIS de o
APK estar no sítio.

Uso:
    python3 tools/make_manifest.py build/wargame.apk "asas e esquadras com nome" > /tmp/wargame.json
    cp /tmp/wargame.json /var/www/vilarongacalado/downloads/wargame.json
"""
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
URL = 'https://vilarongacalado.com/downloads/wargame.apk'


def version():
    """A versão desta compilação, do project.godot — a mesma que o jogo instalado conhece."""
    txt = (REPO / 'project.godot').read_text(encoding='utf-8')
    m = re.search(r'^config/version="([^"]+)"', txt, re.M)
    if not m:
        sys.exit('project.godot sem config/version: o jogo não saberia que versão tem')
    return m.group(1)


def code():
    txt = (REPO / 'export_presets.cfg').read_text(encoding='utf-8')
    m = re.search(r'^version/code=(\d+)', txt, re.M)
    return int(m.group(1)) if m else 0


def name():
    txt = (REPO / 'export_presets.cfg').read_text(encoding='utf-8')
    m = re.search(r'^version/name="([^"]+)"', txt, re.M)
    return m.group(1) if m else ''


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    apk = Path(sys.argv[1])
    if not apk.is_file():
        sys.exit(f'{apk}: não existe — publica o APK primeiro')
    v, n = version(), name()
    if v != n:
        sys.exit(f'project.godot diz {v} e export_presets.cfg diz {n}: sobe-se a versão nos dois')
    notes = sys.argv[2] if len(sys.argv) > 2 else ''
    # a data sai do próprio APK (não da hora a que isto corre): assim o manifesto é o mesmo
    # ficheiro se se voltar a gerar, e a página de download tem uma data em que se pode confiar
    built = datetime.fromtimestamp(apk.stat().st_mtime, timezone.utc).isoformat(timespec='seconds')
    print(json.dumps({'version': v, 'code': code(), 'apk': URL,
                      'size': apk.stat().st_size, 'built': built, 'notes': notes},
                     ensure_ascii=False, indent=1))


if __name__ == '__main__':
    main()
