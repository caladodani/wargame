-- AGO (k=25) — Forças Armadas Angolanas (FAA), ordem de batalha aproximada 2024-2026.
-- Fontes: legado da guerra civil (1975-2002) e da guerra fronteiriça (intervenções na RDC),
-- brigadas blindadas equipadas com T-72 e BMP de origem soviética/russa, guarnição permanente do
-- enclave de Cabinda (separatismo FLEC), brigadas de infantaria por região militar.
-- Carácter: exército grande herdado da guerra civil, numeroso mas com equipamento envelhecido,
-- país lusófono. A ligação histórica a Portugal é apenas de contexto (colonial/linguístico),
-- sem qualquer aliança militar actual.

-- ===== unit_type próprios (100+20*25 .. 119+20*25 = 600..619) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (600,'T-72 Angolano','ground',2.6,50,1.8,30),
 (601,'Infantaria Veterana de Guerrilha','ground',0.9,28,0.9,24);

INSERT INTO unit_stat VALUES
 (600,'soft_atk',10),(600,'hard_atk',11),(600,'defense',10),(600,'breakthrough',20),(600,'armor',48),(600,'piercing',38),(600,'hardness',0.8),(600,'hp',17),
 (601,'soft_atk',7), (601,'hard_atk',1), (601,'defense',26),(601,'breakthrough',9), (601,'armor',0), (601,'piercing',5), (601,'hardness',0.1),(601,'hp',24);

INSERT INTO unit_tag VALUES
 (600,'armored'),(600,'ground'),
 (601,'infantry'),(601,'ground'),(601,'veterano');

-- ===== espíritos nacionais + modificadores (ids 600..619) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('AGO_veteranos_guerra_civil','AGO','Veteranos da Guerra Civil',
   'Décadas de guerra interna (1975-2002) deixaram um corpo de infantaria extremamente experiente na defesa do território, mesmo com equipamento datado.'),
 ('AGO_exercito_de_massa','AGO','Exército de Massa',
   'As FAA privilegiam o número de efectivos sobre a coordenação sofisticada: mais força de ataque em números, mas comando menos ágil.'),
 ('AGO_defesa_cabinda','AGO','Guarnição de Cabinda',
   'A separação geográfica do enclave de Cabinda obrigou a uma doutrina de defesa fixa em floresta, muito treinada contra a guerrilha da FLEC.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (600,'spirit',NULL,NULL,'str_defender','veterano','mul',1.20,'AGO','AGO_veteranos_guerra_civil'),
 (601,'spirit',NULL,NULL,'str_defender',NULL,      'add',0.05,'AGO','AGO_veteranos_guerra_civil'),
 (602,'spirit',NULL,NULL,'str_attacker',NULL,      'mul',1.10,'AGO','AGO_exercito_de_massa'),
 (603,'spirit',NULL,NULL,'command',     NULL,      'mul',0.92,'AGO','AGO_exercito_de_massa'),
 (604,'spirit','terrain','forest','str_defender',NULL,'mul',1.20,'AGO','AGO_defesa_cabinda');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('AGO','production_speed',0.85),
 ('AGO','org_regain',0.9),
 ('AGO','start_army_mult',1.3);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('AGO','República presidencialista','Presidente da República',
  'Defesa territorial de massa herdada da guerra civil, com prioridade à guarnição do enclave de Cabinda.',
  'Não-alinhado',
  'As Forças Armadas Angolanas são um exército numeroso, herdeiro directo de quase três décadas de guerra civil que só terminou em 2002. O equipamento, sobretudo blindados T-72 e BMP de origem soviética e russa, está envelhecido mas em grande quantidade, e a infantaria acumula uma experiência de combate real pouco comum em África. Angola é um país lusófono, com uma ligação histórica e linguística a Portugal que já não se traduz em qualquer aliança militar.');

-- ===== templates próprios (ids 1+50*25 .. 50+50*25 = 1251..1300) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1251,'AGO','Brigada Blindada'),
 (1252,'AGO','Brigada de Infantaria Veterana');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1251,600,4),(1251,2,2),(1251,4,2),
 (1252,601,6),(1252,4,1),(1252,6,1);

-- ===== brigadas nomeadas (ids 1251..1300) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1253,'AGO','1ª Brigada Blindada','Brigada Blindada','Luanda'),
 (1254,'AGO','8ª Brigada Blindada','Brigada Blindada','Huambo'),
 (1255,'AGO','Brigada de Infantaria de Benguela','Brigada de Infantaria Veterana','Benguela'),
 (1256,'AGO','Brigada de Infantaria do Moxico','Brigada de Infantaria Veterana','Moxico'),
 (1257,'AGO','Brigada de Infantaria do Cuando Cubango','Brigada de Infantaria Veterana','Cuando Cubango'),
 (1258,'AGO','Brigada de Infantaria da Lunda Norte','Infantaria','Lunda Norte'),
 (1259,'AGO','Brigada de Infantaria de Malanje','Infantaria','Malanje'),
 (1260,'AGO','Brigada de Infantaria da Huíla','Brigada de Infantaria Veterana','Huíla'),
 (1261,'AGO','Brigada de Infantaria do Bié','Infantaria','Bié'),
 (1262,'AGO','Brigada de Defesa de Cabinda','Brigada de Infantaria Veterana','Cabinda'),
 (1263,'AGO','Brigada de Infantaria do Zaire','Infantaria','Zaire'),
 (1264,'AGO','Brigada de Infantaria do Uíge','Infantaria','Uíge'),
 (1265,'AGO','Regimento Blindado de Cuanza Sul','Blindada','Cuanza Sul');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('ago_modernizacao_blindada','AGO','Modernização Blindada','Revisão da frota de T-72 e BMP herdada da guerra fria, com prioridade à manutenção industrial.',42,NULL,1),
 ('ago_reequipamento_t72','AGO','Linha de Reequipamento','Acordos com fornecedores tradicionais para peças e novas remessas de blindados.',49,'ago_modernizacao_blindada',2),
 ('ago_diamantes_petroleo','AGO','Diamantes e Petróleo ao Serviço da Defesa','Receitas extractivas financiam investigação e produção militar.',35,'ago_modernizacao_blindada',3),
 ('ago_guarnicao_cabinda','AGO','Reforço da Guarnição de Cabinda','Consolidação da presença militar fixa no enclave, resposta permanente à FLEC.',35,NULL,4),
 ('ago_doutrina_guerrilha','AGO','Doutrina Antiguerrilha','Institucionalização das lições da luta contra a FLEC em manuais e treino de tropa.',42,'ago_guarnicao_cabinda',5),
 ('ago_recenseamento_massa','AGO','Recenseamento Militar Alargado','Reorganização do serviço militar obrigatório para sustentar um exército de massa.',28,NULL,6),
 ('ago_veteranos_reserva','AGO','Reserva de Veteranos','Antigos combatentes da guerra civil são reintegrados como quadro de reserva mobilizável.',35,'ago_recenseamento_massa',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('ago_modernizacao_blindada','industry',1.10),
 ('ago_reequipamento_t72','production_speed',1.12),
 ('ago_diamantes_petroleo','industry',1.08),
 ('ago_diamantes_petroleo','research_speed',1.05),
 ('ago_guarnicao_cabinda','org_regain',1.10),
 ('ago_doutrina_guerrilha','org_regain',1.10),
 ('ago_recenseamento_massa','conscription',1.20),
 ('ago_veteranos_reserva','conscription',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('ago_reequipamento_t72','ago_diamantes_petroleo');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('ago_veteranos_reserva','ago_modernizacao_blindada');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('AGO_adv_petrolifera','AGO','economia','Director da Petrolífera','🛢',170,'Barris vendidos ao melhor preço do golfo.'),
 ('AGO_adv_matas','AGO','seguranca','Veterano das Matas','🌳',155,'Fez a guerra toda sem estrada nenhuma.');
INSERT INTO advisor_effect VALUES ('AGO_adv_petrolifera','export_price',1.22);
INSERT INTO advisor_effect VALUES ('AGO_adv_matas','move_speed',1.12);
INSERT INTO advisor_effect VALUES ('AGO_adv_matas','org_regain',1.06);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('AGO_petroleo','Renda do Petróleo','⛽',10,'AGO');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('AGO_law_concessoes','AGO_petroleo','Concessões abertas','As companhias estrangeiras levam o bruto e deixam a taxa.',0,1,'AGO'),
 ('AGO_law_partilha','AGO_petroleo','Contratos de partilha','Sonangol fica com metade de cada barril que sai.',1,0,'AGO'),
 ('AGO_law_nacionalizacao','AGO_petroleo','Nacionalização do bruto','O petróleo é do Estado: a refinaria cresce, o comprador foge.',2,0,'AGO');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('AGO_law_concessoes','export_share',1.15),
 ('AGO_law_concessoes','industry',0.97),
 ('AGO_law_partilha','industry',1.06),
 ('AGO_law_partilha','export_price',1.05),
 ('AGO_law_nacionalizacao','industry',1.12),
 ('AGO_law_nacionalizacao','export_share',0.85),
 ('AGO_law_nacionalizacao','research_speed',0.95);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('AGO_gen_mato','Comandante do Mato','defense',1.14,130,'AGO','🌿','Vinte anos de guerra na savana: sabe onde a coluna passa e onde morre.'),
 ('AGO_gen_brigada_ligeira','Chefe da Brigada Ligeira','move_speed',1.18,120,'AGO','🏍','Camionetas e homens leves: chega à baixa do Kwanza antes de darem por ele.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('AGO_escola','Guerra da Savana','🌿',10,'AGO');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('AGO_doc_bush','AGO_escola','Escola do Mato','A coluna que não se vê da estrada é a que chega inteira ao fim do dia.',50,NULL,1,'AGO'),
 ('AGO_doc_emboscada','AGO_escola','Emboscada em Profundidade','Deixa-se passar a ponta e bate-se no meio, onde vão os camiões.',110,'AGO_doc_bush',2,'AGO'),
 ('AGO_doc_longa','AGO_escola','Guerra Longa','Vinte anos ensinam que quem aguenta mais tempo é que ganha.',190,'AGO_doc_emboscada',3,'AGO');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('AGO_doc_bush','defense',1.06),
 ('AGO_doc_emboscada','defense',1.06),
 ('AGO_doc_emboscada','attack',1.05),
 ('AGO_doc_longa','defense',1.08),
 ('AGO_doc_longa','org_regain',1.07);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('AGO_ar','Asas do Kwanza','🛩',11,'AGO','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('AGO_ar_vigia','AGO_ar','Vigia do Mato','Um avião que sabe onde olhar vale por dez que andam à procura.',50,NULL,1,'AGO'),
 ('AGO_ar_cuito','AGO_ar','Lição de Cuito Cuanavale','A maior batalha aérea de África ensinou a não gastar o que não se substitui.',110,'AGO_ar_vigia',2,'AGO'),
 ('AGO_ar_coluna','AGO_ar','Apoio à Coluna','O avião serve a coluna em terra; quem se esquece disso perde as duas coisas.',185,'AGO_ar_cuito',3,'AGO');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('AGO_ar_vigia','air_losses',0.95),
 ('AGO_ar_cuito','air_losses',0.94),
 ('AGO_ar_cuito','air_upkeep',0.95),
 ('AGO_ar_coluna','air_bombing',1.1);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('AGO_mar','Guarda de Cabinda','🛢',12,'AGO','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('AGO_mar_barra','AGO_mar','Guarda da Barra','Quem manda na foz manda no país que vive dela.',50,NULL,1,'AGO'),
 ('AGO_mar_lancha','AGO_mar','Escola das Lanchas','Barcos pequenos, mar de casa, inimigo sempre longe do porto dele.',110,'AGO_mar_barra',2,'AGO'),
 ('AGO_mar_petroleo','AGO_mar','Escolta do Petróleo','O que sai de Cabinda paga o país: sai com escolta ou não sai.',185,'AGO_mar_lancha',3,'AGO');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('AGO_mar_barra','naval_patrol',1.08),
 ('AGO_mar_lancha','naval_upkeep',0.92),
 ('AGO_mar_petroleo','naval_escort',1.1);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('AGO_ar_gen_kwanza','Chefe da Asa do Kwanza','air_upkeep',0.88,145,'AGO','🪶','Mantém no ar aparelhos com peças que já ninguém fabrica.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('AGO_mar_gen_namibe','Comandante da Costa do Namibe','naval_patrol',1.15,145,'AGO','🐚','Conhece cada enseada da costa e sabe onde um navio se esconde.','mar',45);
