-- GBR (k=6) — Exército Britânico, ordem de batalha aproximada 2024-2026.
-- Fontes: 3 Commando Brigade (Royal Marines, Plymouth), 16 Air Assault Brigade (Paras, Essex),
-- 3rd (UK) Division (12th ABCT Challenger 2/3, 20th Armoured Infantry Bde), 1st (UK) Division
-- (51st Scotland, 160th Wales, 38 Irish, 4th Infantry), London District (Household Division).
-- Carácter: exército pequeno e totalmente profissional, projecção expedicionária (anfíbia e
-- aerotransportada), pilar histórico da NATO, forte tradição de infantaria de montanha... não,
-- de defesa insular densamente urbanizada.

-- ===== unit_type próprios (100+20*6 .. 119+20*6 = 220..239) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (220,'Royal Marines','ground',1.7,45,0.9,32),
 (221,'Paraquedistas (Parachute Regt)','ground',1.6,42,0.9,36),
 (222,'Warrior IFV','ground',2.8,48,1.5,48),
 (223,'Challenger 3','ground',7.0,80,2.3,36);

INSERT INTO unit_stat VALUES
 (220,'soft_atk',8),  (220,'hard_atk',1.5),(220,'defense',23),(220,'breakthrough',12),(220,'armor',0), (220,'piercing',6), (220,'hardness',0.1), (220,'hp',23),
 (221,'soft_atk',7.5),(221,'hard_atk',1),  (221,'defense',21),(221,'breakthrough',12),(221,'armor',0), (221,'piercing',5), (221,'hardness',0.1), (221,'hp',21),
 (222,'soft_atk',10.5),(222,'hard_atk',5), (222,'defense',27),(222,'breakthrough',18),(222,'armor',18),(222,'piercing',22),(222,'hardness',0.55),(222,'hp',31),
 (223,'soft_atk',13), (223,'hard_atk',18), (223,'defense',14),(223,'breakthrough',32),(223,'armor',75),(223,'piercing',65),(223,'hardness',0.92),(223,'hp',22);

INSERT INTO unit_tag VALUES
 (220,'infantry'),(220,'ground'),(220,'especial'),
 (221,'infantry'),(221,'ground'),(221,'especial'),
 (222,'infantry'),(222,'armored'),(222,'ground'),
 (223,'armored'),(223,'ground');

-- ===== espíritos nacionais + modificadores (ids 220..239) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('GBR_tradicao_anfibia','GBR','Tradição Anfíbia',
   'A 3 Commando Brigade dos Royal Marines mantém décadas de treino de assalto anfíbio e operações árticas: as tropas de elite atacam com mais força e o comando conjunto ganha eficiência.'),
 ('GBR_exercito_profissional','GBR','Exército Voluntário Profissional',
   'Sem conscrição desde 1963, o Exército Britânico assenta inteiramente em voluntários bem treinados: menos divisões, mas cada uma defende-se melhor.'),
 ('GBR_lideranca_nato','GBR','Pilar Fundador da NATO',
   'Membro fundador da Aliança e sede de vários quartéis-generais multinacionais: maior eficiência de comando em operações conjuntas.'),
 ('GBR_defesa_urbana','GBR','Ilha Densamente Urbanizada',
   'A maior parte da população britânica vive em grandes áreas metropolitanas: a doutrina de defesa em profundidade urbana torna essas regiões mais difíceis de tomar.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (220,'spirit',NULL,NULL,        'str_attacker','especial','mul',1.20,'GBR','GBR_tradicao_anfibia'),
 (221,'spirit',NULL,NULL,        'command',     NULL,      'mul',1.06,'GBR','GBR_tradicao_anfibia'),
 (222,'spirit',NULL,NULL,        'str_defender',NULL,      'mul',1.10,'GBR','GBR_exercito_profissional'),
 (223,'spirit',NULL,NULL,        'command',     NULL,      'mul',1.15,'GBR','GBR_lideranca_nato'),
 (224,'spirit','terrain','urban','str_defender',NULL,      'mul',1.15,'GBR','GBR_defesa_urbana'),
 (225,'spirit','terrain','urban','str_attacker',NULL,      'add',-0.05,'GBR','GBR_defesa_urbana');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('GBR','production_speed',1.10),
 ('GBR','org_regain',1.20),
 ('GBR','start_army_mult',0.55);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('GBR','Monarquia parlamentar','Primeiro-Ministro e Secretário de Estado da Defesa',
  'Forças expedicionárias profissionais de projecção rápida (anfíbia e aerotransportada), integração total com o comando da NATO.',
  'NATO',
  'O Reino Unido mantém um dos exércitos mais pequenos da sua história, mas totalmente profissional e vocacionado para a projecção de força além-fronteiras — dos Royal Marines aos pára-quedistas do 16 Air Assault. É pilar fundador da NATO e sede de vários comandos multinacionais, com uma indústria de defesa avançada (BAE Systems, Rolls-Royce) e o Challenger como espinha dorsal blindada.');

-- ===== templates próprios (ids 301..350) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (301,'GBR','Brigada Comando'),
 (302,'GBR','Brigada de Assalto Aéreo'),
 (303,'GBR','Brigada Blindada Challenger');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (301,220,4),(301,4,1),(301,5,1),
 (302,221,4),(302,4,1),(302,5,1),
 (303,223,3),(303,222,3),(303,4,1),(303,6,1);

-- ===== brigadas/divisões reais nomeadas (ids 304..350) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (304,'GBR','3 Commando Brigade','Brigada Comando','Plymouth'),
 (305,'GBR','16 Air Assault Brigade','Brigada de Assalto Aéreo','Southend-on-Sea'),
 (306,'GBR','12th Armoured Brigade Combat Team','Brigada Blindada Challenger','Wiltshire'),
 (307,'GBR','20th Armoured Infantry Brigade','Blindada','Wiltshire'),
 (308,'GBR','7th Infantry Brigade','Mecanizada','York'),
 (309,'GBR','51st Infantry Brigade (Escócia)','Infantaria','Edinburgh'),
 (310,'GBR','160th (Wales) Brigade','Infantaria','Cardiff'),
 (311,'GBR','London District (Household Division)','Infantaria','Westminster'),
 (312,'GBR','4th Infantry Brigade','Infantaria','Portsmouth'),
 (313,'GBR','38 (Irish) Brigade','Infantaria','Belfast'),
 (314,'GBR','102nd Logistic Brigade','Infantaria AT','Nottingham'),
 (315,'GBR','3 SCOTS (The Black Watch)','Infantaria','Highland'),
 (316,'GBR','1st Armoured Infantry Brigade','Blindada','Bristol');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('gbr_compromisso_nato','GBR','Compromisso com a Aliança Atlântica','O Reino Unido reforça o seu papel de pilar fundador da NATO, financiando os quartéis-generais multinacionais em solo britânico.',35,NULL,1),
 ('gbr_projecao_expedicionaria','GBR','Força de Projeção Expedicionária','Doutrina de intervenção rápida além-fronteiras, apoiada pelos Royal Marines e pelos pára-quedistas do 16 Air Assault.',35,NULL,2),
 ('gbr_comando_conjunto','GBR','Comando Conjunto Permanente','O Permanent Joint Headquarters de Northwood coordena operações multinacionais com maior eficiência de estado-maior.',42,'gbr_compromisso_nato',3),
 ('gbr_industria_defesa','GBR','BAE Systems e Rolls-Royce','Investimento sustentado na base industrial de defesa nacional acelera a produção de equipamento militar.',49,'gbr_projecao_expedicionaria',4),
 ('gbr_porta_avioes','GBR','Grupo de Ataque HMS Queen Elizabeth','A entrada em pleno serviço dos porta-aviões da classe Queen Elizabeth consolida a capacidade de projeção naval britânica.',56,'gbr_projecao_expedicionaria',5),
 ('gbr_dissuasao_nuclear','GBR','Trident e a Dissuasão Contínua no Mar','A frota de submarinos Vanguard mantém a patrulha ininterrupta da dissuasão nuclear britânica, libertando recursos científicos para outros programas.',63,'gbr_comando_conjunto',6),
 ('gbr_exercito_voluntario','GBR','Exército Voluntário e Reserva','Sem conscrição desde 1963, o recrutamento assenta em campanhas de voluntariado e no reforço do Army Reserve.',28,NULL,7),
 ('gbr_forcas_especiais','GBR','SAS, SBS e o Directorate of Special Forces','O aperfeiçoamento contínuo das forças especiais britânicas eleva a resiliência e a rapidez de reorganização de toda a estrutura de comando.',42,'gbr_exercito_voluntario',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('gbr_compromisso_nato','org_regain',1.10),
 ('gbr_projecao_expedicionaria','production_speed',1.08),
 ('gbr_comando_conjunto','org_regain',1.08),
 ('gbr_industria_defesa','industry',1.10),
 ('gbr_porta_avioes','production_speed',1.10),
 ('gbr_dissuasao_nuclear','research_speed',1.10),
 ('gbr_exercito_voluntario','conscription',1.20),
 ('gbr_forcas_especiais','org_regain',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('gbr_industria_defesa','gbr_porta_avioes');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('gbr_forcas_especiais','gbr_compromisso_nato');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('GBR_adv_almirantado','GBR','seguranca','Lorde do Almirantado','⚓',195,'Os portos da ilha nunca fecham.'),
 ('GBR_adv_colonias','GBR','propaganda','Secretário das Colónias','🏛',190,'Administra meio mundo com uma pasta de couro.');
INSERT INTO advisor_effect VALUES ('GBR_adv_almirantado','port_capacity',1.3);
INSERT INTO advisor_effect VALUES ('GBR_adv_almirantado','defense',1.04);
INSERT INTO advisor_effect VALUES ('GBR_adv_colonias','occupied_yield',1.2);
INSERT INTO advisor_effect VALUES ('GBR_adv_colonias','integration_speed',1.2);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('GBR_commonwealth','Commonwealth','👑',10,'GBR');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('GBR_law_lacos','GBR_commonwealth','Laços simbólicos','Uma coroa, muitos parlamentos e pouco comércio combinado.',0,1,'GBR'),
 ('GBR_law_global','GBR_commonwealth','Grã-Bretanha global','Acordos por todo o lado e uma frota que os acompanha.',1,0,'GBR'),
 ('GBR_law_mercado','GBR_commonwealth','Mercado imperial','Preferência aduaneira dentro da família, tarifa para o resto.',2,0,'GBR');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('GBR_law_lacos','export_share',1.1),
 ('GBR_law_global','export_price',1.1),
 ('GBR_law_global','research_speed',1.05),
 ('GBR_law_mercado','industry',1.08),
 ('GBR_law_mercado','export_share',1.2),
 ('GBR_law_mercado','research_speed',0.97);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('GBR_gen_comando','Chefe dos Comandos','attack',1.15,140,'GBR','👑','Raide nocturno, gente pouca, alvo certo: a escola das operações combinadas.'),
 ('GBR_gen_estado_maior_imp','Estado-Maior Imperial','org_regain',1.15,135,'GBR','🎩','Coordena forças de meio mundo sem que a linha se desencontre.');
