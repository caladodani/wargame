-- KOR (k=10) — Exército da República da Coreia (ROK Army), ordem de batalha aproximada 2024-2026.
-- Fontes: I, II e III Corpo (fronteira com a DMZ), K2 Black Panther, K9 Thunder, ROK Marine Corps
-- (fuzileiros), Special Warfare Command. Carácter: exército grande e moderno, sustentado por
-- conscrição universal, indústria de defesa própria de topo (Hyundai Rotem, Hanwha) e décadas de
-- preparação directa contra a Coreia do Norte ao longo da DMZ.

-- ===== unit_type próprios (100+20*10 .. 119+20*10 = 300..319) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (300,'K2 Black Panther','ground',5.6,65,2.0,44),
 (301,'K9 Thunder (Artilharia Autopropulsionada)','support',2.4,45,1.5,35),
 (302,'ROK Marines','ground',1.9,45,1.0,30);

INSERT INTO unit_stat VALUES
 (300,'soft_atk',12),(300,'hard_atk',16),(300,'defense',13),(300,'breakthrough',33),(300,'armor',68),(300,'piercing',60),(300,'hardness',0.9),(300,'hp',21),
 (301,'soft_atk',25),(301,'hard_atk',3), (301,'defense',7), (301,'breakthrough',8), (301,'armor',5), (301,'piercing',12),(301,'hardness',0.3),(301,'hp',9),
 (302,'soft_atk',9), (302,'hard_atk',1.5),(302,'defense',23),(302,'breakthrough',12),(302,'armor',0), (302,'piercing',7), (302,'hardness',0.15),(302,'hp',27);

INSERT INTO unit_tag VALUES
 (300,'armored'),(300,'ground'),
 (301,'support'),(301,'ground'),
 (302,'infantry'),(302,'ground'),(302,'especial');

-- ===== espíritos nacionais + modificadores (ids 300..319) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('KOR_conscricao_universal','KOR','Conscrição Universal',
   'O serviço militar obrigatório mantém um exército grande e sempre pronto a mobilizar-se: exército inicial reforçado.'),
 ('KOR_vigilancia_dmz','KOR','Vigilância Permanente da DMZ',
   'Décadas em alerta ao longo da Zona Desmilitarizada tornaram as tropas sul-coreanas particularmente firmes na defesa, sobretudo em montanha.'),
 ('KOR_industria_defesa','KOR','Indústria de Defesa de Ponta',
   'Hyundai Rotem e Hanwha colocam a Coreia do Sul entre os maiores exportadores de armamento do mundo: blindados e artilharia próprios mais eficazes.'),
 ('KOR_alianca_eua','KOR','Aliança com os EUA',
   'O Comando Combinado ROK-EUA garante interoperabilidade e reforço logístico contínuo, melhorando a eficiência de comando.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (300,'spirit',NULL,NULL,           'str_defender',NULL,      'mul',1.10,'KOR','KOR_conscricao_universal'),
 (301,'spirit','terrain','mountain','str_defender',NULL,      'mul',1.20,'KOR','KOR_vigilancia_dmz'),
 (302,'spirit',NULL,NULL,           'str_defender',NULL,      'add',0.10,'KOR','KOR_vigilancia_dmz'),
 (303,'spirit',NULL,NULL,           'str_attacker','armored', 'mul',1.15,'KOR','KOR_industria_defesa'),
 (304,'spirit',NULL,NULL,           'str_attacker',NULL,      'mul',1.10,'KOR','KOR_industria_defesa'),
 (305,'spirit',NULL,NULL,           'command',     NULL,      'mul',1.10,'KOR','KOR_alianca_eua');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('KOR','production_speed',1.10),
 ('KOR','org_regain',1.05),
 ('KOR','start_army_mult',1.3);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('KOR','República presidencialista','Presidente da República da Coreia',
  'Defesa em profundidade ao longo da DMZ, superioridade tecnológica em blindados e artilharia, mobilização rápida por conscrição.',
  'Não-alinhado (tratado bilateral com os EUA)',
  'A Coreia do Sul mantém um dos exércitos mais numerosos e modernos da Ásia, moldado por mais de sete décadas de confronto directo com a Coreia do Norte ao longo da DMZ. A conscrição universal garante um efetivo permanentemente elevado, enquanto uma indústria de defesa de topo (K2, K9) fornece blindados e artilharia entre os mais avançados do mundo. Os Fuzileiros da Marinha da ROK dão-lhe ainda capacidade anfíbia própria.');

-- ===== templates próprios (ids 1+50*10..50+50*10 = 501..550) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (501,'KOR','Divisão Blindada K2'),
 (502,'KOR','Brigada de Fuzileiros'),
 (503,'KOR','Divisão de Artilharia');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (501,300,4),(501,2,2),(501,301,1),(501,5,1),
 (502,302,4),(502,1,2),(502,6,1),
 (503,301,4),(503,1,3),(503,6,1);

-- ===== brigadas/regimentos reais nomeados (ids 501..550) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (504,'KOR','I Corpo (Frente da DMZ Ocidental)','Divisão Blindada K2','Gyeonggi'),
 (505,'KOR','II Corpo (Frente Central)','Infantaria','Gangwon'),
 (506,'KOR','III Corpo (Frente Central-Oeste)','Divisão Blindada K2','Gyeonggi'),
 (507,'KOR','Capital Defense Command','Infantaria','Seoul'),
 (508,'KOR','1ª Divisão de Fuzileiros da ROK','Brigada de Fuzileiros','Incheon'),
 (509,'KOR','2ª Divisão de Fuzileiros da ROK','Brigada de Fuzileiros','South Gyeongsang'),
 (510,'KOR','5ª Divisão de Infantaria','Infantaria','Gangwon'),
 (511,'KOR','6ª Divisão de Infantaria','Infantaria','Gangwon'),
 (512,'KOR','Special Warfare Command','Infantaria AT','Gyeonggi'),
 (513,'KOR','1ª Brigada de Artilharia K9','Divisão de Artilharia','North Gyeongsang'),
 (514,'KOR','7ª Divisão Mecanizada','Mecanizada','North Chungcheong'),
 (515,'KOR','Comando de Defesa de Busan','Infantaria','Busan');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='KOR')
   AND name IN ('Gangwon','North Gyeongsang');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('kor_juche_defesa','KOR','Autonomia na Indústria de Defesa','Seul aposta na autossuficiência tecnológica militar, reduzindo a dependência de fornecedores estrangeiros.',28,NULL,1),
 ('kor_alianca_eua','KOR','Aliança Coreia-EUA','Reforço da cooperação militar com Washington: exercícios conjuntos e partilha de inteligência.',28,NULL,2),
 ('kor_servico_militar','KOR','Reforma do Serviço Militar Obrigatório','Revisão da conscrição face à pressão demográfica e à ameaça do Norte.',21,NULL,3),
 ('kor_k_industria','KOR','Complexo K9/K2: Indústria Blindada','Investimento nas linhas de montagem dos obuses K9 Thunder e dos carros de combate K2 Black Panther.',35,'kor_juche_defesa',4),
 ('kor_add_pesquisa','KOR','Agência de Desenvolvimento de Defesa','Financiamento reforçado à ADD para mísseis, radares e sistemas autónomos de nova geração.',42,'kor_juche_defesa',5),
 ('kor_exportacao_armamento','KOR','Coreia do Sul, Potência Exportadora de Armamento','Contratos com a Polónia, a Austrália e outros clientes impulsionam a produção nacional de armamento.',49,'kor_k_industria',6),
 ('kor_comando_combinado','KOR','Comando Combinado Reforçado','Aprofundamento do Combined Forces Command com os Estados Unidos para resposta rápida na península.',35,'kor_alianca_eua',7),
 ('kor_reserva_mobilizavel','KOR','Reserva Mobilizável de Massa','Modernização do sistema de reservistas para garantir mobilização rápida em caso de guerra.',35,'kor_servico_militar',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('kor_juche_defesa','industry',1.05),
 ('kor_k_industria','industry',1.08),
 ('kor_k_industria','production_speed',1.05),
 ('kor_add_pesquisa','research_speed',1.15),
 ('kor_exportacao_armamento','production_speed',1.10),
 ('kor_exportacao_armamento','industry',1.05),
 ('kor_alianca_eua','org_regain',1.05),
 ('kor_comando_combinado','org_regain',1.10),
 ('kor_comando_combinado','research_speed',1.05),
 ('kor_servico_militar','conscription',1.15),
 ('kor_reserva_mobilizavel','conscription',1.20),
 ('kor_reserva_mobilizavel','org_regain',1.05);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('kor_k_industria','kor_add_pesquisa');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('kor_reserva_mobilizavel','kor_juche_defesa');
