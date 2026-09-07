-- DEU (k=7) — Bundeswehr (Exército Alemão), ordem de batalha aproximada 2024-2026.
-- Fontes: 1. Panzerdivision (Oldenburg), 10. Panzerdivision (Veitshöchheim), Panzerlehrbrigade 9
-- (Munster), Panzergrenadierbrigade 37 "Freistaat Sachsen" e 41 "Vorpommern", Panzergrenadierbrigade
-- 21 "Lipperland", Division Schnelle Kräfte (Fallschirmjäger + Gebirgsjäger, Saarlouis/Stetten),
-- Luftlandebrigade 1, Gebirgsjägerbrigade 23 (Bad Reichenhall), Wachbataillon (Berlim).
-- Carácter: indústria de precisão de classe mundial, doutrina de combate combinado (Panzergrenadier),
-- frente central da NATO com forte ênfase defensiva e de comando.

-- ===== unit_type próprios (100+20*7 .. 119+20*7 = 240..259) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (240,'Fallschirmjäger','ground',1.6,42,0.9,35),
 (241,'Leopard 2A7','ground',7.5,85,2.4,38),
 (242,'Puma (Schützenpanzer)','ground',2.9,48,1.5,46),
 (243,'Gebirgsjäger','ground',1.5,40,0.9,28);

INSERT INTO unit_stat VALUES
 (240,'soft_atk',7.5), (240,'hard_atk',1),  (240,'defense',21),(240,'breakthrough',12),(240,'armor',0), (240,'piercing',5), (240,'hardness',0.1), (240,'hp',21),
 (241,'soft_atk',13.5),(241,'hard_atk',19), (241,'defense',15),(241,'breakthrough',33),(241,'armor',78),(241,'piercing',68),(241,'hardness',0.93),(241,'hp',23),
 (242,'soft_atk',10.5),(242,'hard_atk',5),  (242,'defense',27),(242,'breakthrough',18),(242,'armor',20),(242,'piercing',23),(242,'hardness',0.55),(242,'hp',31),
 (243,'soft_atk',8),   (243,'hard_atk',1.5),(243,'defense',25),(243,'breakthrough',10),(243,'armor',0), (243,'piercing',6), (243,'hardness',0.15),(243,'hp',24);

INSERT INTO unit_tag VALUES
 (240,'infantry'),(240,'ground'),(240,'especial'),
 (241,'armored'),(241,'ground'),
 (242,'infantry'),(242,'armored'),(242,'ground'),
 (243,'infantry'),(243,'ground'),(243,'especial');

-- ===== espíritos nacionais + modificadores (ids 240..259) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('DEU_industria_de_precisao','DEU','Indústria de Precisão',
   'Rheinmetall e Krauss-Maffei Wegmann produzem blindados de referência mundial: as unidades blindadas alemãs atacam com mais força.'),
 ('DEU_doutrina_combinada','DEU','Doutrina de Armas Combinadas',
   'A tradição Panzergrenadier de infantaria mecanizada e blindados a operar juntos melhora a coordenação de comando em todas as frentes.'),
 ('DEU_frente_central_nato','DEU','Frente Central da NATO',
   'Território alemão como charneira defensiva da Aliança desde a Guerra Fria: as divisões defendem-se melhor.'),
 ('DEU_forcas_de_montanha','DEU','Fallschirmjäger e Gebirgsjäger',
   'A Division Schnelle Kräfte combina pára-quedistas e caçadores de montanha de elite: as tropas especiais rendem mais em terreno montanhoso.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (240,'spirit',NULL,NULL,           'str_attacker','armored', 'mul',1.15,'DEU','DEU_industria_de_precisao'),
 (241,'spirit',NULL,NULL,           'str_defender','armored', 'mul',1.08,'DEU','DEU_industria_de_precisao'),
 (242,'spirit',NULL,NULL,           'command',     NULL,      'mul',1.12,'DEU','DEU_doutrina_combinada'),
 (243,'spirit',NULL,NULL,           'str_defender',NULL,      'mul',1.10,'DEU','DEU_frente_central_nato'),
 (244,'spirit','terrain','mountain','str_attacker','especial','mul',1.20,'DEU','DEU_forcas_de_montanha'),
 (245,'spirit','terrain','mountain','str_defender','especial','mul',1.15,'DEU','DEU_forcas_de_montanha');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('DEU','production_speed',1.15),
 ('DEU','org_regain',1.15),
 ('DEU','start_army_mult',0.5);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('DEU','República Federal Parlamentar','Chanceler Federal e Ministro da Defesa',
  'Armas combinadas Panzergrenadier, defesa em profundidade da frente central europeia, indústria de blindados de referência.',
  'NATO',
  'A Bundeswehr é hoje um exército pequeno para a dimensão do país, mas assente numa indústria de defesa de precisão (Rheinmetall, KMW) e numa doutrina de armas combinadas testada desde a Guerra Fria. O Leopard 2 continua a ser referência mundial em blindados, e as forças especiais reúnem pára-quedistas e caçadores de montanha na Division Schnelle Kräfte.');

-- ===== templates próprios (ids 351..400) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (351,'DEU','Brigada Panzergrenadier'),
 (352,'DEU','Brigada Blindada Leopard'),
 (353,'DEU','Brigada de Elite (Jäger)');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (351,242,4),(351,241,2),(351,4,1),(351,6,1),
 (352,241,4),(352,242,2),(352,4,1),(352,5,1),
 (353,240,2),(353,243,2),(353,1,2),(353,4,1);

-- ===== brigadas/divisões reais nomeadas (ids 354..400) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (354,'DEU','1. Panzerdivision','Brigada Blindada Leopard','Niedersachsen'),
 (355,'DEU','10. Panzerdivision','Brigada Blindada Leopard','Bayern'),
 (356,'DEU','Panzergrenadierbrigade 37 "Freistaat Sachsen"','Brigada Panzergrenadier','Sachsen'),
 (357,'DEU','Panzerlehrbrigade 9','Blindada','Niedersachsen'),
 (358,'DEU','Panzergrenadierbrigade 41 "Vorpommern"','Brigada Panzergrenadier','Mecklenburg-Vorpommern'),
 (359,'DEU','Division Schnelle Kräfte','Brigada de Elite (Jäger)','Saarland'),
 (360,'DEU','Luftlandebrigade 1','Infantaria','Saarland'),
 (361,'DEU','Gebirgsjägerbrigade 23','Brigada de Elite (Jäger)','Bayern'),
 (362,'DEU','Wachbataillon','Infantaria','Berlin'),
 (363,'DEU','Panzergrenadierbrigade 21 "Lipperland"','Brigada Panzergrenadier','Nordrhein-Westfalen');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('deu_industria_precisao','DEU','Base Industrial de Defesa','A indústria alemã de defesa (Rheinmetall, KMW, Diehl) recebe incentivos fiscais e contratos plurianuais para escalar a produção.',35,NULL,1),
 ('deu_frente_nato','DEU','Frente Central da NATO','A Alemanha assume-se como charneira defensiva da NATO na Europa Central, reforçando comando e prontidão das divisões.',35,NULL,2),
 ('deu_nova_conscricao','DEU','Debate sobre o Serviço Militar','Um novo modelo de serviço voluntário e obrigatório é discutido no Bundestag para colmatar o défice de efetivos da Bundeswehr.',28,NULL,3),
 ('deu_rheinmetall_kmw','DEU','Expansão de Rheinmetall e KMW','Novas linhas de montagem em Unterlüß e Munique aceleram a produção de blindados e munições.',42,'deu_industria_precisao',4),
 ('deu_leopard2a8','DEU','Programa Leopard 2A8','A encomenda do Leopard 2A8, com blindagem reativa e canhão de maior calibre, moderniza as divisões Panzer.',49,'deu_rheinmetall_kmw',5),
 ('deu_zeitenwende','DEU','Zeitenwende: Fundo Especial','O fundo especial Sondervermögen de 100 mil milhões de euros financia investigação e prontidão operacional da Bundeswehr.',56,'deu_frente_nato',6),
 ('deu_reserva_territorial','DEU','Reserva Territorial (Heimatschutz)','A Reserva Territorial organiza-se em unidades regionais para reforçar a defesa civil e a resiliência do país em caso de crise.',35,'deu_nova_conscricao',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('deu_industria_precisao','industry',1.08),
 ('deu_frente_nato','org_regain',1.10),
 ('deu_nova_conscricao','conscription',1.15),
 ('deu_rheinmetall_kmw','industry',1.06),
 ('deu_rheinmetall_kmw','production_speed',1.08),
 ('deu_leopard2a8','production_speed',1.10),
 ('deu_zeitenwende','research_speed',1.12),
 ('deu_zeitenwende','org_regain',1.08),
 ('deu_reserva_territorial','conscription',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('deu_reserva_territorial','deu_industria_precisao');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('DEU_adv_ruhr','DEU','economia','Barão do Ruhr','🏭',200,'As chaminés do vale trabalham para ele.'),
 ('DEU_adv_engenharia','DEU','ciencia','Engenheiro-chefe do Estado','🔧',190,'Desenha e põe na linha de montagem no mesmo mês.');
INSERT INTO advisor_effect VALUES ('DEU_adv_ruhr','industry',1.16);
INSERT INTO advisor_effect VALUES ('DEU_adv_engenharia','research_speed',1.15);
INSERT INTO advisor_effect VALUES ('DEU_adv_engenharia','production_speed',1.06);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('DEU_zeitenwende','Viragem de Época','🦅',10,'DEU');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('DEU_law_fundo','DEU_zeitenwende','Fundo especial','Cem mil milhões votados de uma vez, gastos devagar.',0,1,'DEU'),
 ('DEU_law_rearmamento','DEU_zeitenwende','Rearmamento da Bundeswehr','As encomendas saem e os quartéis voltam a encher.',1,0,'DEU'),
 ('DEU_law_lideranca','DEU_zeitenwende','Liderança europeia','A Alemanha paga a defesa do continente e comanda-a.',2,0,'DEU');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('DEU_law_fundo','industry',1.05),
 ('DEU_law_rearmamento','production_speed',1.1),
 ('DEU_law_rearmamento','conscription',1.1),
 ('DEU_law_lideranca','attack',1.06),
 ('DEU_law_lideranca','org_regain',1.08),
 ('DEU_law_lideranca','industry',1.05);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('DEU_gen_estado_maior','Escola do Estado-Maior','org_regain',1.16,140,'DEU','🦅','A ordem de operações sai perfeita e a tropa recompõe-se a horas.'),
 ('DEU_gen_panzer','Mestre da Coluna Blindada','move_speed',1.18,145,'DEU','🛡','A tradição da manobra rápida, com os carros que a Alemanha ainda faz.');
