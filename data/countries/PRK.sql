-- PRK (k=11) — Coreia do Norte (Korean People's Army), ordem de batalha aproximada 2024-2026.
-- Fontes: Corpos mecanizados e de infantaria ao longo da DMZ, Comando de Forças Especiais (o maior
-- do mundo em efectivo), artilharia massiva concentrada perto de Kaesong/Pyongyang, T-62/Chonma-ho
-- (blindados obsoletos mas em massa). Carácter: exército enorme por conscrição total, doutrina de
-- massa e artilharia, equipamento antigo e barato, indústria fraca e logística débil.

-- ===== unit_type próprios (100+20*11 .. 119+20*11 = 320..339) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (320,'Chonma-ho (T-62 modificado)','ground',1.7,35,1.6,28),
 (321,'Forças Especiais (KPA SOF)','ground',1.2,30,0.7,26),
 (322,'Artilharia Massiva de Fronteira','support',1.3,30,1.3,18);

INSERT INTO unit_stat VALUES
 (320,'soft_atk',9), (320,'hard_atk',8), (320,'defense',9), (320,'breakthrough',18),(320,'armor',35),(320,'piercing',25),(320,'hardness',0.75),(320,'hp',16),
 (321,'soft_atk',7), (321,'hard_atk',1), (321,'defense',16),(321,'breakthrough',10),(321,'armor',0), (321,'piercing',5), (321,'hardness',0.1), (321,'hp',18),
 (322,'soft_atk',22),(322,'hard_atk',2), (322,'defense',5), (322,'breakthrough',5), (322,'armor',0), (322,'piercing',6), (322,'hardness',0.15),(322,'hp',5);

INSERT INTO unit_tag VALUES
 (320,'armored'),(320,'ground'),
 (321,'infantry'),(321,'ground'),(321,'especial'),
 (322,'support'),(322,'ground');

-- ===== espíritos nacionais + modificadores (ids 320..339) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('PRK_mobilizacao_total','PRK','Mobilização Total',
   'A conscrição obrigatória e as reservas paramilitares (Guarda Vermelha dos Trabalhadores e Camponeses) dão à Coreia do Norte um exército inicial descomunal para a sua economia.'),
 ('PRK_equipamento_obsoleto','PRK','Equipamento Envelhecido',
   'Grande parte do parque blindado é de origem soviética dos anos 60-70, mal manutenida: os blindados próprios atacam com menos convicção.'),
 ('PRK_forcas_especiais','PRK','Maior Comando de Forças Especiais do Mundo',
   'Dezenas de milhares de operacionais treinados para infiltração e sabotagem tornam as unidades especiais norte-coreanas particularmente perigosas a atacar.'),
 ('PRK_isolamento_economico','PRK','Isolamento Económico',
   'Sanções internacionais e uma economia centralizada e fechada atrasam a reposição de baixas: a organização recupera-se mais devagar do que na maioria dos exércitos.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (320,'spirit',NULL,NULL,'command',     NULL,      'mul',0.90,'PRK','PRK_mobilizacao_total'),
 (321,'spirit',NULL,NULL,'str_attacker','armored', 'mul',0.85,'PRK','PRK_equipamento_obsoleto'),
 (322,'spirit',NULL,NULL,'str_defender','armored', 'mul',0.85,'PRK','PRK_equipamento_obsoleto'),
 (323,'spirit',NULL,NULL,'str_attacker','especial','mul',1.25,'PRK','PRK_forcas_especiais'),
 (324,'spirit',NULL,NULL,'str_attacker','especial','add',0.15,'PRK','PRK_forcas_especiais'),
 (325,'spirit',NULL,NULL,'command',     NULL,      'mul',0.90,'PRK','PRK_isolamento_economico');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('PRK','industry',0.5),
 ('PRK','production_speed',0.70),
 ('PRK','org_regain',0.65),
 ('PRK','start_army_mult',2.0);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('PRK','República popular de partido único','Presidente da Comissão de Assuntos de Estado e Comandante Supremo',
  'Doutrina de massa e artilharia: saturar a frente com efetivo e fogo de artilharia, forças especiais para infiltração profunda.',
  'Não-alinhado (cooperação militar com a China e a Rússia)',
  'A Coreia do Norte mantém, proporcionalmente à sua população, um dos maiores exércitos do mundo, sustentado por conscrição praticamente total e por uma vasta reserva paramilitar. A doutrina assenta em massa de infantaria, artilharia concentrada perto da fronteira e um dos maiores comandos de forças especiais do planeta. Em contrapartida, a indústria é fraca, o equipamento blindado é largamente obsoleto e o isolamento económico limita a reposição de perdas.');

-- ===== templates próprios (ids 1+50*11..50+50*11 = 551..600) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (551,'PRK','Divisão Blindada Chonma-ho'),
 (552,'PRK','Brigada de Forças Especiais'),
 (553,'PRK','Divisão de Artilharia de Fronteira');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (551,320,4),(551,1,3),(551,322,1),
 (552,321,5),(552,1,2),
 (553,322,4),(553,1,4);

-- ===== brigadas/regimentos reais nomeados (ids 551..600) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (554,'PRK','I Corpo (Fronteira Oriental)','Infantaria','Kangwŏn-do'),
 (555,'PRK','II Corpo Mecanizado','Divisão Blindada Chonma-ho',"P'yŏngan-namdo"),
 (556,'PRK','IV Corpo (Fronteira Ocidental)','Infantaria',"Hwanghae-namdo"),
 (557,'PRK','V Corpo (Costa Leste)','Infantaria','Kangwŏn-do'),
 (558,'PRK','Comando de Forças Especiais','Brigada de Forças Especiais',"P'yŏngyang"),
 (559,'PRK','Corpo de Artilharia de Kaesong','Divisão de Artilharia de Fronteira',"Hwanghae-bukto"),
 (560,'PRK','Guarda de Pyongyang','Infantaria',"P'yŏngyang"),
 (561,'PRK','VII Corpo (Montanha)','Infantaria','Hamgyŏng-bukto'),
 (562,'PRK','VIII Corpo Mecanizado','Divisão Blindada Chonma-ho',"P'yŏngan-bukto"),
 (563,'PRK','IX Corpo (Costa)','Infantaria','Hamgyŏng-namdo');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='PRK')
   AND name IN ('Chagang-do','Ryanggang');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('prk_juche','PRK','Autossuficiência Juche','A ideologia oficial reforça a produção interna face ao isolamento económico.',35,NULL,1),
 ('prk_mobilizacao','PRK','Mobilização de Massas','O maior exército per capita do mundo alarga o recenseamento e o treino paramilitar.',28,NULL,2),
 ('prk_artilharia_fronteira','PRK','Artilharia da Zona Desmilitarizada','Milhares de peças entrincheiradas junto a Kaesong mantêm Seul sob ameaça permanente.',30,NULL,3),
 ('prk_songun','PRK','Doutrina Songun','O exército em primeiro lugar: os recursos do Estado convergem para as Forças Armadas.',42,'prk_juche',4),
 ('prk_forcas_especiais','PRK','Corpo de Forças Especiais','Infantaria ligeira treinada para infiltração e guerra irregular além da fronteira.',35,'prk_mobilizacao',5),
 ('prk_fortificacao_montanha','PRK','Fortificação Subterrânea','Túneis e bunkers nas cadeias montanhosas protegem tropas e comando de ataques aéreos.',42,'prk_artilharia_fronteira',6),
 ('prk_byungjin','PRK','Linha Byungjin','Desenvolvimento paralelo: a economia civil e o programa de mísseis avançam a par.',49,'prk_songun',7),
 ('prk_industria_militar','PRK','Complexo Industrial-Militar','Fábricas estatais dedicadas a mísseis balísticos e blindados aceleram a produção de armamento.',56,'prk_byungjin',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('prk_juche','industry',1.12),
 ('prk_mobilizacao','conscription',1.20),
 ('prk_artilharia_fronteira','production_speed',1.12),
 ('prk_songun','conscription',1.15),
 ('prk_songun','org_regain',1.05),
 ('prk_forcas_especiais','org_regain',1.10),
 ('prk_fortificacao_montanha','org_regain',1.15),
 ('prk_byungjin','research_speed',1.15),
 ('prk_byungjin','industry',1.10),
 ('prk_industria_militar','production_speed',1.15),
 ('prk_industria_militar','industry',1.08);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('prk_industria_militar','prk_mobilizacao');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('PRK_adv_juche','PRK','propaganda','Comissário do Juche','📣',180,'O país inteiro em pé de guerra desde sempre.'),
 ('PRK_adv_tuneis','PRK','seguranca','Engenheiro dos Túneis','🛠',170,'Uma montanha por dentro vale uma fortaleza.');
INSERT INTO advisor_effect VALUES ('PRK_adv_juche','conscription',1.25);
INSERT INTO advisor_effect VALUES ('PRK_adv_tuneis','defense',1.12);
INSERT INTO advisor_effect VALUES ('PRK_adv_tuneis','counter_intel',1.1);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('PRK_byungjin','Marcha Paralela','☭',10,'PRK');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('PRK_law_economia','PRK_byungjin','Prioridade à economia','Mercados tolerados e fábricas a andar.',0,1,'PRK'),
 ('PRK_law_paralela','PRK_byungjin','Economia e defesa a par','Os dois carris ao mesmo tempo, sem largar nenhum.',1,0,'PRK'),
 ('PRK_law_songun','PRK_byungjin','Primeiro o exército','O quartel come antes da aldeia.',2,0,'PRK');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('PRK_law_economia','industry',1.08),
 ('PRK_law_paralela','production_speed',1.08),
 ('PRK_law_paralela','conscription',1.1),
 ('PRK_law_songun','conscription',1.3),
 ('PRK_law_songun','attack',1.08),
 ('PRK_law_songun','industry',0.9);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('PRK_gen_tunel','Mestre dos Túneis','defense',1.18,120,'PRK','☭','Um exército debaixo de terra que não se bombardeia de cima.'),
 ('PRK_gen_especiais_prk','Chefe das Forças Especiais','move_speed',1.18,130,'PRK','🥋','Cem mil homens treinados para aparecer na retaguarda alheia.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('PRK_escola','Guerra Subterrânea','☭',10,'PRK');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('PRK_doc_tuneis','PRK_escola','Escola dos Túneis','Um exército debaixo de terra não se bombardeia de cima.',50,NULL,1,'PRK'),
 ('PRK_doc_infiltracao','PRK_escola','Infiltração','Cem mil homens treinados para aparecer na retaguarda alheia.',110,'PRK_doc_tuneis',2,'PRK'),
 ('PRK_doc_songun','PRK_escola','Exército Primeiro','O país inteiro é quartel: come-se depois.',190,'PRK_doc_infiltracao',3,'PRK');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('PRK_doc_tuneis','defense',1.09),
 ('PRK_doc_infiltracao','move_speed',1.08),
 ('PRK_doc_infiltracao','attack',1.05),
 ('PRK_doc_songun','conscription',1.12),
 ('PRK_doc_songun','defense',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('PRK_ar','Asas Escondidas','⛰',11,'PRK','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('PRK_ar_hangar','PRK_ar','Hangares na Rocha','Aviões guardados dentro da montanha não se bombardeiam.',50,NULL,1,'PRK'),
 ('PRK_ar_velho','PRK_ar','Frota Antiga Viva','Aparelhos de sessenta anos voam porque alguém decidiu que voam.',110,'PRK_ar_hangar',2,'PRK'),
 ('PRK_ar_noite_prk','PRK_ar','Incursão Nocturna','Um biplano de madeira à noite não aparece em radar nenhum.',185,'PRK_ar_velho',3,'PRK');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('PRK_ar_hangar','air_losses',0.89),
 ('PRK_ar_velho','air_upkeep',0.88),
 ('PRK_ar_noite_prk','air_bombing',1.07);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('PRK_mar','Esquadra Escondida','⛵',12,'PRK','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('PRK_mar_tuneis_mar','PRK_mar','Bases em Túnel','Navios que entram na montanha não aparecem em fotografia nenhuma.',50,NULL,1,'PRK'),
 ('PRK_mar_sang_o','PRK_mar','Submarinos Costeiros','Submarinos pequenos que largam gente na praia do vizinho.',110,'PRK_mar_tuneis_mar',2,'PRK'),
 ('PRK_mar_lancha_prk','PRK_mar','Enxame Costeiro','Muitas lanchas velhas custam pouco e obrigam a olhar para todo o lado.',185,'PRK_mar_sang_o',3,'PRK');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('PRK_mar_tuneis_mar','naval_losses',0.9),
 ('PRK_mar_sang_o','naval_blockade',1.09),
 ('PRK_mar_lancha_prk','naval_upkeep',0.89);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('PRK_ar_gen_povo_prk','Chefe da Asa do Povo','air_upkeep',0.88,145,'PRK','🚀','Esconde os aparelhos na montanha e tira-os de lá a voar.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('PRK_mar_gen_lanchas_prk','Chefe das Lanchas do Litoral','naval_blockade',1.16,145,'PRK','🐟','Barcos pequenos, muitos, e um mar estreito para os usar.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('PRK_tech_ar_bases_tunel','Aviação','Bases Aéreas em Túnel',260,'air_1','Aviões guardados dentro da montanha: o sustento é pouco e o inimigo não os apanha no chão.','PRK'),
 ('PRK_tech_mar_mini_submarinos','Marinha','Frota de Mini-Submarinos',260,'nav_1','Dezenas de submarinos pequenos em água rasa: fecham um mar por medo, não por número.','PRK');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('PRK_tech_ar_bases_tunel','air_upkeep',0.85),
 ('PRK_tech_mar_mini_submarinos','naval_blockade',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'General-Major do Povo',0,0,'PRK'),
 ('exercito',2,'Tenente-General do Povo Coreano',40,0.5,'PRK'),
 ('exercito',3,'Coronel-General do Povo',110,1,'PRK'),
 ('exercito',4,'General do Exército do Povo',220,1.75,'PRK'),
 ('exercito',5,'Marechal da República',360,2.5,'PRK'),
 ('ar',1,'General-Major da Aviação do Povo',0,0,'PRK'),
 ('ar',2,'Tenente-General da Aviação do Povo',40,0.5,'PRK'),
 ('ar',3,'Coronel-General da Aviação do Povo',110,1,'PRK'),
 ('ar',4,'Comandante da Força Aérea do Povo',220,1.75,'PRK'),
 ('ar',5,'Marechal do Ar',360,2.5,'PRK'),
 ('mar',1,'Contra-Almirante do Povo Coreano',0,0,'PRK'),
 ('mar',2,'Vice-Almirante do Povo Coreano',40,0.5,'PRK'),
 ('mar',3,'Almirante do Povo Coreano',110,1,'PRK'),
 ('mar',4,'Comandante da Marinha do Povo Coreano',220,1.75,'PRK'),
 ('mar',5,'Almirante da Frota do Povo',360,2.5,'PRK');

-- ===== condecorações nacionais (medal.country_tag) =====
INSERT INTO medal (id,name,description,metric,threshold,bonus,sort,country_tag) VALUES
 ('PRK_baptismo','Medalha do Guerreiro','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1,'PRK'),
 ('PRK_assalto','Medalha do Assalto Popular','Tomou três regiões ao inimigo.','captures',3,0.03,2,'PRK'),
 ('PRK_campanha','Louvor do Comando Supremo','Quarenta pontos de experiência em combate.','xp',40,0.02,3,'PRK'),
 ('PRK_aco','Ordem da Bandeira Nacional','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4,'PRK'),
 ('PRK_imortais','Título de Herói da República','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5,'PRK');
