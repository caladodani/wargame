# WarGame — estratégia de guerra moderna (Android, Godot 4 + C#)

## Estrutura
```
wargame/
├─ project.godot              # projeto Godot (apresentação)
├─ WarGame.csproj             # assembly Godot; referencia WarGame.Core
├─ WarGame.sln
├─ WarGame.Core/              # SIMULAÇÃO PURA — zero dependências Godot, testável
│  ├─ Model/      World, Region, Country, Division, Clock
│  ├─ Stats/      StatBlock, ModifierEngine, DivisionStatCache
│  ├─ Systems/    ISystem + CombatSystem, SupplySystem, EconomySystem…
│  ├─ Commands/   ICommand + comandos do jogador (validados antes de mutar)
│  ├─ Events/     EventBus + eventos de domínio
│  └─ Data/       IDatabase, repositórios (contratos + impl. SQL genérica)
├─ WarGame.Core.Tests/        # xUnit — corre no PC sem Godot
├─ src/                       # C# que depende de Godot
│  ├─ Data/       GdSqliteDatabase (godot-sqlite GDExtension)
│  └─ Presentation/ Game (autoload), MapView, Hud
├─ scenes/Main.tscn
└─ data/
   ├─ schema.sql              # static.db + estrutura de save
   └─ seed_units.sql          # números calibrados (combat_sim.py)
```

## Regras de arquitetura
1. `WarGame.Core` nunca referencia `Godot`. Apresentação só lê estado e envia `ICommand`.
2. Cada mecânica é um `ISystem` registado em `World.Systems`; ordem definida em `Game.cs`.
3. Nenhum tipo de unidade/terreno/tech existe em código — só linhas em SQLite. `StatBlock` é `string → float`.
4. Novas regras = linhas em `modifier`. Novo sistema = nova classe `ISystem`, sem tocar nas outras.
5. Tick corre em `Task.Run`; UI lê snapshot imutável no fim do tick.

## Setup
1. Godot 4.3+ **.NET** + .NET SDK 8.
2. Addon `godot-sqlite` (AssetLib → "Godot SQLite") em `addons/godot-sqlite/`. Inclui binários Android.
3. `dotnet build WarGame.sln` — ou abrir no editor Godot.
4. Gerar `static.db`: `sqlite3 data/static.db < data/schema.sql && sqlite3 data/static.db < data/seed_units.sql`.
5. Export Android: Project → Export → Android; em .NET marcar arm64-v8a. Requer Android SDK + JDK 17 e keystore de debug.
6. Testes: `dotnet test WarGame.Core.Tests`.

## Mapa
`data/static.db` já inclui o mapa real: **988 regiões, 247 países, ~49k vértices, 2160 adjacências**, projecção Robinson (8000 unidades de largura), gerado por `tools/import_map.py` a partir do Natural Earth (mirror GitHub `nvkelso/natural-earth-vector`).
Para regenerar: `pip install shapely pyproj numpy scikit-learn matplotlib` → `python3 tools/import_map.py --ne <pasta com os geojson> --target 1000`.
Limitações actuais (TODO, tudo em dados): população distribuída ∝ área dentro do país (usar raster GPW/WorldPop);
floresta por heurística de latitude; sem regiões marítimas (naval).

## Próximos passos
- `EconomySystem`, `ProductionSystem`, `SupplySystem` reais.
- IA (`AiSystem`) — máquina de estados por país.
