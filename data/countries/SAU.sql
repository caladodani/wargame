-- SAU (k=19) — Arábia Saudita: ordem de batalha aproximada 2024-2026. Equipamento ocidental de
-- ponta comprado com receitas petrolíferas (Abrams M1A2S), mas coordenação de comando fragmentada
-- entre o Exército regular e a Guarda Nacional (SANG), força paralela historicamente leal à
-- família real. Defesa aérea densa (Patriot) contra mísseis e drones vindos do Iémen.

-- ===== unit_type próprios (100+20*19 .. 119+20*19 = 480..499) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (480,'M1A2S Abrams','ground',5.5,75,2.3,38),
 (481,'Infantaria da Guarda Nacional (SANG)','ground',1.0,30,1.0,24),
 (482,'Bateria Patriot de Defesa Aérea','support',2.0,45,1.3,20);

INSERT INTO unit_stat VALUES
 (480,'soft_atk',14),(480,'hard_atk',16),(480,'defense',14),(480,'breakthrough',30),(480,'armor',70),(480,'piercing',60),(480,'hardness',0.90),(480,'hp',22),
 (481,'soft_atk',6), (481,'hard_atk',1), (481,'defense',22),(481,'breakthrough',7), (481,'armor',0), (481,'piercing',5), (481,'hardness',0.10),(481,'hp',24),
 (482,'soft_atk',3), (482,'hard_atk',1.5),(482,'defense',12),(482,'breakthrough',2), (482,'armor',7.5),(482,'piercing',15),(482,'hardness',0.35),(482,'hp',10);

INSERT INTO unit_tag VALUES
 (480,'armored'),(480,'ground'),
 (481,'infantry'),(481,'ground'),
 (482,'support'),(482,'ground');

-- ===== espíritos nacionais + modificadores (ids 480..499) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('SAU_petrodolares','SAU','Petrodólares e Equipamento Ocidental',
   'As receitas petrolíferas financiam a compra de equipamento blindado ocidental de topo de gama, dando às forças blindadas uma vantagem qualitativa clara a atacar.'),
 ('SAU_comando_fragmentado','SAU','Comando Fragmentado',
   'A coexistência do Exército regular com a Guarda Nacional, força paralela de lealdade tribal e real, dificulta uma cadeia de comando unificada e eficiente.'),
 ('SAU_defesa_aerea_avancada','SAU','Rede de Defesa Aérea Patriot',
   'Uma densa rede de baterias Patriot, montada para intercetar mísseis e drones houthis, torna as unidades de apoio mais resistentes a defender.'),
 ('SAU_guarda_nacional_paralela','SAU','Guarda Nacional Leal',
   'A Guarda Nacional, recrutada por lealdade tribal à Casa de Saud, defende o território com uma dedicação acima da média.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (480,'spirit',NULL,NULL,'str_attacker','armored', 'mul',1.15,'SAU','SAU_petrodolares'),
 (481,'spirit',NULL,NULL,'command',     NULL,      'mul',0.85,'SAU','SAU_comando_fragmentado'),
 (482,'spirit',NULL,NULL,'str_defender','support', 'mul',1.15,'SAU','SAU_defesa_aerea_avancada'),
 (483,'spirit',NULL,NULL,'str_defender','support', 'add',0.10,'SAU','SAU_defesa_aerea_avancada'),
 (484,'spirit',NULL,NULL,'str_defender','infantry','mul',1.05,'SAU','SAU_guarda_nacional_paralela');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('SAU','production_speed',1.20),
 ('SAU','org_regain',0.95),
 ('SAU','start_army_mult',1.1);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('SAU','Monarquia absoluta','Rei e Príncipe Herdeiro, Ministro da Defesa',
  'Doutrina apoiada no poder de compra: equipamento ocidental de ponta, forte defesa aérea contra mísseis e drones, e uma Guarda Nacional paralela ao Exército regular para a defesa interna e das fronteiras.',
  'Não-alinhado (parceria de segurança estreita com os EUA; membro do Conselho de Cooperação do Golfo)',
  'A Arábia Saudita investe fortemente as receitas petrolíferas em equipamento militar ocidental de topo de gama, incluindo carros de combate Abrams e uma densa rede de defesa aérea Patriot. A eficácia de combate é, no entanto, limitada por uma cadeia de comando fragmentada entre o Exército regular e a Guarda Nacional, força histórica de lealdade tribal à família real, e pela extensão das fronteiras a defender, do Iraque ao Iémen.');

-- ===== templates próprios (ids 1+50*19..50+50*19 = 951..1000) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (951,'SAU','Brigada Blindada Abrams'),
 (952,'SAU','Brigada da Guarda Nacional'),
 (953,'SAU','Bateria de Defesa Aérea Integrada');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (951,480,4),(951,2,2),(951,4,1),
 (952,481,6),(952,4,2),
 (953,482,4),(953,1,2);

-- ===== brigadas reais nomeadas (ids 954..1000) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (954,'SAU','4ª Brigada Blindada','Brigada Blindada Abrams',"Ha'il"),
 (955,'SAU','8ª Brigada Blindada Real','Brigada Blindada Abrams','Ash Sharqiyah'),
 (956,'SAU','10ª Brigada Mecanizada','Mecanizada','Tabuk'),
 (957,'SAU','11ª Brigada Mecanizada','Mecanizada','Al Quassim'),
 (958,'SAU','20ª Brigada Blindada (Fronteira Norte)','Brigada Blindada Abrams','Al Hudud ash Shamaliyah'),
 (959,'SAU','Brigada da Guarda Nacional de Riade','Brigada da Guarda Nacional','Ar Riyad'),
 (960,'SAU','Brigada da Guarda Nacional de Meca','Brigada da Guarda Nacional','Makkah'),
 (961,'SAU','Bateria Patriot da Capital','Bateria de Defesa Aérea Integrada','Ar Riyad'),
 (962,'SAU','Brigada de Fronteira Sul (Najran)','Infantaria','Najran'),
 (963,'SAU','Brigada de Fronteira Sul (Jizan)','Infantaria','Jizan');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('sau_visao_2030','SAU','Visão 2030','Diversificação económica além do petróleo puxa pela indústria nacional.',49,NULL,1),
 ('sau_gign','SAU','Programa GAMI de Indústria Militar','A General Authority for Military Industries nacionaliza fatias da cadeia de defesa.',56,'sau_visao_2030',2),
 ('sau_neom','SAU','Pólo Tecnológico de NEOM','Investimento massivo em tecnologia e automação com aplicação dual civil-militar.',42,'sau_visao_2030',3),
 ('sau_guarda_nacional','SAU','Reforma da Guarda Nacional','Modernização da SANG como pilar de segurança interna e das províncias.',35,NULL,4),
 ('sau_fronteira_sul','SAU','Muralha da Fronteira Sul','Reforço de Najran e Jizan face à instabilidade além-fronteira no Iémen.',35,'sau_guarda_nacional',5),
 ('sau_defesa_aerea','SAU','Escudo Aéreo Integrado','Rede de baterias Patriot e radares protege as instalações petrolíferas críticas.',42,NULL,6),
 ('sau_recrutamento','SAU','Serviço Militar Voluntário Alargado','Campanha de recrutamento e formação alarga a base de reservistas do Reino.',35,'sau_defesa_aerea',7),
 ('sau_academia_rei_khalid','SAU','Academia Militar Rei Khalid','Expansão da formação de oficiais eleva a doutrina e a prontidão das forças.',49,'sau_gign',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('sau_visao_2030','industry',1.10),
 ('sau_gign','industry',1.08),
 ('sau_gign','production_speed',1.05),
 ('sau_neom','research_speed',1.10),
 ('sau_guarda_nacional','conscription',1.10),
 ('sau_fronteira_sul','org_regain',1.08),
 ('sau_defesa_aerea','org_regain',1.06),
 ('sau_recrutamento','conscription',1.12),
 ('sau_academia_rei_khalid','research_speed',1.08);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('sau_gign','sau_neom');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('sau_academia_rei_khalid','sau_guarda_nacional');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('SAU_adv_petroleo','SAU','economia','Ministro do Petróleo','🛢',210,'Fixa o preço do mundo ao pequeno-almoço.'),
 ('SAU_adv_guarda','SAU','seguranca','Comandante da Guarda Nacional','🎖',180,'Tropa fiel e sempre descansada.');
INSERT INTO advisor_effect VALUES ('SAU_adv_petroleo','export_price',1.25);
INSERT INTO advisor_effect VALUES ('SAU_adv_petroleo','industry',1.06);
INSERT INTO advisor_effect VALUES ('SAU_adv_guarda','org_regain',1.12);
