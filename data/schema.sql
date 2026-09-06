-- ================= STATIC (static.db, só leitura, no APK) =================
CREATE TABLE IF NOT EXISTS unit_type (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, category TEXT NOT NULL,
  cost REAL NOT NULL DEFAULT 1, build_days INTEGER NOT NULL DEFAULT 30,
  supply REAL NOT NULL DEFAULT 1, mobility REAL NOT NULL DEFAULT 25
);
CREATE TABLE IF NOT EXISTS unit_stat (
  unit_type_id INTEGER NOT NULL REFERENCES unit_type(id),
  stat_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (unit_type_id, stat_key)
);
CREATE TABLE IF NOT EXISTS unit_tag (
  unit_type_id INTEGER NOT NULL REFERENCES unit_type(id), tag TEXT NOT NULL,
  PRIMARY KEY (unit_type_id, tag)
);
CREATE TABLE IF NOT EXISTS modifier (
  id INTEGER PRIMARY KEY, source_kind TEXT NOT NULL,
  condition_key TEXT, condition_value TEXT,
  stat_key TEXT NOT NULL, required_tag TEXT,
  op TEXT NOT NULL CHECK (op IN ('add','mul')), value REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS terrain (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, color TEXT,
  move_cost REAL NOT NULL DEFAULT 1          -- multiplicador de dias para entrar na região
);
-- Constantes de jogo (economia, movimento, IA…). Nenhuma em código: World.Rules lê daqui.
CREATE TABLE IF NOT EXISTS rule (key TEXT PRIMARY KEY, value REAL NOT NULL, note TEXT);
CREATE TABLE IF NOT EXISTS country (
  id INTEGER PRIMARY KEY, tag TEXT UNIQUE NOT NULL, name TEXT NOT NULL, color TEXT,
  capital_region_id INTEGER                  -- onde nascem as divisões produzidas (seed_armies.py)
);
CREATE TABLE IF NOT EXISTS region (
  id INTEGER PRIMARY KEY, name TEXT NOT NULL, owner_id INTEGER REFERENCES country(id),
  terrain TEXT NOT NULL REFERENCES terrain(id), river INTEGER NOT NULL DEFAULT 0,
  population INTEGER NOT NULL DEFAULT 0, infrastructure REAL NOT NULL DEFAULT 1,
  centroid_x REAL, centroid_y REAL
);
CREATE TABLE IF NOT EXISTS region_polygon (   -- anéis exteriores, float32 x,y já projectados (Robinson, y para baixo)
  region_id INTEGER NOT NULL REFERENCES region(id), ring_index INTEGER NOT NULL, points BLOB NOT NULL,
  PRIMARY KEY (region_id, ring_index)
);
CREATE TABLE IF NOT EXISTS region_neighbour (
  region_id INTEGER NOT NULL REFERENCES region(id), neighbour_id INTEGER NOT NULL REFERENCES region(id),
  PRIMARY KEY (region_id, neighbour_id)
);
CREATE TABLE IF NOT EXISTS region_resource (
  region_id INTEGER NOT NULL REFERENCES region(id), resource TEXT NOT NULL, amount REAL NOT NULL,
  PRIMARY KEY (region_id, resource)
);
-- Exército inicial (tools/seed_armies.py): uma linha por divisão no dia 0. Só se lê quando não há save.
CREATE TABLE IF NOT EXISTS start_division (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL REFERENCES country(id),
  template_id INTEGER NOT NULL, region_id INTEGER NOT NULL REFERENCES region(id)
);
CREATE TABLE IF NOT EXISTS tech (
  id TEXT PRIMARY KEY, branch TEXT NOT NULL, name TEXT NOT NULL, cost REAL NOT NULL, requires TEXT
);
CREATE TABLE IF NOT EXISTS event_def (
  id TEXT PRIMARY KEY, title TEXT NOT NULL, condition_json TEXT NOT NULL, effect_json TEXT NOT NULL
);

-- ================= SAVE (save_N.db em user://) =================
-- Só estado mutável. Referencia ids de static.db por valor.
CREATE TABLE IF NOT EXISTS save_meta (key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE IF NOT EXISTS s_country (
  id INTEGER PRIMARY KEY, is_player INTEGER NOT NULL DEFAULT 0, money REAL NOT NULL DEFAULT 0,
  stability REAL NOT NULL DEFAULT 50
);
CREATE TABLE IF NOT EXISTS s_country_tech (country_id INTEGER, tech_id TEXT, PRIMARY KEY (country_id, tech_id));
CREATE TABLE IF NOT EXISTS s_war (a INTEGER, b INTEGER, since_day INTEGER, PRIMARY KEY (a, b));
CREATE TABLE IF NOT EXISTS s_region (
  id INTEGER PRIMARY KEY, controller_id INTEGER NOT NULL, infrastructure REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS template (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, name TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS template_unit (
  template_id INTEGER NOT NULL REFERENCES template(id), unit_type_id INTEGER NOT NULL, qty INTEGER NOT NULL,
  PRIMARY KEY (template_id, unit_type_id)
);
CREATE TABLE IF NOT EXISTS s_division (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, template_id INTEGER NOT NULL,
  region_id INTEGER NOT NULL, target_region_id INTEGER,
  hp REAL NOT NULL, org REAL NOT NULL, supply REAL NOT NULL,
  move_progress REAL NOT NULL DEFAULT 0, path TEXT      -- path: ids separados por vírgula, do próximo salto ao destino
);
CREATE INDEX IF NOT EXISTS ix_div_region ON s_division(region_id);
CREATE TABLE IF NOT EXISTS s_battle (region_id INTEGER PRIMARY KEY, attacker_country_id INTEGER NOT NULL, days INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS s_battle_division (region_id INTEGER, division_id INTEGER, side TEXT CHECK (side IN ('att','def')), PRIMARY KEY (region_id, division_id));
CREATE TABLE IF NOT EXISTS s_production_queue (
  id INTEGER PRIMARY KEY, country_id INTEGER NOT NULL, template_id INTEGER NOT NULL, progress REAL NOT NULL
);
CREATE TABLE IF NOT EXISTS s_stock (country_id INTEGER, unit_type_id INTEGER, qty INTEGER NOT NULL, PRIMARY KEY (country_id, unit_type_id));
