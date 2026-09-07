# WarGame — estratégia de guerra moderna (Android, Godot 4 + C#)

Referência de design: Hearts of Iron 4 (divisões que demoram dias a atravessar províncias, batalhas de vários dias, produção lenta, abastecimento, investigação, espíritos nacionais, IA com objectivos de guerra).

## Estrutura
```
wargame/
├─ project.godot              # projeto Godot (apresentação)
├─ WarGame.csproj             # assembly Godot; referencia WarGame.Core
├─ WarGame.sln
├─ WarGame.Core/              # SIMULAÇÃO PURA — zero dependências Godot, testável
│  ├─ Model/      World, Region, Country, Division, Clock, Tech
│  ├─ Stats/      StatBlock, ModifierEngine, DivisionStatCache
│  ├─ Systems/    ISystem: Supply, Economy, Production, Research, Movement, Combat, Recovery, AI
│  ├─ Commands/   ICommand + comandos do jogador e da IA (validados antes de mutar)
│  ├─ Events/     EventBus + eventos de domínio
│  └─ Data/       IDatabase, repositórios (contratos + impl. SQL genérica)
├─ WarGame.Core.Tests/        # xUnit — corre no PC sem Godot (TestWorld carrega schema + seeds reais)
├─ src/                       # C# que depende de Godot
│  ├─ Data/       GdSqliteDatabase (godot-sqlite GDExtension)
│  └─ Presentation/ Game (autoload), MapView, Hud, RegionPanel, CountryPanel, ProductionPanel
├─ scenes/Main.tscn
├─ tools/
│  ├─ import_map.py           # Natural Earth → data/static.db (regiões, países, seeds, exércitos)
│  ├─ seed_armies.py          # exército inicial por país (templates + divisões nomeadas/geradas)
│  ├─ check_countries.py      # valida data/countries/*.sql (ids, gamas, referências, tags)
│  ├─ make_map_art.py         # coze assets/map/{ocean,relief}.png (fundo do mapa, ver "Mapa")
│  └─ combat_sim.py           # calibração do combate (CombatSystem é port directo)
└─ data/
   ├─ schema.sql              # static.db + estrutura de save (s_*)
   ├─ seed_units.sql          # unidades base, terrenos, modificadores, regras (rule)
   ├─ seed_tech.sql           # árvore de 22 tecnologias + efeitos + techs iniciais
   ├─ seed_world.sql          # regras da IA/mundo, guerras iniciais, agressividade
   ├─ countries/<TAG>.sql     # características únicas por país (28 países; README.md com as gamas de ids)
   └─ static.db               # GERADO — nunca editar à mão
```

## Regras de arquitetura
1. `WarGame.Core` nunca referencia `Godot`. Apresentação só lê estado e envia `ICommand`.
2. Cada mecânica é um `ISystem` registado em `World.Systems`; ordem definida em `Game.cs`. Não fundir sistemas.
3. Nenhum tipo de unidade/terreno/tech/regra existe em código — só linhas em SQLite. `StatBlock` é `string → float`; constantes vêm da tabela `rule` (`World.Rule("chave", fallback)`).
4. Novas regras = linhas em `modifier` (condições: terrain, river, country, tech:<id>). Novo sistema = nova classe `ISystem`, sem tocar nas outras.
5. Tick corre em `Task.Run`; UI lê snapshot imutável no fim do tick.
6. Erros de compilação corrigem-se com a alteração mínima; teste de calibração do combate a falhar por pouco → ajustar o intervalo do teste, nunca os números.

## Setup (tudo na home, sem root)
Godot 4.3-stable .NET em `~/.local/bin/godot`, .NET 8 em `~/.dotnet`, JDK 17 em `~/jdk`, Android SDK em `~/android-sdk`.
```
export DOTNET_ROOT=$HOME/.dotnet JAVA_HOME=$HOME/jdk/jdk-17.0.20.1+1; export PATH=$HOME/.local/bin:$HOME/.dotnet:$JAVA_HOME/bin:$PATH
dotnet build WarGame.sln -nologo -v q && dotnet test WarGame.Core.Tests -nologo -v q
godot --headless --path ~/wargame --import
godot --headless --path ~/wargame --export-debug Android build/wargame.apk
~/android-sdk/build-tools/34.0.0/apksigner verify build/wargame.apk
```
Smoke headless (escolhe PRT, joga 6 dias, grava): `XDG_DATA_HOME=/tmp/x timeout 240 godot --headless --path ~/wargame -- --smoke`.

## Dados
- Regenerar `static.db` (≈15 s): `~/.venvs/wargame-tools/bin/python tools/import_map.py --ne ~/ne --out data/static.db` — corre schema + seed_units + seed_tech + seed_world + `data/countries/*.sql` (ordenados) + `seed_armies.seed()`.
- Ficheiro de país (`data/countries/<TAG>.sql`): unit_type próprios, espíritos nacionais (`national_spirit` + `modifier` com `country_tag`/`spirit_id`), `country_stat` (industry, production_speed, org_regain, start_army_mult, research_speed, move_speed, aggression), `country_info` (painel), `country_template`/`country_unit` (brigadas reais nomeadas e colocadas por nome de região), `UPDATE region SET terrain` só do próprio país. Validar sempre: `python3 tools/check_countries.py data/countries/*.sql` (sai 1 com erros).
- `country_stat.industry` é calculado do PIB per capita no import (clamp 0,4–2,5); os ficheiros de país podem sobrepor.

## Mapa
**2988 regiões, 247 países, ~65k vértices, 7026 adjacências**, projecção Robinson (8000 unidades de largura), gerado do Natural Earth 10m/50m (mirror `nvkelso/natural-earth-vector`, em `~/ne`). Orçamento por país ∝ √(área × população), tecto = admin-1; Portugal e Brasil com todos os distritos/estados (`FULL_DETAIL`). Terreno por cobertura de polígonos físicos (montanha/deserto/tundra), cintura de floresta por latitude, urbano por densidade; população por lugares povoados (10m) + resto por área/cidade.

**Fundo do mapa** (`assets/map/`, cozido por `tools/make_map_art.py`): `ocean.png` por baixo dos
polígonos (azul de mar alto, plataforma mais clara junto às costas com massa de terra, transparente
fora do contorno de Robinson) e `relief.png` por cima deles em multiplicação (branco não mexe, sombra
escurece — a cor do dono continua a ler-se e a serra aparece por baixo). O relevo vem do **Natural
Earth "Shaded Relief" 1:50m (SR_50M), domínio público**; não está no repositório, descarrega-se à
parte:

```
curl -fL -o /tmp/SR_50M.zip https://naciscdn.org/naturalearth/50m/raster/SR_50M.zip && unzip -o /tmp/SR_50M.zip -d /tmp
~/.venvs/wargame-tools/bin/python tools/make_map_art.py --sr /tmp/SR_50M.tif
godot --headless --path . --import        # regenera os .ctex
```

Refazer só é preciso se o `--world-width` do `import_map.py` mudar ou se as regiões mudarem de forma:
a máscara de terra sai do `static.db`, não do Natural Earth, para o fundo casar com o que o jogo
desenha. Os `.import` das duas imagens estão em `compress/mode=2` (VRAM) e `mipmaps/generate=true`
de propósito: sem compressão o relevo eram 136 MB de VRAM e sem mipmaps o mapa fervilha de longe.
Limitações (tudo em dados): sem regiões marítimas (naval), floresta heurística.

## Próximos passos (HoI4)
- Facções/alianças como mecânica (NATO etc. só existe como texto), capitulação/paz, botão voltar Android.
- Stock de equipamento, doutrinas, eventos históricos, naval/aéreo.
