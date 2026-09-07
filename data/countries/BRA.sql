-- BRA (k=1) — Exército Brasileiro, ordem de batalha aproximada 2024-2026.
-- Fontes: brigadas de Infantaria de Selva amazônicas, Brigada de Infantaria Paraquedista (RJ),
-- brigadas de Cavalaria Mecanizada e de Infantaria Motorizada da fronteira sul/oeste, Brigadas
-- de Cavalaria Blindada e de Infantaria Blindada (Leopard 1A5 BR), Guarani (VBTP) generalizado.
-- Carácter (pt-BR nos nomes das unidades): exército grande, de conscritos, fronteiras extensas,
-- doutrina de selva própria, indústria de defesa nacional (Embraer, Avibras, Iveco-FNSS).

-- ===== unit_type próprios (100+20*1 .. 119+20*1 = 120..139) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (120,'Infantaria de Selva','ground',1.3,35,0.9,22),
 (121,'Guarani','ground',2.2,42,1.4,48),
 (122,'Leopard 1A5 BR','ground',2.2,45,1.8,42),
 (123,'Cavalaria Mecanizada','ground',2.0,38,1.3,58);

INSERT INTO unit_stat VALUES
 (120,'soft_atk',7), (120,'hard_atk',1), (120,'defense',26),(120,'breakthrough',9), (120,'armor',0), (120,'piercing',5), (120,'hardness',0.1), (120,'hp',27),
 (121,'soft_atk',9), (121,'hard_atk',4), (121,'defense',25),(121,'breakthrough',15),(121,'armor',12),(121,'piercing',18),(121,'hardness',0.45),(121,'hp',28),
 (122,'soft_atk',10),(122,'hard_atk',11),(122,'defense',10),(122,'breakthrough',22),(122,'armor',35),(122,'piercing',40),(122,'hardness',0.75),(122,'hp',16),
 (123,'soft_atk',8), (123,'hard_atk',5), (123,'defense',18),(123,'breakthrough',20),(123,'armor',20),(123,'piercing',25),(123,'hardness',0.5), (123,'hp',22);

INSERT INTO unit_tag VALUES
 (120,'infantry'),(120,'ground'),(120,'selva'),
 (121,'infantry'),(121,'armored'),(121,'ground'),
 (122,'armored'),(122,'ground'),
 (123,'armored'),(123,'ground');

-- ===== espíritos nacionais + modificadores (ids 120..139) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('BRA_selva','BRA','Selva!',
   'Décadas de operações na Amazônia deram origem a uma doutrina própria de guerra de selva: as unidades de Infantaria de Selva combatem com grande vantagem em terreno de floresta.'),
 ('BRA_exercito_de_massa','BRA','Exército de Massa',
   'Um grande contingente anual de recrutas permite mobilizar um exército numeroso, mas o treino médio por soldado é mais raso do que em forças totalmente profissionais.'),
 ('BRA_fronteiras_vivas','BRA','Fronteiras Vivas',
   'Doutrina de presença permanente ao longo da fronteira amazônica e platina: as tropas defendem melhor o território nacional.'),
 ('BRA_industria_de_defesa','BRA','Indústria de Defesa Nacional',
   'Embraer, Avibras e a Iveco-FNSS sustentam produção local de blindados, viaturas e munições, reduzindo a dependência de importação e melhorando ligeiramente o equipamento em campo.'),
 ('BRA_potencia_regional','BRA','Potência Regional Não-Alinhada',
   'Liderança histórica na América do Sul e larga experiência em missões de paz da ONU reforçam a coordenação de comando das Forças Armadas.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (120,'spirit','terrain','forest','str_attacker','selva','mul',1.25,'BRA','BRA_selva'),
 (121,'spirit','terrain','forest','str_defender','selva','mul',1.20,'BRA','BRA_selva'),
 (122,'spirit',NULL,NULL,        'str',          NULL,   'mul',0.94,'BRA','BRA_exercito_de_massa'),
 (123,'spirit',NULL,NULL,        'str_defender', NULL,   'add',0.15,'BRA','BRA_fronteiras_vivas'),
 (124,'spirit',NULL,NULL,        'str',          NULL,   'mul',1.04,'BRA','BRA_industria_de_defesa'),
 (125,'spirit',NULL,NULL,        'command',      NULL,   'mul',1.06,'BRA','BRA_potencia_regional');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('BRA','production_speed',1.15),
 ('BRA','org_regain',0.8),
 ('BRA','start_army_mult',1.6);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('BRA','República federativa presidencialista','Presidente da República Federativa do Brasil',
  'Defesa de fronteiras extensas e da Amazônia, apoiada em mobilização de massa e indústria de defesa nacional.',
  'Não-alinhado',
  'O Brasil possui o maior exército da América do Sul, historicamente concentrado na defesa da fronteira amazônica e platina. Uma doutrina própria de guerra de selva sustenta várias brigadas amazônicas, enquanto o sul do país concentra a cavalaria mecanizada e blindada. A indústria de defesa nacional (Embraer, Avibras, Iveco-FNSS) garante alguma autonomia de produção, e a longa tradição de missões de paz da ONU reforça a coordenação de comando.');

-- ===== templates próprios (ids 51..100) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (51,'BRA','Infantaria de Selva'),
 (52,'BRA','Cavalaria Mecanizada'),
 (53,'BRA','Blindada Leopard'),
 (54,'BRA','Infantaria Motorizada');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (51,120,5),(51,1,2),(51,4,1),
 (52,123,4),(52,121,3),(52,4,1),
 (53,122,4),(53,121,3),(53,4,1),
 (54,1,5),(54,121,2),(54,4,1),(54,6,1);

-- ===== brigadas reais nomeadas (ids 51..100) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (61,'BRA','1ª Brigada de Infantaria de Selva','Infantaria de Selva','Roraima'),
 (62,'BRA','2ª Brigada de Infantaria de Selva','Infantaria de Selva','Amazonas'),
 (63,'BRA','16ª Brigada de Infantaria de Selva','Infantaria de Selva','Amazonas'),
 (64,'BRA','17ª Brigada de Infantaria de Selva','Infantaria de Selva','Rondônia'),
 (65,'BRA','23ª Brigada de Infantaria de Selva','Infantaria de Selva','Pará'),
 (66,'BRA','Brigada de Infantaria Paraquedista','Infantaria','Rio de Janeiro'),
 (67,'BRA','12ª Brigada de Infantaria Leve Aeromóvel','Infantaria Motorizada','São Paulo'),
 (68,'BRA','5ª Brigada de Cavalaria Blindada','Blindada Leopard','Paraná'),
 (69,'BRA','6ª Brigada de Infantaria Blindada','Blindada Leopard','Rio Grande do Sul'),
 (70,'BRA','1ª Brigada de Cavalaria Mecanizada','Cavalaria Mecanizada','Rio Grande do Sul'),
 (71,'BRA','2ª Brigada de Cavalaria Mecanizada','Cavalaria Mecanizada','Rio Grande do Sul'),
 (72,'BRA','3ª Brigada de Cavalaria Mecanizada','Cavalaria Mecanizada','Rio Grande do Sul'),
 (73,'BRA','4ª Brigada de Cavalaria Mecanizada','Cavalaria Mecanizada','Mato Grosso do Sul'),
 (74,'BRA','13ª Brigada de Infantaria Motorizada','Infantaria Motorizada','Mato Grosso'),
 (75,'BRA','10ª Brigada de Infantaria Motorizada','Infantaria Motorizada','Pernambuco'),
 (76,'BRA','7ª Brigada de Infantaria Motorizada','Infantaria Motorizada','Rio Grande do Norte'),
 (77,'BRA','3ª Brigada de Infantaria Motorizada','Infantaria Motorizada','Goiás'),
 (78,'BRA','4ª Brigada de Infantaria Leve de Montanha','Infantaria','Minas Gerais'),
 (79,'BRA','15ª Brigada de Infantaria Mecanizada','Mecanizada','Paraná'),
 (80,'BRA','9ª Brigada de Infantaria Motorizada','Infantaria Motorizada','Distrito Federal');

-- ===== correcção de terreno =====
UPDATE region SET terrain='forest'
 WHERE owner_id=(SELECT id FROM country WHERE tag='BRA') AND name IN ('Acre','Rondônia');
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='BRA') AND name IN ('Minas Gerais','Espírito Santo');
UPDATE region SET terrain='urban'
 WHERE owner_id=(SELECT id FROM country WHERE tag='BRA') AND name IN ('São Paulo');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('bra_amazonia','BRA','Amazônia é Nossa','Presença permanente na fronteira norte: pelotões de selva e vigilância.',35,NULL,1),
 ('bra_sivam','BRA','SIVAM Ampliado','Radares e sensores cobrem a floresta: ninguém entra sem ser visto.',42,'bra_amazonia',2),
 ('bra_industria','BRA','Base Industrial de Defesa','Embraer, IMBEL e Taurus em ritmo de guerra: produção nacional.',49,NULL,3),
 ('bra_pre_sal','BRA','Riqueza do Pré-Sal','A renda do petróleo financia o rearmamento.',42,'bra_industria',4),
 ('bra_prosub','BRA','Programa de Submarinos','Tecnologia nuclear naval própria: dissuasão no Atlântico Sul.',56,'bra_industria',5),
 ('bra_mobilizacao','BRA','Mobilização Nacional','O serviço militar obrigatório vira reserva treinada de verdade.',35,NULL,6),
 ('bra_potencia','BRA','Potência Regional','Liderança sul-americana assumida: doutrina própria e projeção de força.',56,'bra_mobilizacao',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('bra_amazonia','org_regain',1.05),
 ('bra_sivam','research_speed',1.08),
 ('bra_industria','production_speed',1.12),
 ('bra_pre_sal','industry',1.10),
 ('bra_prosub','research_speed',1.05),
 ('bra_prosub','industry',1.03),
 ('bra_mobilizacao','conscription',1.30),
 ('bra_potencia','org_regain',1.08),
 ('bra_potencia','conscription',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('bra_pre_sal','bra_prosub');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('bra_potencia','bra_amazonia');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('BRA_adv_aeronautica','BRA','ciencia','Engenheiro-chefe da Aeronáutica','✈',185,'Desenha aviões que o país sabe construir.'),
 ('BRA_adv_amazonia','BRA','seguranca','Coronel da Amazónia','🌳',170,'Move tropa onde não há estrada nenhuma.');
INSERT INTO advisor_effect VALUES ('BRA_adv_aeronautica','research_speed',1.14);
INSERT INTO advisor_effect VALUES ('BRA_adv_aeronautica','production_speed',1.05);
INSERT INTO advisor_effect VALUES ('BRA_adv_amazonia','move_speed',1.12);
INSERT INTO advisor_effect VALUES ('BRA_adv_amazonia','defense',1.05);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('BRA_amazonia','Soberania da Amazónia','🌳',10,'BRA');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('BRA_law_vigilancia','BRA_amazonia','Vigilância aérea','Radares e satélites olham a floresta de cima.',0,1,'BRA'),
 ('BRA_law_selva','BRA_amazonia','Batalhões de selva','Quem vive lá defende-a, com a doutrina que a mata pede.',1,0,'BRA'),
 ('BRA_law_interior','BRA_amazonia','Ocupação do interior','Estradas, quartéis e povoamento: a floresta passa a ter dono à vista.',2,0,'BRA');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('BRA_law_vigilancia','counter_intel',1.15),
 ('BRA_law_selva','defense',1.1),
 ('BRA_law_selva','org_regain',1.05),
 ('BRA_law_interior','industry',1.06),
 ('BRA_law_interior','conscription',1.1);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('BRA_gen_selva','Comandante de Selva','defense',1.16,130,'BRA','🌳','A escola de Manaus: a floresta é dele e o invasor é que se perde.'),
 ('BRA_gen_pracinha','Herdeiro dos Pracinhas','attack',1.13,140,'BRA','🐍','A cobra fumou uma vez em Itália e ninguém deixou esquecer.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('BRA_escola','Escola de Selva','🌳',10,'BRA');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('BRA_doc_selva','BRA_escola','Centro de Guerra na Selva','Manaus forma quem sabe viver onde o invasor apenas sobrevive.',50,NULL,1,'BRA'),
 ('BRA_doc_amazonia','BRA_escola','Vigilância da Amazónia','Uma fronteira que não se guarda com arame guarda-se com movimento.',110,'BRA_doc_selva',2,'BRA'),
 ('BRA_doc_pracinha','BRA_escola','Herança dos Pracinhas','A cobra fumou uma vez e o exército nunca mais deixou esquecer.',190,'BRA_doc_amazonia',3,'BRA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('BRA_doc_selva','defense',1.07),
 ('BRA_doc_amazonia','defense',1.06),
 ('BRA_doc_amazonia','move_speed',1.06),
 ('BRA_doc_pracinha','attack',1.09),
 ('BRA_doc_pracinha','org_regain',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('BRA_ar','Asas do Atlântico Sul','🦜',11,'BRA','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('BRA_ar_embraer','BRA_ar','Fábrica Própria','Quem faz os seus aviões repara-os na mesma tarde.',50,NULL,1,'BRA'),
 ('BRA_ar_amazonia_ar','BRA_ar','Vigilância da Amazónia','Cinco milhões de quilómetros quadrados vigiam-se de cima ou não se vigiam.',110,'BRA_ar_embraer',2,'BRA'),
 ('BRA_ar_senta_pua','BRA_ar','Senta a Pua','O grupo de caça que voou em Itália deixou o lema e a maneira.',185,'BRA_ar_amazonia_ar',3,'BRA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('BRA_ar_embraer','air_upkeep',0.9),
 ('BRA_ar_amazonia_ar','air_losses',0.94),
 ('BRA_ar_senta_pua','air_bombing',1.1);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('BRA_mar','Mar Azul','🇧🇷',12,'BRA','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('BRA_mar_amazonia_azul','BRA_mar','Amazónia Azul','Quatro milhões de quilómetros quadrados de mar são território, não paisagem.',50,NULL,1,'BRA'),
 ('BRA_mar_escolta_bra','BRA_mar','Escolta do Atlântico','Comboios para o norte durante uma guerra inteira ensinaram o ofício.',110,'BRA_mar_amazonia_azul',2,'BRA'),
 ('BRA_mar_fluvial','BRA_mar','Esquadra Fluvial','Onde o mar acaba começa o rio, e o rio também se guarda.',185,'BRA_mar_escolta_bra',3,'BRA');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('BRA_mar_amazonia_azul','naval_patrol',1.1),
 ('BRA_mar_escolta_bra','naval_escort',1.09),
 ('BRA_mar_fluvial','naval_upkeep',0.93);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('BRA_ar_gen_atlantico_bra','Chefe da Asa do Atlântico','air_losses',0.88,145,'BRA','🕊','Patrulha um oceano com poucos aviões e não perde nenhum por descuido.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('BRA_mar_gen_esquadra_sul','Almirante da Esquadra do Sul','naval_patrol',1.15,145,'BRA','🌊','Divide o mar em quadrados e não deixa um por olhar.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('BRA_tech_ar_caca_nacional','Aviação','Programa de Caça Nacional',260,'air_1','Montagem e manutenção em casa: os aparelhos voam mais e caem menos por falta de peça.','BRA'),
 ('BRA_tech_mar_amazonia_azul','Marinha','Amazónia Azul',260,'nav_1','Vigilância da plataforma continental inteira, do pré-sal à foz: o mar é território.','BRA');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('BRA_tech_ar_caca_nacional','air_losses',0.87),
 ('BRA_tech_mar_amazonia_azul','naval_patrol',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'General de Brigada',0,0,'BRA'),
 ('exercito',2,'General de Divisão',40,0.5,'BRA'),
 ('exercito',3,'General de Exército',110,1,'BRA'),
 ('exercito',4,'Comandante do Exército',220,1.75,'BRA'),
 ('exercito',5,'Marechal',360,2.5,'BRA'),
 ('ar',1,'Brigadeiro do Ar',0,0,'BRA'),
 ('ar',2,'Major-Brigadeiro',40,0.5,'BRA'),
 ('ar',3,'Tenente-Brigadeiro',110,1,'BRA'),
 ('ar',4,'Comandante da Aeronáutica',220,1.75,'BRA'),
 ('ar',5,'Marechal do Ar',360,2.5,'BRA'),
 ('mar',1,'Contra-Almirante do Brasil',0,0,'BRA'),
 ('mar',2,'Vice-Almirante do Brasil',40,0.5,'BRA'),
 ('mar',3,'Almirante de Esquadra',110,1,'BRA'),
 ('mar',4,'Comandante da Marinha',220,1.75,'BRA'),
 ('mar',5,'Almirante',360,2.5,'BRA');

-- ===== condecorações nacionais (medal.country_tag) =====
INSERT INTO medal (id,name,description,metric,threshold,bonus,sort,country_tag) VALUES
 ('BRA_baptismo','Medalha de Campanha','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1,'BRA'),
 ('BRA_assalto','Medalha de Assalto','Tomou três regiões ao inimigo.','captures',3,0.03,2,'BRA'),
 ('BRA_campanha','Louvor do Comando','Quarenta pontos de experiência em combate.','xp',40,0.02,3,'BRA'),
 ('BRA_aco','Cruz de Combate','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4,'BRA'),
 ('BRA_imortais','Ordem do Mérito Militar','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5,'BRA');

-- ===== nomes de formação nacionais (formation_name.country_tag) =====
INSERT INTO formation_name (id,name,domain,sort,country_tag) VALUES
 ('BRA_ar_1','Esquadrão Jaguar','ar',1,'BRA'),
 ('BRA_ar_2','Esquadrão Pampa','ar',2,'BRA'),
 ('BRA_ar_3','Grupo de Bombardeio de Natal','ar',3,'BRA'),
 ('BRA_mar_1','Esquadra do Atlântico Sul','mar',1,'BRA'),
 ('BRA_mar_2','Flotilha do Rio de Janeiro','mar',2,'BRA'),
 ('BRA_mar_3','Divisão Naval do Nordeste','mar',3,'BRA');
