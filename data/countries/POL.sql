-- POL (k=13) — Wojsko Polskie, ordem de batalha aproximada 2024-2026.
-- Fontes: Warszawska Dywizja Pancerna (Wesoła), 11. Dywizja Kawalerii Pancernej (Żagań, Abrams),
-- 16. Pomorska e 12. Dywizja Zmechanizowana, 18. Dywizja Zmechanizowana (K2/K9, Siedlce),
-- 6. Brygada Powietrznodesantowa, 25. Brygada Kawalerii Powietrznej, 21. Brygada Strzelców
-- Podhalańskich, 17. Wielkopolska e 15. Brygada Zmechanizowana, 9. Brygada Kawalerii Pancernej,
-- Wojska Obrony Terytorialnej (WOT).
-- Carácter: exército em expansão acelerada (compras massivas Abrams/K2/K9 sul-coreano-americanas),
-- doutrina de defesa territorial na flanco leste da NATO.

-- ===== unit_type próprios (100+20*13 .. 119+20*13 = 360..379) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (360,'K2 Black Panther','ground',7.5,80,2.1,42),
 (361,'Abrams M1A2','ground',7.2,78,2.2,35),
 (362,'Rosomak','ground',2.6,45,1.5,50),
 (363,'K9 Thunder','support',2.4,45,1.6,25);

INSERT INTO unit_stat VALUES
 (360,'soft_atk',14),(360,'hard_atk',19),(360,'defense',15),(360,'breakthrough',32),(360,'armor',80),(360,'piercing',68),(360,'hardness',0.94),(360,'hp',24),
 (361,'soft_atk',13),(361,'hard_atk',18),(361,'defense',14),(361,'breakthrough',30),(361,'armor',78),(361,'piercing',62),(361,'hardness',0.93),(361,'hp',22),
 (362,'soft_atk',10),(362,'hard_atk',4), (362,'defense',26),(362,'breakthrough',17),(362,'armor',16),(362,'piercing',20),(362,'hardness',0.5), (362,'hp',30),
 (363,'soft_atk',26),(363,'hard_atk',3), (363,'defense',7), (363,'breakthrough',7), (363,'armor',5), (363,'piercing',12),(363,'hardness',0.25),(363,'hp',7);

INSERT INTO unit_tag VALUES
 (360,'armored'),(360,'ground'),
 (361,'armored'),(361,'ground'),
 (362,'infantry'),(362,'armored'),(362,'ground'),
 (363,'support'),(363,'ground');

-- ===== espíritos nacionais + modificadores (ids 360..379) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('POL_rearmamento_acelerado','POL','Rearmamento Acelerado',
   'Compras massivas de Abrams, K2 e K9 e uma indústria em expansão constante elevam ligeiramente a força de combate de toda a força modernizada.'),
 ('POL_obrona_terytorialna','POL','Obrona Terytorialna',
   'As Wojska Obrony Terytorialnej treinam para a defesa do território nacional palmo a palmo: maior resiliência defensiva em solo próprio.'),
 ('POL_flanco_leste','POL','Sentinela da Flanco Leste',
   'Anos de exercícios NATO na fronteira oriental deram ao comando polaco rotinas de coordenação aliada muito treinadas.'),
 ('POL_tradicao_podhalanska','POL','Tradição Podhalańska',
   'Os Strzelcy Podhalańscy mantêm viva a tradição de combate de montanha nos Cárpatos, dando vantagem tanto a atacar como a defender em relevo acidentado.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (360,'spirit',NULL,NULL,           'str',         NULL,'mul',1.05,'POL','POL_rearmamento_acelerado'),
 (361,'spirit',NULL,NULL,           'str_defender',NULL,'mul',1.15,'POL','POL_obrona_terytorialna'),
 (362,'spirit',NULL,NULL,           'command',     NULL,'mul',1.10,'POL','POL_flanco_leste'),
 (363,'spirit','terrain','mountain','str_defender',NULL,'mul',1.15,'POL','POL_tradicao_podhalanska'),
 (364,'spirit','terrain','mountain','str_attacker',NULL,'mul',1.10,'POL','POL_tradicao_podhalanska');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('POL','production_speed',1.35),
 ('POL','org_regain',1.05),
 ('POL','start_army_mult',1.30);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('POL','República parlamentar','Chefe do Estado-Maior-General das Forças Armadas',
  'Reconstrução acelerada de uma força blindada pesada (Abrams/K2/K9), defesa territorial em profundidade da flanco leste, plena integração de comando com a OTAN.',
  'NATO',
  'A Polónia atravessa a maior expansão militar da sua história recente, com encomendas maciças de blindados sul-coreanos e norte-americanos a reforçar divisões que já contavam com tradição de cavalaria blindada. A doutrina assenta na defesa em profundidade da fronteira oriental, apoiada por uma Obrona Terytorialna cada vez mais robusta. A indústria de defesa nacional cresce em paralelo com a produção sob licença de sistemas estrangeiros.');

-- ===== templates próprios (ids 651..700) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (651,'POL','Dywizja Pancerna'),
 (652,'POL','Brygada Zmechanizowana K9'),
 (653,'POL','Brygada Górska Podhalańska');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (651,360,3),(651,361,3),(651,362,2),
 (652,362,4),(652,1,2),(652,363,2),
 (653,1,4),(653,6,2),(653,4,1);

-- ===== brigadas/divisões reais nomeadas (ids 654..700) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (654,'POL','Warszawska Dywizja Pancerna im. Tadeusza Kościuszki','Dywizja Pancerna','Masovian'),
 (655,'POL','11. Dywizja Kawalerii Pancernej','Dywizja Pancerna','Lubusz'),
 (656,'POL','16. Pomorska Dywizja Zmechanizowana','Brygada Zmechanizowana K9','Warmian-Masurian'),
 (657,'POL','12. Dywizja Zmechanizowana','Brygada Zmechanizowana K9','West Pomeranian'),
 (658,'POL','18. Dywizja Zmechanizowana','Mecanizada','Masovian'),
 (659,'POL','6. Brygada Powietrznodesantowa','Infantaria','Lesser Poland'),
 (660,'POL','25. Brygada Kawalerii Powietrznej','Infantaria','Łódź'),
 (661,'POL','21. Brygada Strzelców Podhalańskich','Brygada Górska Podhalańska','Subcarpathian'),
 (662,'POL','17. Wielkopolska Brygada Zmechanizowana','Mecanizada','Lubusz'),
 (663,'POL','15. Brygada Zmechanizowana','Mecanizada','Warmian-Masurian'),
 (664,'POL','9. Brygada Kawalerii Pancernej','Dywizja Pancerna','Warmian-Masurian'),
 (665,'POL','1. Brygada Obrony Terytorialnej Mazowiecka','Infantaria AT','Masovian');
