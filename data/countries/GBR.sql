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
 (221,'soft_atk',7.5),(221,'hard_atk',1),  (221,'defense',21),(221,'breakthrough',13),(221,'armor',0), (221,'piercing',5), (221,'hardness',0.1), (221,'hp',21),
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
