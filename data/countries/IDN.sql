-- IDN (k=23) — Tentara Nasional Indonesia - Angkatan Darat (TNI-AD), ordem de batalha aproximada
-- 2024-2026. Fontes: Kostrad (Komando Cadangan Strategis, reserva estratégica de reação rápida),
-- Kopassus (forças especiais), Kodam (comandos militares regionais por ilha). Carácter: arquipélago
-- de mais de 17 mil ilhas, exército grande baseado em infantaria leve dispersa por várias regiões,
-- forças especiais de elite, indústria e mobilidade modestas.

-- ===== unit_type próprios (100+20*23 .. 119+20*23 = 560..579) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (560,'Infantaria Leve de Arquipélago','ground',0.8,25,0.7,30),
 (561,'Kopassus (Forças Especiais)','ground',1.4,40,0.8,32),
 (562,'Kostrad (Reserva Estratégica)','ground',1.7,40,1.2,38);

INSERT INTO unit_stat VALUES
 (560,'soft_atk',5), (560,'hard_atk',1), (560,'defense',18),(560,'breakthrough',9), (560,'armor',0),(560,'piercing',5), (560,'hardness',0.05),(560,'hp',22),
 (561,'soft_atk',8), (561,'hard_atk',1.5),(561,'defense',18),(561,'breakthrough',12),(561,'armor',0),(561,'piercing',6), (561,'hardness',0.1), (561,'hp',20),
 (562,'soft_atk',9), (562,'hard_atk',3), (562,'defense',24),(562,'breakthrough',17),(562,'armor',6),(562,'piercing',14),(562,'hardness',0.35),(562,'hp',27);

INSERT INTO unit_tag VALUES
 (560,'infantry'),(560,'ground'),
 (561,'infantry'),(561,'ground'),(561,'especial'),
 (562,'infantry'),(562,'ground');

-- ===== espíritos nacionais + modificadores (ids 560..579) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('IDN_defesa_arquipelagica','IDN','Defesa Arquipelágica',
   'A doutrina de "Sishankamrata" (defesa e segurança popular total) espalha o exército por milhares de ilhas: as tropas indonésias defendem melhor terreno costeiro e urbano fragmentado.'),
 ('IDN_kopassus_elite','IDN','Elite Kopassus',
   'O Kopassus é uma das forças especiais mais treinadas do Sudeste Asiático, com décadas de operações de contrainsurgência: unidades especiais atacam com maior eficácia.'),
 ('IDN_mobilizacao_popular','IDN','Mobilização Popular de Massa',
   'A grande população e a tradição de milícias regionais (Kodam) permitem mobilizar rapidamente um efetivo inicial acima da média para a dimensão da economia.'),
 ('IDN_logistica_dispersa','IDN','Logística Dispersa',
   'Manter unidades espalhadas por um arquipélago imenso encarece e atrasa a reposição de baixas: a organização recupera-se ligeiramente mais devagar.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (560,'spirit','terrain','urban', 'str_defender',NULL,      'mul',1.15,'IDN','IDN_defesa_arquipelagica'),
 (561,'spirit','river',  'true',  'str_defender',NULL,      'add',0.08,'IDN','IDN_defesa_arquipelagica'),
 (562,'spirit',NULL,NULL,         'str_attacker','especial','mul',1.20,'IDN','IDN_kopassus_elite'),
 (563,'spirit',NULL,NULL,         'command',     NULL,      'mul',1.05,'IDN','IDN_kopassus_elite'),
 (564,'spirit',NULL,NULL,         'command',     NULL,      'mul',0.92,'IDN','IDN_logistica_dispersa'),
 (565,'spirit',NULL,NULL,         'str_defender','infantry','add',0.08,'IDN','IDN_mobilizacao_popular');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('IDN','production_speed',0.85),
 ('IDN','org_regain',0.90),
 ('IDN','start_army_mult',1.15);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('IDN','República presidencialista','Presidente da República da Indonésia',
  'Defesa e segurança popular total: dispersão de forças de infantaria leve por todo o arquipélago, reserva estratégica (Kostrad) para reação rápida, forças especiais para operações de precisão.',
  'Não-alinhado',
  'A Indonésia mantém um dos maiores exércitos do Sudeste Asiático, adaptado à realidade de um arquipélago com mais de 17 mil ilhas. A doutrina de defesa popular total dispersa infantaria leve por comandos militares regionais (Kodam), enquanto o Kostrad funciona como reserva estratégica de reação rápida e o Kopassus fornece capacidade de forças especiais de referência regional. A dispersão geográfica compensa-se com número, mas encarece a logística e a reposição de baixas.');

-- ===== templates próprios (ids 1+50*23..50+50*23 = 1151..1200) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1151,'IDN','Divisão Kostrad'),
 (1152,'IDN','Grupo Kopassus'),
 (1153,'IDN','Brigada de Infantaria de Arquipélago');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1151,562,4),(1151,2,2),(1151,4,1),
 (1152,561,4),(1152,560,2),
 (1153,560,5),(1153,1,2),(1153,6,1);

-- ===== brigadas/regimentos reais nomeados (ids 1151..1200) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1154,'IDN','1ª Divisão Infanteri Kostrad','Divisão Kostrad','Jawa Barat'),
 (1155,'IDN','2ª Divisão Infanteri Kostrad','Divisão Kostrad','Jawa Timur'),
 (1156,'IDN','Kopassus Grup 1 (Serang)','Grupo Kopassus','Banten'),
 (1157,'IDN','Kopassus Grup 2 (Kandang Menjangan)','Grupo Kopassus','Jawa Timur'),
 (1158,'IDN','Kodam Iskandar Muda','Brigada de Infantaria de Arquipélago','Aceh'),
 (1159,'IDN','Kodam Bukit Barisan','Brigada de Infantaria de Arquipélago','Sumatera Utara'),
 (1160,'IDN','Kodam Jaya (Guarda de Jacarta)','Infantaria','Jakarta Raya'),
 (1161,'IDN','Kodam Diponegoro','Infantaria','Jawa Tengah'),
 (1162,'IDN','Kodam Brawijaya','Infantaria','Jawa Timur'),
 (1163,'IDN','Kodam Hasanuddin','Brigada de Infantaria de Arquipélago','Sulawesi Selatan'),
 (1164,'IDN','Kodam Cenderawasih','Brigada de Infantaria de Arquipélago','Papua'),
 (1165,'IDN','Kodam Tanjungpura','Infantaria','Kalimantan Barat'),
 (1166,'IDN','Kodam Udayana','Infantaria','Bali');

-- ===== correcção de terreno =====
UPDATE region SET terrain='forest'
 WHERE owner_id=(SELECT id FROM country WHERE tag='IDN')
   AND name IN ('Papua','Papua Barat','Kalimantan Tengah','Kalimantan Timur');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('idn_poros_maritim','IDN','Eixo Marítimo Mundial','A doutrina do Poros Maritim Dunia orienta a Indonésia para a soberania e conectividade dos seus mares.',35,NULL,1),
 ('idn_mef','IDN','Força Mínima Essencial','O programa Minimum Essential Force define o nível de capacidade que as TNI devem atingir até 2029.',42,NULL,2),
 ('idn_sishankamrata','IDN','Defesa Popular Total','A doutrina Sishankamrata mobiliza população e território num sistema de defesa semi-universal.',30,NULL,3),
 ('idn_wawasan_nusantara','IDN','Doutrina do Arquipélago','Wawasan Nusantara consagra o arquipélago como unidade geopolítica indivisível, ligando defesa e desenvolvimento regional.',40,'idn_poros_maritim',4),
 ('idn_industria_pindad','IDN','Indústria de Defesa Nacional','PT Pindad, PT PAL e PT Dirgantara Indonesia ganham investimento para reduzir a dependência de armamento estrangeiro.',49,'idn_mef',5),
 ('idn_komcad','IDN','Componente de Reserva','O Komponen Cadangan treina civis e reservistas para reforçar rapidamente as fileiras das TNI em crise.',35,'idn_sishankamrata',6),
 ('idn_natuna','IDN','Vigilância das Ilhas Natuna','Reforço da presença naval e aérea nas águas de Natuna face à pressão sobre a linha de nove traços.',42,'idn_industria_pindad',7),
 ('idn_kopassus','IDN','Modernização das Forças Especiais','Kopassus e o Corpo de Fuzileiros Navais recebem equipamento e doutrina para operações no arquipélago.',35,'idn_wawasan_nusantara',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('idn_poros_maritim','research_speed',1.08),
 ('idn_mef','production_speed',1.10),
 ('idn_sishankamrata','conscription',1.15),
 ('idn_wawasan_nusantara','industry',1.06),
 ('idn_wawasan_nusantara','org_regain',1.05),
 ('idn_industria_pindad','industry',1.10),
 ('idn_komcad','conscription',1.12),
 ('idn_natuna','org_regain',1.08),
 ('idn_kopassus','org_regain',1.06),
 ('idn_kopassus','production_speed',1.05);
