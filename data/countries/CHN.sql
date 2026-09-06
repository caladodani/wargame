-- China: indústria colossal, exército de massa moderno mas sem experiência de combate real desde 1979.
-- Ordem de batalha aprox. 2024-2026: 13 Exércitos de Grupo do EPL + guarnições de fronteira
-- (Xinjiang, Tibete) especializadas em montanha/altitude.

-- ===== Unidades próprias =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (160,'Infantaria de Montanha','ground',1.1,32,0.9,22),
 (161,'Blindados Type 99','ground',4.3,62,2.1,40),
 (162,'Milícia de Massa','ground',0.6,20,0.8,20);

INSERT INTO unit_stat VALUES
 (160,'soft_atk',6),(160,'hard_atk',1),(160,'defense',28),(160,'breakthrough',8),(160,'armor',0),(160,'piercing',6),(160,'hardness',0.1),(160,'hp',27),
 (161,'soft_atk',13),(161,'hard_atk',15),(161,'defense',13),(161,'breakthrough',29),(161,'armor',68),(161,'piercing',58),(161,'hardness',0.9),(161,'hp',21),
 (162,'soft_atk',5),(162,'hard_atk',1),(162,'defense',18),(162,'breakthrough',6),(162,'armor',0),(162,'piercing',4),(162,'hardness',0.05),(162,'hp',20);

INSERT INTO unit_tag VALUES
 (160,'infantry'),(160,'ground'),(160,'montanha'),
 (161,'armored'),(161,'ground'),
 (162,'infantry'),(162,'ground');

-- ===== Espíritos nacionais =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('CHN_industria_colossal','CHN','Indústria Colossal','A maior base fabril do mundo mantém a artilharia e o apoio sempre bem municiados.'),
 ('CHN_falta_experiencia','CHN','Falta de Experiência de Combate','Sem uma guerra de grande escala desde 1979, os ataques rendem menos do que o equipamento faria prever.'),
 ('CHN_muralha_defensiva','CHN','Doutrina da Muralha Defensiva','Décadas de defesa territorial em profundidade tornam as tropas muito mais difíceis de desalojar em montanha ou zona urbana.'),
 ('CHN_exercito_povo','CHN','Exército do Povo','A doutrina de guerra popular compensa com números o que falta em sofisticação individual.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (170,'spirit',NULL,NULL,'str','support','add',0.10,'CHN','CHN_industria_colossal'),
 (171,'spirit',NULL,NULL,'str_attacker',NULL,'mul',0.90,'CHN','CHN_falta_experiencia'),
 (172,'spirit','terrain','mountain','str_defender',NULL,'mul',1.15,'CHN','CHN_muralha_defensiva'),
 (173,'spirit','terrain','urban','str_defender',NULL,'mul',1.10,'CHN','CHN_muralha_defensiva'),
 (174,'spirit',NULL,NULL,'str','infantry','add',0.05,'CHN','CHN_exercito_povo');

-- ===== country_stat / country_info =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('CHN','industry',1.3),
 ('CHN','production_speed',1.3),
 ('CHN','org_regain',0.95),
 ('CHN','start_army_mult',1.3);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('CHN','República popular de partido único','Presidente e Secretário-Geral do Partido',
  'Guerra popular moderna: massa industrial e territorial apoiada em mísseis, drones e defesa em profundidade.',
  'Não-alinhado',
  'A segunda maior economia do mundo transformou o Exército Popular de Libertação num dos maiores e mais bem equipados do planeta em poucas décadas. Falta-lhe, porém, experiência de combate recente à escala de uma grande guerra — a força vem da quantidade, da indústria e da defesa territorial, não ainda da prática de fogo real.');

-- ===== Templates próprios =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (151,'CHN','Divisão de Montanha'),
 (152,'CHN','Divisão Blindada Type 99'),
 (153,'CHN','Onda Humana');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (151,160,6),(151,4,2),
 (152,161,4),(152,2,3),(152,4,2),
 (153,162,8),(153,4,2);

-- ===== Brigadas/divisões reais (exército inicial) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (154,'CHN','71º Exército de Grupo','Mecanizada','Jiangsu'),
 (155,'CHN','72º Exército de Grupo','Blindada','Zhejiang'),
 (156,'CHN','73º Exército de Grupo','Infantaria','Fujian'),
 (157,'CHN','74º Exército de Grupo','Divisão Blindada Type 99','Guangdong'),
 (158,'CHN','75º Exército de Grupo','Infantaria','Guangxi'),
 (159,'CHN','76º Exército de Grupo','Blindada','Shaanxi'),
 (160,'CHN','77º Exército de Grupo','Mecanizada','Sichuan'),
 (161,'CHN','78º Exército de Grupo','Blindada','Heilongjiang'),
 (162,'CHN','79º Exército de Grupo','Infantaria AT','Liaoning'),
 (163,'CHN','80º Exército de Grupo','Infantaria','Shandong'),
 (164,'CHN','81º Exército de Grupo','Divisão Blindada Type 99','Hebei'),
 (165,'CHN','82º Exército de Grupo','Infantaria','Henan'),
 (166,'CHN','Guarnição de Xinjiang','Divisão de Montanha','Xinjiang'),
 (167,'CHN','Guarnição do Tibete','Divisão de Montanha','Xizang'),
 (168,'CHN','Guarnição de Pequim','Infantaria AT','Beijing'),
 (169,'CHN','Milícias Populares de Massa','Onda Humana','Hunan');
