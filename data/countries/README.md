# Características únicas por país

Um ficheiro por país, `<TAG>.sql` (tag ISO-3 do Natural Earth: PRT, BRA, USA…). Carregado pelo
`tools/import_map.py` depois do `seed_units.sql`, e verificado por `python3 tools/check_countries.py`.
Cada ficheiro **só fala do seu país** (`country_tag = TAG`). Tudo é dado: nenhum número em código.

Tabelas que um ficheiro pode preencher (ver `data/schema.sql`):

| Tabela | Para quê | Notas |
|---|---|---|
| `UPDATE country SET name=…` | nome em português se o NAME_PT do NE não servir | `WHERE tag='TAG'` |
| `country_info` | governo, líder, doutrina, aliança, descrição (painel do país) | texto pt-PT |
| `country_stat` | `industry` (rendimento ×), `production_speed` (×), `org_regain` (×), `start_army_mult` (exército inicial ×) | `INSERT OR REPLACE`; 1 = neutro; a `industry` automática por PIB já existe para todos |
| `national_spirit` | espíritos nacionais (HoI4): id `TAG_slug`, nome, descrição | efeitos = linhas em `modifier` |
| `modifier` | efeitos: `stat_key` ∈ str, str_attacker, str_defender, command; `op` add/mul; `country_tag='TAG'`; `spirit_id`; condição opcional (`terrain`=plain/forest/urban/mountain/desert/tundra, `river`=true); `required_tag` opcional (infantry/ground/armored/support ou tag própria) | ids ≥ 100, únicos no jogo (gama por país abaixo) |
| `unit_type` + `unit_stat` + `unit_tag` | batalhões próprios (Comandos, Infantaria de Selva…) | ids ≥ 100; 8 stats obrigatórios (soft_atk, hard_atk, defense, breakthrough, armor, piercing, hardness, hp); balancear a partir dos 1-6 do `seed_units.sql` |
| `country_template` + `country_template_unit` | templates próprios (o `seed_armies.py` cria-os para esse país) | 6-10 batalhões |
| `UPDATE region SET terrain=…` | corrigir o terreno das próprias regiões (o import é heurístico) | `WHERE owner_id=(SELECT id FROM country WHERE tag='TAG') AND name IN (…)`; terrenos: plain, forest, urban, mountain, desert, tundra |
| `country_unit` | brigadas reais nomeadas do exército inicial: nome, `template_name` (genérico ou próprio), `region_name` | região desconhecida → capital; o resto do exército é gerado |

Gamas de ids (para não colidirem entre ficheiros): país nº *k* na lista abaixo usa `unit_type` e
`modifier` de `100+20k` a `119+20k`, `country_template` e `country_unit` de `1+50k` a `50+50k`.

| k | TAG | k | TAG | k | TAG | k | TAG |
|---|---|---|---|---|---|---|---|
| 0 | PRT | 6 | GBR | 12 | ESP | 18 | ISR |
| 1 | BRA | 7 | DEU | 13 | POL | 19 | SAU |
| 2 | USA | 8 | ITA | 14 | UKR | 20 | EGY |
| 3 | CHN | 9 | JPN | 15 | TUR | 21 | AUS |
| 4 | RUS | 10 | KOR | 16 | IRN | 22 | CAN |
| 5 | IND | 11 | PRK | 17 | PAK | 23 | IDN |
| 24 | ARG | 25 | AGO | 26 | MOZ | 27 | FRA |

Proibido: DELETE, DROP, ALTER, INSERT em region/country/rule/terrain. Texto em português de Portugal (pt-PT), excepto o ficheiro BRA.sql que pode usar pt-BR nos nomes das unidades.
