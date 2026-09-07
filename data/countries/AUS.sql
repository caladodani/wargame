-- AUS (k=21) — Australian Army, ordem de batalha aproximada 2024-2026.
-- Fontes: 1ª Divisão (Brigadas 1, 3 e 7, regulares), Special Operations Command (SASR, Commandos),
-- M1A1/A2 Abrams (a substituir os M1A1), AS21 Redback / Boxer CRV em introdução. Carácter: exército
-- pequeno, totalmente profissional, altamente expedicionário e interoperável com EUA/Reino Unido
-- (AUKUS, Five Eyes), pensado para projeção de força para fora do continente.

-- ===== unit_type próprios (100+20*21 .. 119+20*21 = 520..539) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (520,'M1A2 Abrams','ground',7.0,70,2.1,42),
 (521,'Boxer CRV','ground',3.2,50,1.5,52),
 (522,'SASR / Commando','ground',1.7,50,0.8,34);

INSERT INTO unit_stat VALUES
 (520,'soft_atk',13),(520,'hard_atk',19),(520,'defense',14),(520,'breakthrough',32),(520,'armor',78),(520,'piercing',70),(520,'hardness',0.93),(520,'hp',22),
 (521,'soft_atk',11),(521,'hard_atk',5), (521,'defense',26),(521,'breakthrough',18),(521,'armor',18),(521,'piercing',22),(521,'hardness',0.55),(521,'hp',30),
 (522,'soft_atk',9), (522,'hard_atk',1.5),(522,'defense',19),(522,'breakthrough',12),(522,'armor',0), (522,'piercing',6), (522,'hardness',0.1), (522,'hp',19);

INSERT INTO unit_tag VALUES
 (520,'armored'),(520,'ground'),
 (521,'armored'),(521,'ground'),
 (522,'infantry'),(522,'ground'),(522,'especial');

-- ===== espíritos nacionais + modificadores (ids 520..539) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('AUS_forcas_profissionais','AUS','Forças Totalmente Profissionais',
   'Sem conscrição, o Exército Australiano assenta em voluntários de longa carreira, bem treinados: maior firmeza defensiva por batalhão, apesar do número reduzido.'),
 ('AUS_doutrina_expedicionaria','AUS','Doutrina Expedicionária',
   'Décadas de operações fora do continente (Timor-Leste, Afeganistão, Iraque) tornam as forças australianas mais eficazes a atacar longe de casa.'),
 ('AUS_aukus_five_eyes','AUS','AUKUS e Five Eyes',
   'A integração profunda com os EUA e o Reino Unido garante partilha de informação e doutrina conjunta, melhorando a eficiência de comando.'),
 ('AUS_isolamento_continental','AUS','Isolamento Continental',
   'A imensidão do território e a baixa densidade populacional fora das capitais limitam a mobilização inicial: exército inicial abaixo da média mundial.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (520,'spirit',NULL,NULL,'str_defender',NULL,      'mul',1.12,'AUS','AUS_forcas_profissionais'),
 (521,'spirit',NULL,NULL,'str_attacker',NULL,      'mul',1.10,'AUS','AUS_doutrina_expedicionaria'),
 (522,'spirit',NULL,NULL,'str_attacker','especial','mul',1.20,'AUS','AUS_doutrina_expedicionaria'),
 (523,'spirit',NULL,NULL,'command',     NULL,      'mul',1.12,'AUS','AUS_aukus_five_eyes'),
 (524,'spirit',NULL,NULL,'command',     NULL,      'mul',0.95,'AUS','AUS_isolamento_continental');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('AUS','production_speed',1.05),
 ('AUS','org_regain',1.15),
 ('AUS','start_army_mult',0.55);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('AUS','Monarquia constitucional parlamentar (Commonwealth)','Primeiro-Ministro da Austrália',
  'Forças pequenas, profissionais e projetáveis: capacidade expedicionária conjunta com EUA e Reino Unido, defesa do continente por superioridade tecnológica.',
  'Não-alinhado (AUKUS, Five Eyes, parceiro próximo da NATO)',
  'A Austrália mantém um exército pequeno mas inteiramente profissional, historicamente vocacionado para operações expedicionárias ao lado dos aliados anglo-saxónicos. A renovação de blindados (M1A2 Abrams, Boxer CRV, AS21 Redback) e a integração profunda em redes de informação como o AUKUS e o Five Eyes compensam um efetivo modesto face à vastidão do território. As forças especiais (SASR, Commandos) têm reputação de elite internacional.');

-- ===== templates próprios (ids 1+50*21..50+50*21 = 1051..1100) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1051,'AUS','Brigada Blindada Abrams'),
 (1052,'AUS','Brigada Motorizada Boxer'),
 (1053,'AUS','Grupo de Operações Especiais');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1051,520,4),(1051,521,2),(1051,4,1),
 (1052,521,4),(1052,1,2),(1052,6,1),
 (1053,522,4),(1053,1,2);

-- ===== brigadas/regimentos reais nomeados (ids 1051..1100) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1054,'AUS','1ª Brigada Blindada','Brigada Blindada Abrams','Northern Territory'),
 (1055,'AUS','3ª Brigada (Motorizada)','Brigada Motorizada Boxer','Queensland'),
 (1056,'AUS','7ª Brigada (Mecanizada)','Brigada Motorizada Boxer','Queensland'),
 (1057,'AUS','SASR (Special Air Service Regiment)','Grupo de Operações Especiais','Western Australia'),
 (1058,'AUS','2º Regimento de Commandos','Grupo de Operações Especiais','New South Wales'),
 (1059,'AUS','8ª/9ª Brigada de Infantaria (Reserva)','Infantaria','Victoria'),
 (1060,'AUS','4ª Brigada (Reserva)','Infantaria','South Australia'),
 (1061,'AUS','11ª Brigada (Defesa do Norte)','Infantaria','Queensland'),
 (1062,'AUS','13ª Brigada (Reserva Ocidental)','Infantaria','Western Australia');

-- ===== correcção de terreno =====
UPDATE region SET terrain='desert'
 WHERE owner_id=(SELECT id FROM country WHERE tag='AUS')
   AND name IN ('Northern Territory','Western Australia','South Australia');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('aus_forward_defence','AUS','Defesa Avançada do Norte','Doutrina de Defesa Avançada: radares e bases no Top End vigiam os acessos do arquipélago indonésio.',35,NULL,1),
 ('aus_anzus','AUS','Pilar ANZUS','Aprofundar a aliança com os EUA: interoperabilidade, bases rotativas e partilha de informações.',42,'aus_forward_defence',2),
 ('aus_aukus','AUS','Pacto AUKUS','Submarinos de propulsão nuclear e tecnologia avançada partilhada com Washington e Londres.',63,'aus_anzus',3),
 ('aus_ran_expansion','AUS','Frota de Superfície Contínua','Programa naval contínuo: fragatas Hunter e destroyers Hobart saem dos estaleiros de Adelaide.',49,'aus_anzus',4),
 ('aus_pacific_step_up','AUS','Ascensão no Pacífico','Diplomacia e ajuda às nações insulares do Pacífico Sul para conter influência rival na região.',35,NULL,5),
 ('aus_outback_industry','AUS','Indústria do Outback','Minério e recursos do interior financiam a reindustrialização da base de defesa nacional.',49,NULL,6),
 ('aus_sovereign_industry','AUS','Capacidade Soberana de Defesa','Investimento em Williamtown e Bendigo para produzir munições e blindados em solo australiano.',56,'aus_outback_industry',7),
 ('aus_reserve_call','AUS','Chamada da Reserva','Recrutamento reforçado nas forças de reserva estaduais para engrossar as fileiras do ADF.',28,NULL,8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('aus_forward_defence','org_regain',1.06),
 ('aus_anzus','research_speed',1.08),
 ('aus_aukus','research_speed',1.10),
 ('aus_ran_expansion','production_speed',1.12),
 ('aus_pacific_step_up','industry',1.05),
 ('aus_outback_industry','industry',1.10),
 ('aus_sovereign_industry','production_speed',1.08),
 ('aus_reserve_call','conscription',1.20);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('aus_aukus','aus_ran_expansion');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('AUS_adv_minas','AUS','economia','Senhor das Minas','⛏',190,'O minério dele chega a três continentes.'),
 ('AUS_adv_costa','AUS','seguranca','Guardião da Costa','⚓',175,'Cais abertos de um oceano ao outro.');
INSERT INTO advisor_effect VALUES ('AUS_adv_minas','industry',1.13);
INSERT INTO advisor_effect VALUES ('AUS_adv_minas','export_price',1.1);
INSERT INTO advisor_effect VALUES ('AUS_adv_costa','port_capacity',1.22);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('AUS_pacifico','Aliança do Pacífico','🦘',10,'AUS');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('AUS_law_continental','AUS_pacifico','Defesa continental','O deserto e o mar fazem o trabalho; a força fica em casa.',0,1,'AUS'),
 ('AUS_law_submarinos','AUS_pacifico','Submarinos e tecnologia aliada','Estaleiros e laboratórios trabalham com Londres e Washington.',1,0,'AUS'),
 ('AUS_law_expedicionario','AUS_pacifico','Expedicionário no Índico','A frota patrulha longe de casa e a tropa embarca com ela.',2,0,'AUS');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('AUS_law_continental','defense',1.08),
 ('AUS_law_submarinos','research_speed',1.1),
 ('AUS_law_submarinos','industry',1.05),
 ('AUS_law_expedicionario','attack',1.08),
 ('AUS_law_expedicionario','org_regain',1.06);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('AUS_gen_deserto','Comandante do Interior','move_speed',1.18,125,'AUS','🦘','Move uma brigada por mil quilómetros de nada e chega com ela inteira.'),
 ('AUS_gen_anzac','Herdeiro do ANZAC','attack',1.13,140,'AUS','🎖','A tradição do corpo expedicionário: embarca-se e vai-se, longe.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('AUS_escola','Escola do Interior','🦘',10,'AUS');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('AUS_doc_outback','AUS_escola','Marcha do Interior','Mil quilómetros de nada exigem colunas que se abastecem sozinhas.',50,NULL,1,'AUS'),
 ('AUS_doc_anzac','AUS_escola','Corpo Expedicionário','A tradição do ANZAC: embarca-se, vai-se longe e combate-se com quem lá está.',110,'AUS_doc_outback',2,'AUS'),
 ('AUS_doc_pacifico','AUS_escola','Guarda do Pacífico','Um continente vazio defende-se no mar dos outros, não na praia própria.',190,'AUS_doc_anzac',3,'AUS');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('AUS_doc_outback','move_speed',1.07),
 ('AUS_doc_anzac','move_speed',1.06),
 ('AUS_doc_anzac','attack',1.06),
 ('AUS_doc_pacifico','attack',1.08),
 ('AUS_doc_pacifico','defense',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('AUS_ar','Asas do Interior','🦅',11,'AUS','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('AUS_ar_deserto_ar','AUS_ar','Voo do Deserto','Mil quilómetros sem aeródromo obrigam a poupar tudo, sobretudo o motor.',50,NULL,1,'AUS'),
 ('AUS_ar_vigilancia','AUS_ar','Vigilância Marítima','O continente vê-se do ar antes de se ver do mar.',110,'AUS_ar_deserto_ar',2,'AUS'),
 ('AUS_ar_coalizao','AUS_ar','Voo em Coligação','Encaixar na força aérea dos outros é meia força aérea de graça.',185,'AUS_ar_vigilancia',3,'AUS');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('AUS_ar_deserto_ar','air_upkeep',0.92),
 ('AUS_ar_vigilancia','air_losses',0.94),
 ('AUS_ar_coalizao','air_bombing',1.08),
 ('AUS_ar_coalizao','air_upkeep',0.95);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('AUS_mar','Guarda do Pacífico','🌊',12,'AUS','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('AUS_mar_coral','AUS_mar','Herança do Mar de Coral','A batalha em que as esquadras não se viram mudou a maneira de as usar.',50,NULL,1,'AUS'),
 ('AUS_mar_fragata','AUS_mar','Escola de Fragatas','Poucos navios, todos a andar, nenhum parado por avaria.',110,'AUS_mar_coral',2,'AUS'),
 ('AUS_mar_sea_lanes','AUS_mar','Guarda das Rotas','Uma ilha grande vive das rotas: cortadas elas, acabou.',185,'AUS_mar_fragata',3,'AUS');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('AUS_mar_coral','naval_losses',0.93),
 ('AUS_mar_fragata','naval_upkeep',0.91),
 ('AUS_mar_sea_lanes','naval_escort',1.1);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('AUS_ar_gen_norte','Chefe do Comando do Norte','air_bombing',1.14,145,'AUS','🪁','Chega onde ninguém julga que se chega e volta com o depósito seco.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('AUS_mar_gen_coral','Comodoro do Mar de Coral','naval_escort',1.16,145,'AUS','🐬','Leva comboios por mares onde o socorro fica a três dias.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('AUS_tech_ar_mar_de_coral','Aviação','Vigilância do Mar de Coral',260,'air_1','Alerta aéreo a milhares de quilómetros de tudo: ninguém entra sem ser visto primeiro.','AUS'),
 ('AUS_tech_mar_anzac','Marinha','Fragatas Anzac',260,'nav_1','Escolta de comboios entre continentes, que é a única maneira de abastecer uma ilha grande.','AUS');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('AUS_tech_ar_mar_de_coral','air_losses',0.87),
 ('AUS_tech_mar_anzac','naval_escort',1.18);
