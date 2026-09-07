-- MOZ (k=26) — Forças Armadas de Defesa de Moçambique (FADM), ordem de batalha aproximada 2024-2026.
-- Fontes: insurgência jihadista em Cabo Delgado desde 2017 (apoio de Ruanda e SADC), Força de
-- Intervenção Rápida (FIR), batalhões de infantaria por província, legado da guerrilha de
-- independência da FRELIMO.
-- Carácter: exército pequeno e subfinanciado, país lusófono, centrado na contra-insurgência
-- territorial em Cabo Delgado mais do que em guerra convencional.

-- ===== unit_type próprios (100+20*26 .. 119+20*26 = 620..639) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (620,'Comando de Contra-Insurgência','ground',0.6,20,0.6,40),
 (621,'Força de Intervenção Rápida','ground',1.4,35,1.1,38);

INSERT INTO unit_stat VALUES
 (620,'soft_atk',5),(620,'hard_atk',1),(620,'defense',18),(620,'breakthrough',7), (620,'armor',0),(620,'piercing',5), (620,'hardness',0.1),(620,'hp',16),
 (621,'soft_atk',8),(621,'hard_atk',2),(621,'defense',22),(621,'breakthrough',12),(621,'armor',5),(621,'piercing',12),(621,'hardness',0.3),(621,'hp',22);

INSERT INTO unit_tag VALUES
 (620,'infantry'),(620,'ground'),(620,'coin'),
 (621,'infantry'),(621,'ground');

-- ===== espíritos nacionais + modificadores (ids 620..639) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('MOZ_defesa_cabo_delgado','MOZ','Contra-Insurgência em Cabo Delgado',
   'Anos a combater a insurgência jihadista nas matas do Norte ensinaram as tropas moçambicanas a lutar melhor em terreno de floresta.'),
 ('MOZ_legado_frelimo','MOZ','Legado da Luta Armada',
   'A memória da guerrilha de independência contra o colonialismo mantém viva uma doutrina de resistência territorial descentralizada, mais firme na defesa.'),
 ('MOZ_apoio_regional','MOZ','Apoio Regional (SADC/Ruanda)',
   'O treino e a coordenação com as forças ruandesas e da SADC destacadas em Cabo Delgado melhoraram a eficiência de comando das unidades moçambicanas.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (620,'spirit','terrain','forest','str_defender',NULL,'mul',1.20,'MOZ','MOZ_defesa_cabo_delgado'),
 (621,'spirit',NULL,NULL,'str_attacker','coin','mul',1.20,'MOZ','MOZ_defesa_cabo_delgado'),
 (622,'spirit',NULL,NULL,'str_defender',NULL,'add',0.08,'MOZ','MOZ_legado_frelimo'),
 (623,'spirit',NULL,NULL,'command',NULL,'mul',1.08,'MOZ','MOZ_apoio_regional');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('MOZ','production_speed',0.8),
 ('MOZ','org_regain',1.0),
 ('MOZ','start_army_mult',0.6);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('MOZ','República presidencialista','Presidente da República',
  'Contra-insurgência territorial descentralizada, com prioridade absoluta à estabilização de Cabo Delgado.',
  'Não-alinhado',
  'As Forças Armadas de Defesa de Moçambique são um exército pequeno e pouco financiado, cujo esforço principal desde 2017 é conter a insurgência jihadista na província de Cabo Delgado, com apoio de tropas do Ruanda e da SADC. A doutrina herda a experiência da guerrilha de independência da FRELIMO e privilegia unidades leves e móveis sobre uma força convencional pesada. Moçambique é um país lusófono, sem alianças militares formais fora da região austral de África.');

-- ===== templates próprios (ids 1+50*26 .. 50+50*26 = 1301..1350) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1301,'MOZ','Companhia de Contra-Insurgência'),
 (1302,'MOZ','Força de Intervenção Rápida');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1301,620,5),(1301,1,2),(1301,6,1),
 (1302,621,4),(1302,1,2),(1302,4,1);

-- ===== batalhões nomeados (ids 1301..1350) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1303,'MOZ','Batalhão de Contra-Insurgência de Cabo Delgado','Companhia de Contra-Insurgência','Cabo Delgado'),
 (1304,'MOZ','Força de Intervenção Rápida de Nampula','Força de Intervenção Rápida','Nampula'),
 (1305,'MOZ','Batalhão de Infantaria de Maputo','Infantaria','Maputo'),
 (1306,'MOZ','Batalhão de Infantaria da Zambézia','Infantaria','Zambezia'),
 (1307,'MOZ','Batalhão de Infantaria de Sofala','Infantaria','Sofala'),
 (1308,'MOZ','Batalhão de Infantaria de Manica','Infantaria','Manica'),
 (1309,'MOZ','Batalhão de Infantaria de Tete','Infantaria','Tete');

-- ===== correcção de terreno (floresta em Cabo Delgado, palco da insurgência) =====
UPDATE region SET terrain='forest'
 WHERE owner_id=(SELECT id FROM country WHERE tag='MOZ')
   AND name IN ('Cabo Delgado');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('moz_cabo_delgado','MOZ','Ofensiva de Estabilização em Cabo Delgado','Reforço da ofensiva conjunta com a SADC e o Ruanda para retomar o controlo do norte de Cabo Delgado.',35,NULL,1),
 ('moz_reequipamento','MOZ','Reequipamento das FADM','Programa de modernização do armamento ligeiro e dos meios de transporte das Forças Armadas de Defesa de Moçambique.',42,NULL,2),
 ('moz_gas_natural','MOZ','Protecção das Infraestruturas de Gás de Rovuma','Blindagem militar dos projectos de gás natural liquefeito na bacia do Rovuma, alvo preferencial da insurgência.',35,NULL,3),
 ('moz_fir','MOZ','Expansão da Força de Intervenção Rápida','Novos batalhões da FIR treinados para operações rápidas de contra-insurgência em terreno de mata.',35,'moz_cabo_delgado',4),
 ('moz_academia_militar','MOZ','Academia Militar de Maputo','Investimento na formação de oficiais e na doutrina moderna do Estado-Maior General.',28,'moz_reequipamento',5),
 ('moz_sadc_cooperacao','MOZ','Missão da SADC em Moçambique (SAMIM)','Aprofundamento da cooperação com as forças da SADC destacadas para proteger as instalações de gás.',35,'moz_gas_natural',6),
 ('moz_veteranos_frelimo','MOZ','Legado dos Veteranos da FRELIMO','Reintegração da experiência dos veteranos da luta de libertação no recrutamento e na moral das novas gerações.',49,'moz_fir',7),
 ('moz_industria_naval','MOZ','Estaleiros Navais da Beira e de Maputo','Recuperação dos estaleiros navais para apoiar a patrulha costeira e a logística militar do Índico.',42,'moz_academia_militar',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('moz_cabo_delgado','org_regain',1.05),
 ('moz_reequipamento','production_speed',1.08),
 ('moz_gas_natural','industry',1.10),
 ('moz_fir','org_regain',1.08),
 ('moz_fir','conscription',1.05),
 ('moz_academia_militar','research_speed',1.06),
 ('moz_sadc_cooperacao','industry',1.05),
 ('moz_veteranos_frelimo','conscription',1.10),
 ('moz_industria_naval','production_speed',1.07),
 ('moz_industria_naval','industry',1.05);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('moz_industria_naval','moz_cabo_delgado');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('MOZ_adv_gas','MOZ','economia','Administrador do Gás','🔥',160,'O gás do norte paga o orçamento inteiro.'),
 ('MOZ_adv_milicias','MOZ','seguranca','Chefe das Milícias Locais','🎖',150,'Arma quem conhece o mato.');
INSERT INTO advisor_effect VALUES ('MOZ_adv_gas','export_price',1.2);
INSERT INTO advisor_effect VALUES ('MOZ_adv_gas','industry',1.05);
INSERT INTO advisor_effect VALUES ('MOZ_adv_milicias','conscription',1.14);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('MOZ_gas','Gás de Cabo Delgado','🔥',10,'MOZ');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('MOZ_law_concessoes','MOZ_gas','Concessões estrangeiras','As multinacionais fazem a obra e levam o gás.',0,1,'MOZ'),
 ('MOZ_law_seguranca','MOZ_gas','Segurança dos projectos','Um cordão militar à volta de cada plataforma.',1,0,'MOZ'),
 ('MOZ_law_renda','MOZ_gas','Renda nacional do gás','A fatia do Estado cresce e paga fábrica em terra.',2,0,'MOZ');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('MOZ_law_concessoes','export_share',1.2),
 ('MOZ_law_concessoes','industry',0.97),
 ('MOZ_law_seguranca','defense',1.1),
 ('MOZ_law_seguranca','industry',1.05),
 ('MOZ_law_renda','industry',1.12),
 ('MOZ_law_renda','export_price',1.1);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('MOZ_gen_costa','Comandante da Costa','defense',1.14,125,'MOZ','🔥','Cabo Delgado ensinou-lhe a guerra que se faz entre a mata e o mar.'),
 ('MOZ_gen_rio','Chefe da Força do Zambeze','move_speed',1.16,115,'MOZ','🛶','Move a coluna pelo rio quando a estrada não existe.');
