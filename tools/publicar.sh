#!/usr/bin/env bash
# Publica uma versão do WarGame: portão de qualidade, exporta o APK, põe-no em
# /var/www/vilarongacalado/downloads/ e só DEPOIS escreve o manifesto ao lado.
#
# A ordem não é gosto: a página https://vilarongacalado.com/wargame/ não tem versão embebida,
# lê o wargame.json ao vivo, e o jogo instalado compara o "size" do manifesto com os bytes que
# descarrega — recusa-se a instalar meio ficheiro. Manifesto à frente do APK = jogadores a
# apanhar erro; APK sem manifesto = ninguém sabe que há versão nova, mas nada parte.
# Por isso: APK primeiro (e por cima de si próprio de uma vez só, com mv), manifesto a seguir.
#
# O APK é assinado com o keystore de debug local (~/.android/debug.keystore). O CI não publica
# nada de propósito: com outro keystore a assinatura mudava e os jogadores deixavam de conseguir
# instalar por cima — perdiam os saves. O GitHub Actions é só portão de qualidade.
#
# Uso:
#   tools/publicar.sh "menu inicial novo"      # as notas aparecem na página de download
#   tools/publicar.sh "..." --sujo             # deixa passar árvore de git com trabalho por commitar
#   tools/publicar.sh "..." --forcar           # deixa republicar a versão que já lá está

set -euo pipefail

REPO=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
DEST_DIR=/var/www/vilarongacalado/downloads
DEST_APK=$DEST_DIR/wargame.apk
DEST_JSON=$DEST_DIR/wargame.json
URL_APK=https://vilarongacalado.com/downloads/wargame.apk
URL_JSON=https://vilarongacalado.com/downloads/wargame.json
URL_PAGINA=https://vilarongacalado.com/wargame/
BUILD_TOOLS=$HOME/android-sdk/build-tools/34.0.0
APK_LOCAL=$REPO/build/wargame.apk

# Ficheiros que entram no APK. O resto (tools/, .github/, README.md, deploy.sh) pode estar por
# commitar sem mentir a ninguém: não muda um único byte do que o jogador instala.
DO_JOGO=(project.godot export_presets.cfg icon.png icon.png.import WarGame.csproj WarGame.sln
         src scenes assets addons data WarGame.Core WarGame.Core.Tests)

# Impressão digital do certificado com que TODOS os APKs publicados até hoje foram assinados
# (~/.android/debug.keystore). O Android só deixa instalar por cima com a mesma assinatura: publicar
# um APK assinado por outra chave obrigaria cada jogador a desinstalar primeiro — e a desinstalação
# leva os saves. Um `apksigner verify` sem isto só diz que ESTÁ assinado, não POR QUEM.
ASSINATURA_ESPERADA=0ec8fcb6dadbd83eb2c80d729283246e935412acbb0ff8656c43bd08f2eeaa8d

PASSO="arranque"
TMP=""
PUBLICADO=nada        # nada | apk | apk+manifesto — o que ficou mesmo lá fora

trap 'rc=$?; [ -n "$TMP" ] && rm -rf "$TMP";
      rm -f "$DEST_APK.new" "$DEST_JSON.new";
      if [ "$rc" -ne 0 ]; then
        printf "\n== publicar.sh parou no passo: %s (código %s)\n" "$PASSO" "$rc" >&2
        case "$PUBLICADO" in
          nada) printf "   nada foi publicado: lá fora está tudo como estava.\n" >&2 ;;
          apk)  printf "   ATENÇÃO: o APK NOVO já está no ar mas o manifesto ainda é o velho.\n" >&2
                printf "   A página anuncia a versão velha e o tamanho não bate certo: o jogo recusa a\n" >&2
                printf "   instalação até o manifesto subir. Corre outra vez com --forcar.\n" >&2 ;;
          *)    printf "   o APK e o manifesto já estavam publicados quando isto falhou.\n" >&2 ;;
        esac
      fi' EXIT

passo()  { PASSO="$1"; printf "\n== %s\n" "$1"; }
aviso()  { printf "   aviso: %s\n" "$*" >&2; }
morrer() { printf "   ERRO: %s\n" "$*" >&2; exit 1; }

uso() {
    cat >&2 <<'FIM'
uso: tools/publicar.sh "notas da versão" [--sujo] [--forcar]

  "notas"    obrigatórias — são o que a página de download mostra ao jogador
  --sujo     publica mesmo com trabalho por commitar (só para provar um build local)
  --forcar   publica mesmo que a versão já esteja no servidor
FIM
}

# ---------------------------------------------------------------- 0. argumentos
NOTAS=""; SUJO=0; FORCAR=0
while [ $# -gt 0 ]; do
    case "$1" in
        --sujo)            SUJO=1 ;;
        --forcar|--forçar) FORCAR=1 ;;
        -h|--help)         uso; exit 0 ;;
        -*)                uso; morrer "opção desconhecida: $1" ;;
        *)                 if [ -z "$NOTAS" ]; then NOTAS="$1"; else uso; morrer "notas a mais: $1 (são um argumento só, entre aspas)"; fi ;;
    esac
    shift
done

# Sem notas não se começa sequer: o manifesto sem notas é pior para o jogador do que não publicar,
# porque a página fica com a chapa da versão nova e nada a dizer o que mudou.
[ -n "$NOTAS" ] || { uso; morrer "faltam as notas da versão"; }

# ---------------------------------------------------------------- 1. toolchain
passo "Ambiente"
export DOTNET_ROOT=$HOME/.dotnet JAVA_HOME=$HOME/jdk/jdk-17.0.20.1+1
export PATH=$HOME/.local/bin:$HOME/.dotnet:$JAVA_HOME/bin:$PATH
cd "$REPO"

command -v dotnet  >/dev/null || morrer "dotnet não está no PATH"
command -v godot   >/dev/null || morrer "godot não está no PATH (~/.local/bin/godot)"
command -v python3 >/dev/null || morrer "python3 não está no PATH"
[ -x "$BUILD_TOOLS/apksigner" ] || morrer "$BUILD_TOOLS/apksigner não existe"

# A build normal do Godot não corre este projecto (é C#): mais vale saber agora do que no export.
VER_GODOT=$(godot --version 2>/dev/null | tail -1)
case "$VER_GODOT" in
    *mono*) : ;;
    *) morrer "godot '$VER_GODOT' não é a build mono — o projecto é C# e não arranca nela" ;;
esac
printf "   godot %s\n" "$VER_GODOT"

# ---------------------------------------------------------------- 2. árvore limpa
passo "Árvore de git"
git rev-parse --git-dir >/dev/null 2>&1 || morrer "$REPO não é um repositório git"
SUJIDADE=$(git status --porcelain -- "${DO_JOGO[@]}")
if [ -n "$SUJIDADE" ]; then
    if [ "$SUJO" -eq 1 ]; then
        aviso "há trabalho por commitar que entra no APK (--sujo dado, segue-se na mesma):"
        printf '%s\n' "$SUJIDADE" >&2
    else
        printf '%s\n' "$SUJIDADE" >&2
        morrer "o que se publica tem de ser o que está no git — commita, ou usa --sujo para um build de prova"
    fi
fi
COMMIT=$(git rev-parse --short HEAD)
printf "   HEAD %s\n" "$COMMIT"
# Não trava nada: só avisa que o servidor vai ficar à frente do GitHub.
if ACIMA=$(git rev-parse --abbrev-ref '@{u}' 2>/dev/null); then
    [ "$(git rev-parse HEAD)" = "$(git rev-parse "$ACIMA")" ] || aviso "HEAD não está em $ACIMA — falta um push (o CI também não corre sem ele)"
fi

# ---------------------------------------------------------------- 3. versão
passo "Versão"
VER_PROJ=$(sed -n 's/^config\/version="\(.*\)"$/\1/p' project.godot | head -1)
VER_EXP=$(sed -n 's/^version\/name="\(.*\)"$/\1/p' export_presets.cfg | head -1)
CODE_EXP=$(sed -n 's/^version\/code=\([0-9]*\)$/\1/p' export_presets.cfg | head -1)
[ -n "$VER_PROJ" ] || morrer "project.godot sem config/version"
[ -n "$VER_EXP" ]  || morrer "export_presets.cfg sem version/name"
[ -n "$CODE_EXP" ] || morrer "export_presets.cfg sem version/code — é o número que o Android compara para deixar instalar por cima"
if [ "$VER_PROJ" != "$VER_EXP" ]; then
    morrer "project.godot diz $VER_PROJ e export_presets.cfg diz $VER_EXP — sobe-se a versão nos dois (config/version e version/name, e version/code no preset)"
fi
VERSAO=$VER_PROJ
printf "   %s (code %s)\n" "$VERSAO" "${CODE_EXP:-?}"

# ---------------------------------------------------------------- 4. já publicada?
passo "Versão já publicada"
[ -d "$DEST_DIR" ] || morrer "$DEST_DIR não existe — é a pasta que o nginx serve"
[ -w "$DEST_DIR" ] || morrer "sem permissão de escrita em $DEST_DIR"
if [ -f "$DEST_JSON" ]; then
    VER_PUB=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("version",""))' "$DEST_JSON" 2>/dev/null || true)
    CODE_PUB=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1])).get("code",0))' "$DEST_JSON" 2>/dev/null || true)
    printf "   lá fora: %s (code %s)\n" "${VER_PUB:-?}" "${CODE_PUB:-?}"
    if [ "$VER_PUB" = "$VERSAO" ]; then
        if [ "$FORCAR" -eq 1 ]; then
            aviso "a versão $VERSAO já está publicada — vai por cima (--forcar dado)"
        else
            morrer "a versão $VERSAO já está publicada: sobe a versão (project.godot + export_presets.cfg) ou usa --forcar se é mesmo para substituir"
        fi
    fi
    # O Android recusa instalar por cima com versionCode igual ou mais baixo: publicar assim deixa
    # toda a gente que já tem o jogo presa na versão velha. Não é aviso, é motivo para parar.
    if [ -n "${CODE_EXP:-}" ] && [ -n "${CODE_PUB:-}" ] && [ "$CODE_EXP" -le "$CODE_PUB" ]; then
        if [ "$FORCAR" -eq 1 ]; then
            aviso "version/code $CODE_EXP não é maior que o publicado ($CODE_PUB) — segue-se (--forcar dado), mas quem já tem o jogo não vai conseguir actualizar"
        else
            morrer "version/code $CODE_EXP não é maior que o publicado ($CODE_PUB) — sobe version/code em export_presets.cfg (o Android recusa instalar por cima), ou --forcar se sabes o que estás a fazer"
        fi
    fi
else
    aviso "$DEST_JSON não existe — primeira publicação?"
fi

# ---------------------------------------------------------------- 5. portão de qualidade
TMP=$(mktemp -d)

passo "Compilação"
dotnet build WarGame.sln -nologo -v q

passo "Testes"
dotnet test WarGame.Core.Tests -nologo -v q

passo "Dados dos países"
python3 tools/check_countries.py | tee "$TMP/paises.log"
grep -qx 'OK' "$TMP/paises.log" || morrer "check_countries.py não acabou em OK"

passo "Smoke headless"
# XDG_DATA_HOME próprio: o smoke grava um save e não se lhe pede que suje os do utilizador.
# (No export é ao contrário — ver o passo seguinte.)
mkdir -p "$TMP/xdg"
RC_SMOKE=0
XDG_DATA_HOME="$TMP/xdg" timeout 300 godot --headless --path "$REPO" -- --smoke 2>&1 | tee "$TMP/smoke.log" || RC_SMOKE=$?
[ "$RC_SMOKE" -eq 0 ] || morrer "o smoke saiu a $RC_SMOKE (124 = passou dos 300 s)"
mkdir -p "$REPO/build"; cp "$TMP/smoke.log" "$REPO/build/smoke.log"   # o $TMP morre no trap; o log fica
LINHAS=$(grep -c '^smoke: ' "$TMP/smoke.log" || true)
# Um smoke que sai a 0 sem imprimir nada é um smoke que não correu — o jogo pode nem ter arrancado.
[ "${LINHAS:-0}" -ge 6 ] || morrer "o smoke só imprimiu ${LINHAS:-0} linhas 'smoke: ' (esperam-se 6 ou mais) — arrancou e morreu a meio; ver $REPO/build/smoke.log"
# E a última linha é a prova de que chegou ao fim e gravou. Sem ela, o jogo rebentou à saída — foi
# assim que o arranque limpo (o do CI e o de quem instala pela primeira vez) esteve partido sem se ver.
grep -q '^smoke: dia .* guardado, a sair' "$TMP/smoke.log" \
    || morrer "o smoke não chegou ao fim (falta a linha 'guardado, a sair') — ver $REPO/build/smoke.log"
printf "   %s linhas de smoke, chegou ao fim\n" "$LINHAS"
if grep -qE '(SCRIPT|USER) ERROR' "$TMP/smoke.log"; then
    grep -nE '(SCRIPT|USER) ERROR' "$TMP/smoke.log" | head -5 >&2
    morrer "o smoke deixou erros de script no log — ver $REPO/build/smoke.log (--forcar não salta este)"
fi

# ---------------------------------------------------------------- 6. export
passo "Export do APK"
# ARMADILHA: aqui NÃO se mexe no XDG_DATA_HOME. Os templates de exportação e o keystore de debug
# vivem no ~/.local/share real; com override o export morre com "No export template found".
rm -f "$APK_LOCAL"
mkdir -p "$(dirname "$APK_LOCAL")"
godot --headless --path "$REPO" --export-debug Android "$APK_LOCAL"
[ -s "$APK_LOCAL" ] || morrer "o export não deixou $APK_LOCAL (ou deixou-o vazio)"
TAMANHO=$(stat -c %s "$APK_LOCAL")
printf "   %s (%s bytes, %s MiB)\n" "$APK_LOCAL" "$TAMANHO" "$((TAMANHO / 1048576))"

# ---------------------------------------------------------------- 7. assinatura e versão do APK
passo "Assinatura do APK"
"$BUILD_TOOLS/apksigner" verify --print-certs "$APK_LOCAL" | tee "$TMP/certs.log"
ASSINATURA=$(sed -n 's/^Signer #1 certificate SHA-256 digest: *//p' "$TMP/certs.log" | head -1)
[ -n "$ASSINATURA" ] || morrer "o apksigner não deu a impressão digital do signatário"
if [ "$ASSINATURA" != "$ASSINATURA_ESPERADA" ]; then
    morrer "o APK está assinado por outra chave.
   esperada: $ASSINATURA_ESPERADA
   obtida:   $ASSINATURA
   O ~/.android/debug.keystore deve ter sido substituído. Publicar assim obrigaria cada jogador a
   desinstalar o jogo para instalar este — e a desinstalação leva os saves. Repõe o keystore antigo."
fi
printf "   assinado pela chave de sempre (%s…)\n" "${ASSINATURA:0:16}"

passo "versionName dentro do APK"
BADGING=""
if [ -x "$BUILD_TOOLS/aapt2" ]; then
    BADGING=$("$BUILD_TOOLS/aapt2" dump badging "$APK_LOCAL" 2>/dev/null || true)
elif [ -x "$BUILD_TOOLS/aapt" ]; then
    BADGING=$("$BUILD_TOOLS/aapt" dump badging "$APK_LOCAL" 2>/dev/null || true)
fi
if [ -z "$BADGING" ]; then
    aviso "sem aapt/aapt2 em $BUILD_TOOLS (ou não leram o APK) — conferência saltada"
else
    VER_APK=$(printf '%s\n' "$BADGING" | sed -n "s/.*versionName='\([^']*\)'.*/\1/p" | head -1)
    if [ -z "$VER_APK" ]; then
        aviso "o aapt não deu versionName — conferência saltada"
    elif [ "$VER_APK" != "$VERSAO" ]; then
        morrer "o APK diz $VER_APK e o repo diz $VERSAO — o export usou um preset velho"
    else
        printf "   %s, bate certo\n" "$VER_APK"
    fi
fi

# ---------------------------------------------------------------- 8. preparar os dois ao lado
passo "Preparar APK e manifesto"
# O nginx serve estes ficheiros directamente: uma cópia a meio seria descarregada por alguém, e um
# manifesto que anuncie um tamanho diferente do APK servido faz o jogo recusar a instalação.
# Por isso prepara-se TUDO ao lado (.new) e só no fim se trocam os dois, o APK primeiro. Assim a
# janela em que o par está desirmanado é o intervalo entre dois mv, e não os segundos que o
# make_manifest.py demorasse a correr com o APK novo já no ar.
LIVRE=$(df -B1 --output=avail "$DEST_DIR" | tail -1)
[ "$LIVRE" -gt $((TAMANHO + 10485760)) ] || morrer "só há $LIVRE bytes livres em $DEST_DIR para um APK de $TAMANHO — a cópia ficava a meio"
cp "$APK_LOCAL" "$DEST_APK.new"
chmod 644 "$DEST_APK.new"

# O "size" tem de ser o dos bytes que vão ficar publicados: o .new é cópia exacta do que o mv põe
# no lugar, por isso serve — e serve ANTES de mexer no que está no ar.
python3 tools/make_manifest.py "$DEST_APK.new" "$NOTAS" > "$TMP/wargame.json"
python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert d["version"]==sys.argv[2], d["version"]; assert d["size"]==int(sys.argv[3]), d["size"]' \
    "$TMP/wargame.json" "$VERSAO" "$TAMANHO" || morrer "o manifesto gerado não bate certo com a versão $VERSAO / $TAMANHO bytes"
cp "$TMP/wargame.json" "$DEST_JSON.new"
chmod 644 "$DEST_JSON.new"

# ---------------------------------------------------------------- 9. trocar (o par vai junto)
passo "Publicar"
mv -f "$DEST_APK.new" "$DEST_APK";  PUBLICADO=apk
mv -f "$DEST_JSON.new" "$DEST_JSON"; PUBLICADO=apk+manifesto
printf "   %s\n   %s\n" "$DEST_APK" "$DEST_JSON"
cat "$DEST_JSON"

# ---------------------------------------------------------------- 10. conferir lá de fora
passo "Conferir pela Internet"
TAM_MAN=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["size"])' "$DEST_JSON")
if ! command -v curl >/dev/null; then
    aviso "sem curl — a conferência final fica por fazer; abre $URL_PAGINA à mão"
else
    curl -fsSL -H 'Cache-Control: no-cache' "$URL_JSON" -o "$TMP/publicado.json" \
        || morrer "$URL_JSON não respondeu — o ficheiro está no sítio mas o nginx não o serve"
    VER_HTTP=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' "$TMP/publicado.json")
    [ "$VER_HTTP" = "$VERSAO" ] || morrer "lá fora o manifesto ainda diz $VER_HTTP e não $VERSAO (cache pelo meio?)"
    # `|| TAM_HTTP=""`: com pipefail um curl falhado matava o script já na atribuição e o morrer
    # explicativo da linha seguinte nunca chegava a aparecer.
    TAM_HTTP=$(curl -fsSLI -H 'Cache-Control: no-cache' "$URL_APK" | tr -d '\r' | awk 'tolower($1)=="content-length:"{n=$2} END{print n}') || TAM_HTTP=""
    [ -n "$TAM_HTTP" ] || morrer "$URL_APK não deu Content-Length — não dá para garantir que o APK está inteiro"
    [ "$TAM_HTTP" = "$TAM_MAN" ] || morrer "o APK servido tem $TAM_HTTP bytes e o manifesto diz $TAM_MAN — o jogo recusaria a instalação"
    printf "   manifesto e APK batem certo (%s bytes)\n" "$TAM_HTTP"
fi

PASSO="fim"
printf "\n== Publicado\n"
printf "   versão  %s (code %s, commit %s%s)\n" "$VERSAO" "${CODE_EXP:-?}" "$COMMIT" \
       "$([ "$SUJO" -eq 1 ] && printf ' + trabalho por commitar — NÃO se reproduz a partir do git' || true)"
printf "   tamanho %s bytes (%s MiB)\n" "$TAM_MAN" "$((TAM_MAN / 1048576))"
printf "   notas   %s\n" "$NOTAS"
printf "   página  %s\n" "$URL_PAGINA"
