-- PAK (k=17) — Paquistão: sétimo maior exército do mundo em efectivo, ordem de batalha
-- aproximada 2024-2026. Nove Corpos de Exército cobrem a fronteira com a Índia (Punjab, Sind) e
-- a fronteira noroeste montanhosa com o Afeganistão. Doutrina de infantaria de massa e defesa de
-- montanha, blindados nacionais Al-Khalid (co-produzidos com a China) e um Grupo de Serviços
-- Especiais (SSG) de referência regional.

-- ===== unit_type próprios (100+20*17 .. 119+20*17 = 440..459) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (440,'Al-Khalid (MBT)','ground',4.6,65,2.0,42),
 (441,'Infantaria de Montanha (Fronteira NO)','ground',1.1,30,0.8,20),
 (442,'Forças Especiais SSG','ground',1.4,35,0.8,28);

INSERT INTO unit_stat VALUES
 (440,'soft_atk',14),(440,'hard_atk',17),(440,'defense',14),(440,'breakthrough',30),(440,'armor',65),(440,'piercing',60),(440,'hardness',0.90),(440,'hp',22),
 (441,'soft_atk',7), (441,'hard_atk',1), (441,'defense',30),(441,'breakthrough',9), (441,'armor',0), (441,'piercing',5), (441,'hardness',0.10),(441,'hp',27),
 (442,'soft_atk',9), (442,'hard_atk',1.5),(442,'defense',18),(442,'breakthrough',12),(442,'armor',0), (442,'piercing',7.5),(442,'hardness',0.15),(442,'hp',20);

INSERT INTO unit_tag VALUES
 (440,'armored'),(440,'ground'),
 (441,'infantry'),(441,'ground'),(441,'montanha'),
 (442,'infantry'),(442,'ground'),(442,'especial');

-- ===== espíritos nacionais + modificadores (ids 440..459) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('PAK_infantaria_montanha','PAK','Infantaria de Fronteira de Montanha',
   'Décadas de operações na fronteira noroeste montanhosa deram à infantaria paquistanesa destacada aí uma vantagem clara, tanto a defender como a atacar em terreno de montanha.'),
 ('PAK_al_khalid_nacional','PAK','Orgulho Blindado Nacional (Al-Khalid)',
   'O Al-Khalid, co-desenvolvido com a China, é o símbolo da indústria de defesa paquistanesa: as tripulações que o operam recebem treino e manutenção prioritários.'),
 ('PAK_forcas_especiais_ssg','PAK','Grupo de Serviços Especiais',
   'O SSG ("Cegonhas Negras") é uma das forças especiais mais treinadas da região, especializada em operações de infiltração rápida.'),
 ('PAK_exercito_de_massa','PAK','Exército de Conscrição em Massa',
   'Um efectivo enorme para a economia do país torna a coordenação entre corpos de exército mais lenta e burocrática.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (440,'spirit','terrain','mountain','str_defender','montanha','mul',1.20,'PAK','PAK_infantaria_montanha'),
 (441,'spirit','terrain','mountain','str_attacker','montanha','mul',1.10,'PAK','PAK_infantaria_montanha'),
 (442,'spirit',NULL,     NULL,      'str_attacker','armored', 'mul',1.10,'PAK','PAK_al_khalid_nacional'),
 (443,'spirit',NULL,     NULL,      'str_defender','armored', 'add',0.05,'PAK','PAK_al_khalid_nacional'),
 (444,'spirit',NULL,     NULL,      'str_attacker','especial','mul',1.20,'PAK','PAK_forcas_especiais_ssg'),
 (445,'spirit',NULL,     NULL,      'str_attacker','especial','add',0.10,'PAK','PAK_forcas_especiais_ssg'),
 (446,'spirit',NULL,     NULL,      'command',     NULL,      'mul',0.92,'PAK','PAK_exercito_de_massa');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('PAK','production_speed',0.90),
 ('PAK','org_regain',0.90),
 ('PAK','start_army_mult',1.4);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('PAK','República Islâmica federal (com forte peso militar)','Chefe do Estado-Maior do Exército',
  'Doutrina de infantaria de massa e defesa em profundidade nas fronteiras com a Índia e o Afeganistão, blindados nacionais para contra-ataques rápidos e forças especiais de elite para operações de infiltração.',
  'Não-alinhado (cooperação estratégica de longa data com a China; laços históricos com os EUA)',
  'O Paquistão mantém um dos maiores exércitos do mundo em efectivo, organizado em nove Corpos de Exército distribuídos entre a fronteira oriental com a Índia, densamente povoada, e a fronteira noroeste montanhosa com o Afeganistão. O blindado nacional Al-Khalid, co-produzido com a China, é a espinha dorsal das forças mecanizadas, enquanto o Grupo de Serviços Especiais (SSG) mantém uma reputação regional de elite.');

-- ===== templates próprios (ids 1+50*17..50+50*17 = 851..900) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (851,'PAK','Divisão de Infantaria de Montanha'),
 (852,'PAK','Regimento Blindado Al-Khalid'),
 (853,'PAK','Grupo de Forças Especiais SSG');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (851,441,5),(851,1,2),(851,4,2),
 (852,440,4),(852,2,2),(852,4,1),
 (853,442,4),(853,1,2);

-- ===== brigadas/corpos reais nomeados (ids 854..900) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (854,'PAK','I Corpo (Mangla)','Regimento Blindado Al-Khalid','Punjab'),
 (855,'PAK','II Corpo (Multan)','Infantaria','Punjab'),
 (856,'PAK','IV Corpo (Lahore)','Infantaria','Punjab'),
 (857,'PAK','V Corpo (Karachi)','Infantaria','Sind'),
 (858,'PAK','X Corpo (Rawalpindi, QG)','Infantaria','F.C.T.'),
 (859,'PAK','XI Corpo (Peshawar)','Divisão de Infantaria de Montanha','K.P.'),
 (860,'PAK','XII Corpo (Quetta)','Infantaria','Baluchistan'),
 (861,'PAK','Força de Reacção Rápida SSG (Cherat)','Grupo de Forças Especiais SSG','K.P.'),
 (862,'PAK','Comando do Norte (Gilgit-Baltistan)','Divisão de Infantaria de Montanha','Northern Areas'),
 (863,'PAK','XXX Corpo (Gujranwala)','Regimento Blindado Al-Khalid','Punjab');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='PAK')
   AND name IN ('Azad Kashmir','F.A.T.A.','Northern Areas');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('pak_ciec','PAK','Corredor Económico Pak-China','O CPEC injeta capital chinês em estradas, energia e portos ao longo do território.',35,NULL,1),
 ('pak_defesa_indigena','PAK','Autossuficiência Industrial de Defesa','Substituição de importações: motores, blindagem e munições passam a ser produzidos em casa.',42,NULL,2),
 ('pak_reserva_territorial','PAK','Milícia e Reserva Territorial','Recrutamento territorial e milícias tribais reforçam a mobilização em tempo de guerra.',28,NULL,3),
 ('pak_gwadar','PAK','Porto de Gwadar e Zona Franca','O porto de Gwadar e a sua zona franca tornam-se o motor logístico do Corredor.',35,'pak_ciec',4),
 ('pak_jf17','PAK','Linha de Produção do JF-17 Thunder','A parceria com a China acelera o fabrico e o desenvolvimento do caça JF-17 Thunder.',49,'pak_defesa_indigena',5),
 ('pak_alkhalid','PAK','Cadeia de Montagem do Al-Khalid','A Heavy Industries Taxila põe a cadeia de montagem do carro de combate Al-Khalid a ritmo de guerra.',42,'pak_jf17',6),
 ('pak_spd','PAK','Divisão de Planos Estratégicos','A Strategic Plans Division coordena investigação e doutrina nuclear e convencional.',35,'pak_defesa_indigena',7),
 ('pak_forca_fronteira','PAK','Corpo de Guardas de Fronteira','O Corpo de Guardas de Fronteira absorve e recompõe unidades desgastadas ao longo da Linha Durand.',28,'pak_reserva_territorial',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('pak_ciec','industry',1.08),
 ('pak_defesa_indigena','production_speed',1.08),
 ('pak_reserva_territorial','conscription',1.20),
 ('pak_gwadar','industry',1.10),
 ('pak_jf17','production_speed',1.10),
 ('pak_jf17','research_speed',1.05),
 ('pak_alkhalid','production_speed',1.12),
 ('pak_spd','research_speed',1.10),
 ('pak_forca_fronteira','org_regain',1.06),
 ('pak_forca_fronteira','conscription',1.08);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('pak_jf17','pak_spd');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('pak_forca_fronteira','pak_ciec');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('PAK_adv_canais','PAK','economia','Engenheiro dos Canais','💧',165,'Água onde não havia, fábricas a seguir.'),
 ('PAK_adv_interarmas','PAK','seguranca','Chefe dos Serviços Inter-Armas','🕵',180,'Manda mais do que quem manda.');
INSERT INTO advisor_effect VALUES ('PAK_adv_canais','industry',1.11);
INSERT INTO advisor_effect VALUES ('PAK_adv_interarmas','counter_intel',1.3);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('PAK_corredor','Corredor Económico','🌙',10,'PAK');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('PAK_law_portagens','PAK_corredor','Portagens do corredor','A carga passa e deixa taxa em cada província.',0,1,'PAK'),
 ('PAK_law_gwadar','PAK_corredor','Obra de Gwadar','Porto de águas profundas, estrada e via-férrea de uma vez.',1,0,'PAK'),
 ('PAK_law_guarnicao','PAK_corredor','Guarnição do corredor','Uma divisão inteira só para guardar a estrada.',2,0,'PAK');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('PAK_law_portagens','industry',1.05),
 ('PAK_law_portagens','export_share',1.1),
 ('PAK_law_gwadar','industry',1.08),
 ('PAK_law_gwadar','production_speed',1.05),
 ('PAK_law_guarnicao','defense',1.1),
 ('PAK_law_guarnicao','counter_intel',1.1);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('PAK_gen_montanha_pak','Comandante das Montanhas','defense',1.16,130,'PAK','🌙','A fronteira norte é um labirinto de pedra e ele tem o mapa na cabeça.'),
 ('PAK_gen_corpo_choque','Chefe do Corpo de Choque','attack',1.14,140,'PAK','⚔','A reserva blindada da planície, guardada para um só golpe.');
