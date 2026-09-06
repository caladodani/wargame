-- Depósitos de recursos por região: determinístico a partir da tabela region (terreno + id),
-- por isso é aplicado NO FIM do import_map.py (as regiões já existem) e re-aplicável a qualquer altura.
DELETE FROM region_resource;
INSERT INTO region_resource (region_id, resource, amount)
  SELECT id, 'aco', 1 + (id * 7) % 3 FROM region WHERE terrain IN ('mountain','urban') AND id % 4 = 0;
INSERT INTO region_resource (region_id, resource, amount)
  SELECT id, 'petroleo', 1 + (id * 5) % 4 FROM region WHERE terrain IN ('desert','plain','tundra') AND id % 9 = 0;
INSERT INTO region_resource (region_id, resource, amount)
  SELECT id, 'raros', 1 FROM region WHERE terrain IN ('mountain','forest') AND id % 11 = 0;
