-- ISR (k=18) — Israel: Forças de Defesa de Israel (IDF), ordem de batalha aproximada 2024-2026.
-- País pequeno mas com um dos exércitos mais qualificados do mundo, sustentado por um sistema de
-- reservas capaz de multiplicar o exército em dias. Blindados Merkava concebidos para a
-- sobrevivência da tripulação, brigadas de infantaria de elite (Golani, Givati) e uma robusta
-- tradição de forças pára-quedistas e especiais.

-- ===== unit_type próprios (100+20*18 .. 119+20*18 = 460..479) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (460,'Merkava Mk4','ground',5.2,70,2.4,38),
 (461,'Infantaria Golani/Givati','ground',1.3,32,1.0,26),
 (462,'Pára-quedistas (Tzanchanim)','ground',1.5,35,0.9,34);

INSERT INTO unit_stat VALUES
 (460,'soft_atk',16),(460,'hard_atk',18),(460,'defense',16),(460,'breakthrough',34),(460,'armor',75),(460,'piercing',70),(460,'hardness',0.92),(460,'hp',24),
 (461,'soft_atk',9), (461,'hard_atk',1), (461,'defense',28),(461,'breakthrough',11),(461,'armor',0), (461,'piercing',6), (461,'hardness',0.10),(461,'hp',27),
 (462,'soft_atk',8), (462,'hard_atk',1.5),(462,'defense',20),(462,'breakthrough',12),(462,'armor',0), (462,'piercing',7.5),(462,'hardness',0.10),(462,'hp',22);

INSERT INTO unit_tag VALUES
 (460,'armored'),(460,'ground'),
 (461,'infantry'),(461,'ground'),(461,'elite'),
 (462,'infantry'),(462,'ground'),(462,'especial');

-- ===== espíritos nacionais + modificadores (ids 460..479) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('ISR_forcas_reserva','ISR','Sistema de Reservas',
   'Um sistema de mobilização de reservas treinado e testado permite a Israel expandir e reorganizar o exército com uma eficiência de comando pouco comum.'),
 ('ISR_qualidade_sobre_quantidade','ISR','Qualidade sobre Quantidade',
   'A doutrina israelita prioriza o treino intensivo e a qualidade humana sobre a massa: as unidades de elite atacam com clara superioridade.'),
 ('ISR_merkava_blindados','ISR','Blindados Merkava',
   'O Merkava foi concebido em torno da sobrevivência da tripulação, com o motor à frente e compartimentos blindados: os blindados israelitas resistem melhor a defender.'),
 ('ISR_paraquedistas_infiltracao','ISR','Tradição Pára-quedista',
   'Uma longa tradição de operações de infiltração e assalto vertical torna as unidades pára-quedistas particularmente eficazes a atacar.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (460,'spirit',NULL,NULL,'command',     NULL,      'mul',1.10,'ISR','ISR_forcas_reserva'),
 (461,'spirit',NULL,NULL,'str_attacker','elite',   'mul',1.25,'ISR','ISR_qualidade_sobre_quantidade'),
 (462,'spirit',NULL,NULL,'str_attacker','elite',   'add',0.15,'ISR','ISR_qualidade_sobre_quantidade'),
 (463,'spirit',NULL,NULL,'str_defender','armored', 'mul',1.15,'ISR','ISR_merkava_blindados'),
 (464,'spirit',NULL,NULL,'str_attacker','especial','mul',1.20,'ISR','ISR_paraquedistas_infiltracao');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('ISR','production_speed',1.10),
 ('ISR','org_regain',1.30),
 ('ISR','start_army_mult',1.6);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('ISR','Democracia parlamentar','Primeiro-Ministro e Chefe do Estado-Maior General',
  'Doutrina de armas combinadas, superioridade aérea e mobilização rápida de reservas: guerras curtas e decisivas, com blindados concebidos para a sobrevivência da tripulação e infantaria de elite treinada para operar em múltiplas frentes.',
  'Não-alinhado (parceria estratégica de longa data com os EUA)',
  'Apesar do território reduzido, Israel mantém umas das forças armadas mais qualificadas e tecnologicamente avançadas do mundo. O sistema de reservas permite multiplicar rapidamente o exército permanente em caso de mobilização geral, enquanto brigadas de infantaria como a Golani e a Givati, o corpo pára-quedista e o blindado Merkava sustentam uma doutrina assente na qualidade e na velocidade de resposta.');

-- ===== templates próprios (ids 1+50*18..50+50*18 = 901..950) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (901,'ISR','Brigada Blindada Merkava'),
 (902,'ISR','Brigada Golani/Givati'),
 (903,'ISR','Brigada Pára-quedista');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (901,460,4),(901,2,2),(901,4,1),
 (902,461,5),(902,4,2),
 (903,462,5),(903,4,1);

-- ===== brigadas reais nomeadas (ids 904..950) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (904,'ISR','Brigada Golani','Brigada Golani/Givati','HaZafon'),
 (905,'ISR','Brigada Givati','Brigada Golani/Givati','HaDarom'),
 (906,'ISR','Brigada Pára-quedista 35','Brigada Pára-quedista','HaMerkaz'),
 (907,'ISR','7ª Brigada Blindada','Brigada Blindada Merkava','HaZafon'),
 (908,'ISR','401ª Brigada Blindada','Brigada Blindada Merkava','HaDarom'),
 (909,'ISR','Brigada Kfir','Infantaria','Jerusalem'),
 (910,'ISR','Brigada Nahal','Infantaria','HaMerkaz'),
 (911,'ISR','Comando Sayeret Matkal','Brigada Pára-quedista','Tel Aviv');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('isr_doutrina_seguranca','ISR','Doutrina de Segurança Nacional','Revisão da doutrina de defesa: dissuasão, alerta antecipado e decisão rápida.',28,NULL,1),
 ('isr_industria_defesa','ISR','Polo de Indústria de Defesa','IAI, Elbit e Rafael expandem linhas de produção com apoio do Estado.',42,NULL,2),
 ('isr_reserva_nacional','ISR','Exército do Povo','Reforço do sistema de reservistas (Miluim), coluna vertebral da mobilização.',35,NULL,3),
 ('isr_cupula_ferro','ISR','Rede de Defesa Aérea em Camadas','Integração de Cúpula de Ferro, Funda de David e Flecha num comando único.',49,'isr_doutrina_seguranca',4),
 ('isr_iron_fist','ISR','Blindados de Nova Geração','Merkava Barak e módulos de proteção ativa Trophy equipam as brigadas blindadas.',56,'isr_industria_defesa',5),
 ('isr_ciber_8200','ISR','Ciberdefesa da Unidade 8200','Investimento em guerra eletrónica e ciberdefesa a partir da experiência da Unidade 8200.',35,'isr_industria_defesa',6),
 ('isr_fronteira_sul','ISR','Barreira do Negev e Sinai','Reforço da vigilância e fortificação da fronteira sul face ao contrabando e infiltrações.',28,'isr_reserva_nacional',7),
 ('isr_forcas_especiais','ISR','Elite das Forças Especiais','Sayeret Matkal e unidades irmãs recebem equipamento e treino de vanguarda.',42,'isr_reserva_nacional',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('isr_doutrina_seguranca','org_regain',1.08),
 ('isr_industria_defesa','industry',1.10),
 ('isr_reserva_nacional','conscription',1.20),
 ('isr_cupula_ferro','org_regain',1.10),
 ('isr_iron_fist','production_speed',1.12),
 ('isr_ciber_8200','research_speed',1.15),
 ('isr_fronteira_sul','conscription',1.10),
 ('isr_forcas_especiais','org_regain',1.05),
 ('isr_forcas_especiais','research_speed',1.06);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('isr_iron_fist','isr_ciber_8200'),
 ('isr_fronteira_sul','isr_forcas_especiais');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('isr_forcas_especiais','isr_doutrina_seguranca');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('ISR_adv_servicos','ISR','seguranca','Chefe dos Serviços','🕵',200,'Sabe do inimigo antes de o inimigo saber.'),
 ('ISR_adv_oficinas','ISR','ciencia','Fundador de Oficinas','💡',195,'Meia dúzia de homens e uma ideia por semana.');
INSERT INTO advisor_effect VALUES ('ISR_adv_servicos','counter_intel',1.4);
INSERT INTO advisor_effect VALUES ('ISR_adv_oficinas','research_speed',1.2);
