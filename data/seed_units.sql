-- Números calibrados em combat_sim.py
INSERT INTO terrain (id,name,color,move_cost,glyph) VALUES
 ('plain','Planície','#c8d8a0',1.0,'campo'),('forest','Floresta','#4f7942',1.5,'arvore'),('urban','Urbano','#888888',1.2,'cidade'),
 ('mountain','Montanha','#a08060',2.0,'montanha'),('desert','Deserto','#e0c080',1.3,'duna'),('tundra','Tundra','#dfe8ee',1.8,'gelo');

-- Constantes de jogo. Referência HoI4: divisões cruzam uma província em dias, não horas; produção lenta.
INSERT INTO rule (key,value,note) VALUES
 ('move_base_days',80,'dias para entrar numa região = base / mobilidade × move_cost do terreno / infraestrutura'),
 ('points_per_million',0.1,'pontos de produção por dia por milhão de habitantes numa região controlada'),
 ('occupied_yield',0.5,'fração dos pontos que uma região ocupada (controlador ≠ dono) rende'),
 ('build_min_days',10,'uma encomenda nunca fica pronta em menos dias que isto (gasto diário máximo = custo/este valor)'),
 ('new_division_org',40,'organização com que uma divisão sai da fábrica'),
 ('supply_pocket',0.5,'supply de uma divisão sem ligação por terra a território próprio (bolsa)'),
 ('supply_stack',6,'divisões por região sem penalização de supply; acima, supply × (este valor / n)'),
 ('pocket_grace',3,'dias cortada da retaguarda antes de o cerco começar a cobrar (PocketSystem)'),
 ('pocket_attrition',4,'efectivo que uma divisão cercada perde por dia, passado o respiro'),
 ('pocket_org',8,'organização que uma divisão cercada perde por dia, passado o respiro'),
 ('pocket_surrender',21,'dias com a bolsa fechada até a divisão baixar as armas (0 = nunca se rende)'),
 ('pocket_prisoner_share',0.9,'fração do efectivo de uma divisão rendida que vai parar aos campos de quem cercou'),
 ('ai_period_days',3,'a IA decide de X em X dias'),
 ('ai_attack_ratio',1.5,'a IA só ataca com este rácio de divisões vs defensores'),
 ('ai_max_queue',3,'encomendas em fila que a IA mantém'),
 ('start_div_per_million',0.25,'divisões iniciais por milhão de habitantes (seed_armies.py)'),
 ('start_div_min',2,'mínimo de divisões iniciais para países com população'),
 ('start_div_max',120,'tecto de divisões iniciais por país');

INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (1,'Infantaria','ground',1.0,30,1.0,25),
 (2,'Mecanizada','ground',2.5,45,1.6,45),
 (3,'Blindados','ground',4.0,60,2.2,40),
 (4,'Artilharia','support',1.8,40,1.4,25),
 (5,'AA/Anti-drone','support',1.5,40,1.1,25),
 (6,'Anti-tanque','support',1.6,40,1.2,25);

INSERT INTO unit_stat VALUES
 (1,'soft_atk',6),(1,'hard_atk',1),(1,'defense',24),(1,'breakthrough',8),(1,'armor',0),(1,'piercing',5),(1,'hardness',0.1),(1,'hp',25),
 (2,'soft_atk',10),(2,'hard_atk',4),(2,'defense',26),(2,'breakthrough',16),(2,'armor',15),(2,'piercing',20),(2,'hardness',0.5),(2,'hp',30),
 (3,'soft_atk',12),(3,'hard_atk',14),(3,'defense',12),(3,'breakthrough',28),(3,'armor',60),(3,'piercing',55),(3,'hardness',0.9),(3,'hp',20),
 (4,'soft_atk',20),(4,'hard_atk',2),(4,'defense',6),(4,'breakthrough',6),(4,'armor',0),(4,'piercing',8),(4,'hardness',0.2),(4,'hp',6),
 (5,'soft_atk',2),(5,'hard_atk',1),(5,'defense',10),(5,'breakthrough',2),(5,'armor',5),(5,'piercing',10),(5,'hardness',0.3),(5,'hp',8),(5,'air_deny',0.1),
 (2,'fuel_use',0.6),(3,'fuel_use',1.2),
 (6,'soft_atk',2),(6,'hard_atk',20),(6,'defense',8),(6,'breakthrough',2),(6,'armor',0),(6,'piercing',70),(6,'hardness',0.2),(6,'hp',6);

-- A ficha de combate: o nome de cada número e o que ele faz mesmo, palavra por palavra do CombatSystem.
-- Nada disto é decoração — é o que separa "a minha divisão tem 60 de blindagem" de saber que 60 de
-- blindagem contra 55 de perfuração corta o ataque do outro a metade.
INSERT INTO unit_stat_def (key,name,note,sort,glyph,digits,percent,shown) VALUES
 ('soft_atk','Ataque a infantaria','Golpes por dia contra a parte não blindada do inimigo. Conta pela fatia mole dele: quanto mais dura for a divisão do outro lado, menos vale.',1,'espadas',0,0,1),
 ('hard_atk','Ataque a blindados','Golpes por dia contra a parte blindada do inimigo. É esta que conta contra tanques — uma divisão só com ataque mole não lhes faz nada.',2,'lagarta',0,0,1),
 ('piercing','Perfuração','Se for menor do que a blindagem do inimigo, tudo o que se lhe atira vale metade. É o número que decide se as balas passam.',3,'obus',0,0,1),
 ('armor','Blindagem','O aço que se leva. Enquanto for maior do que a perfuração do inimigo, ele bate a metade.',4,'capacete',0,0,1),
 ('defense','Defesa','O que a divisão aguenta enquanto é atacada: os golpes que couberem aqui dentro quase não fazem dano, os que passarem fazem quatro vezes mais.',5,'escudo',0,0,1),
 ('breakthrough','Rotura','O mesmo, mas para quem vai à frente: é com ela que se aguenta o fogo de quem defende ao atacar.',6,'punho',0,0,1),
 ('hardness','Dureza','A fatia da divisão que é aço. Quanto mais alta, menos lhe pesa o ataque a infantaria e mais lhe pesa o ataque a blindados.',7,'bigorna',0,1,1),
 ('hp','Efectivo','A gente que ela leva. Cai com o dano e recompõe-se com reforços; a zero a divisão deixa de existir.',8,'gente',0,0,1),
 ('mobility','Mobilidade','A velocidade do batalhão mais lento — uma divisão anda ao passo de quem fica atrás. Divide os dias de marcha para a região seguinte.',9,'estrada',0,0,1),
 ('fuel_use','Combustível','O que ela bebe por dia. Com o país a seco, os blindados batem a metade.',10,'barril',1,0,1),
 ('air_deny','Anti-aérea','Ainda não pesa em conta nenhuma: fica escrito para o dia em que a anti-aérea das divisões negar o céu da região a quem voa por cima.',11,'asa',1,0,0),
 ('supply_use','Peso na retaguarda','Ainda não pesa em conta nenhuma: o abastecimento hoje conta divisões, não o que cada uma pede.',12,'caixa',1,0,0);

INSERT INTO unit_tag VALUES
 (1,'infantry'),(1,'ground'),(2,'infantry'),(2,'armored'),(2,'ground'),(3,'armored'),(3,'ground'),
 (4,'support'),(4,'ground'),(5,'support'),(5,'ground'),(6,'support'),(6,'ground');

-- As marcas do material (equipment_mark). Quatro gerações por tipo: a primeira é a de origem (tech_id ''),
-- e cada uma das outras abre-se com a tecnologia do ramo. Custam mais, valem mais e gastam-se menos — é
-- esta a razão de se investigar, e é por causa delas que a fábrica tem de parar para se reafinar.
INSERT INTO equipment_mark (id,unit_type_id,mark,name,tech_id,cost,power,wear,note,glyph) VALUES
 ('inf_mk1',1,1,'Fuzil de serviço','',       1.00,1.00,1.00,'O que sai do paiol quando não há mais nada: serve e chega.','capacete'),
 ('inf_mk2',1,2,'Fuzil de assalto','inf_1',  1.15,1.10,0.95,'Cadência a sério nas mãos do recruta, e um colete que aguenta o estilhaço.','escudo'),
 ('inf_mk3',1,3,'Fuzil modular','inf_2',     1.35,1.22,0.88,'Mira, luz e lançador na mesma calha: a secção vê de noite e bate ao longe.','punho'),
 ('inf_mk4',1,4,'Fuzil em rede','inf_3',     1.60,1.36,0.80,'Cada homem é um sensor: o que o primeiro vê, o pelotão inteiro sabe.','coroa'),
 ('mec_mk1',2,1,'Transporte de rodas','',    1.00,1.00,1.00,'Leva a infantaria ao sítio e sai de lá: chapa contra estilhaço e mais nada.','capacete'),
 ('mec_mk2',2,2,'Viatura de combate','arm_1',1.15,1.10,0.95,'Já fica a combater ao lado dos homens que despejou.','escudo'),
 ('mec_mk3',2,3,'Viatura protegida','arm_2', 1.35,1.22,0.88,'Fundo em V e blindagem em camadas: a mina deixa de ser sentença.','punho'),
 ('mec_mk4',2,4,'Viatura em rede','arm_3',   1.60,1.36,0.80,'Anda com o carro de combate e vê o mesmo mapa que ele.','coroa'),
 ('arm_mk1',3,1,'Carro de combate','',       1.00,1.00,1.00,'Aço, canhão e lagarta: o que se pede a um carro e mais nada.','capacete'),
 ('arm_mk2',3,2,'Blindagem reactiva','arm_1',1.15,1.10,0.95,'Os tijolos que rebentam para fora antes de a carga oca entrar.','escudo'),
 ('arm_mk3',3,3,'Protecção activa','arm_2',  1.35,1.22,0.88,'Deita abaixo o míssil antes de ele chegar ao aço.','punho'),
 ('arm_mk4',3,4,'Carro em rede','arm_3',     1.60,1.36,0.80,'Dispara no que o drone vê, sem nunca levantar a cabeça.','coroa'),
 ('art_mk1',4,1,'Obus rebocado','',          1.00,1.00,1.00,'Chega, monta e bate; para se ir embora leva o tempo que leva.','capacete'),
 ('art_mk2',4,2,'Obus autopropulsado','art_1',1.15,1.10,0.95,'Bate e muda de sítio antes de a resposta cair onde estava.','escudo'),
 ('art_mk3',4,3,'Granada guiada','art_2',    1.35,1.22,0.88,'Uma granada onde antes iam vinte: acerta à primeira.','punho'),
 ('art_mk4',4,4,'Fogo em rede','art_3',      1.60,1.36,0.80,'O drone aponta, a bateria responde em segundos.','coroa'),
 ('aa_mk1', 5,1,'Peça antiaérea','',         1.00,1.00,1.00,'Cano a subir e sorte: contra drone pequeno faz o que pode.','capacete'),
 ('aa_mk2', 5,2,'Míssil de curto alcance','drones_1',1.15,1.10,0.95,'Deixa de ser cortina e passa a ser pontaria.','escudo'),
 ('aa_mk3', 5,3,'Radar e míssil','drones_2', 1.35,1.22,0.88,'Vê primeiro, decide sozinho e só depois avisa quem manda.','punho'),
 ('aa_mk4', 5,4,'Feixe anti-drone','drones_3',1.60,1.36,0.80,'Bater um enxame ao preço da electricidade que se gasta.','coroa'),
 ('at_mk1', 6,1,'Canhão sem recuo','',       1.00,1.00,1.00,'Um tiro, um homem exposto, e o carro de combate à vista.','capacete'),
 ('at_mk2', 6,2,'Míssil filoguiado','art_1', 1.15,1.10,0.95,'O fio leva o míssil até onde o atirador continuar a olhar.','escudo'),
 ('at_mk3', 6,3,'Ataque pelo topo','art_2',  1.35,1.22,0.88,'Entra por cima, onde nenhum carro leva aço a sério.','punho'),
 ('at_mk4', 6,4,'Míssil autónomo','art_3',   1.60,1.36,0.80,'Dispara-se e esquece-se: ele procura o carro sozinho.','coroa');

INSERT INTO modifier (source_kind,condition_key,condition_value,stat_key,required_tag,op,value) VALUES
 ('terrain','terrain','forest',  'str_attacker',NULL,     'mul',0.8),
 ('terrain','terrain','urban',   'str_attacker',NULL,     'mul',0.6),
 ('terrain','terrain','mountain','str_attacker',NULL,     'mul',0.5),
 ('terrain','terrain','tundra',  'str_attacker',NULL,     'mul',0.7),
 ('terrain','terrain','desert',  'str_attacker','infantry','mul',0.9),
 ('terrain','terrain','urban',   'str_attacker','armored','mul',0.6),
 ('terrain','terrain','mountain','str_attacker','armored','mul',0.6),
 ('terrain','river',  'true',    'str_attacker',NULL,     'add',-0.3),
 ('terrain','terrain','urban',   'str_defender',NULL,     'mul',1.2),
 ('terrain','terrain','mountain','str_defender',NULL,     'mul',1.3),
 -- Tropas especiais de terreno (HoI4: special forces). A especialidade é da unidade, não do país: quem
 -- treinou para a serra bate-se na serra, ande sob a bandeira que andar. Até aqui estas marcas só valiam
 -- alguma coisa aos países que por acaso tinham o espírito nacional a jeito — o Gebirgsjäger alemão subia
 -- a montanha com a mesma penalização de um recruta qualquer. Estas linhas não apagam o terreno: descontam
 -- a penalização de quem sabe andar lá (mountain ×0.5 × 1.7 = 0.85 do que valeria em campo aberto).
 ('terrain','terrain','mountain','str_attacker','montanha','mul',1.7),
 ('terrain','terrain','mountain','str_defender','montanha','mul',1.15),
 ('terrain','terrain','forest',  'str_attacker','selva',   'mul',1.25),
 ('terrain','terrain','tundra',  'str_attacker','artico',  'mul',1.40),
 ('terrain','terrain','desert',  'str_attacker','deserto', 'mul',1.20),
 -- Fuzileiros: a travessia de rio é o que mais se parece com o que eles treinam (o assalto anfíbio a sério
 -- está no naval_invasion_marine, que o CombatSystem aplica a quem tem esta marca).
 ('terrain','river',  'true',    'str_attacker','anfibio', 'add',0.20),
 ('air',    'air_sup','own',     'str',         NULL,     'add',0.25),
 ('air',    'air_sup','enemy',   'str',         NULL,     'add',-0.25),
 ('tech',   'tech:drones_1','true','str',       NULL,     'add',0.10),
 ('cyber',  'cyber_hit','true',  'command',     NULL,     'mul',0.9);

-- rules: movement
INSERT INTO rule (key,value,note) VALUES ('move_infra_floor',0.5,'infraestrutura mínima usada no cálculo dos dias de movimento (uma região arrasada abranda, não pára)');
-- rules: ai
INSERT INTO rule (key,value,note) VALUES ('ai_min_org',50,'a IA só mexe divisões com organização ≥ isto');
INSERT INTO rule (key,value,note) VALUES ('ai_heavy_every',3,'cada N-ésima encomenda da IA (divisões existentes + fila) é o template de maior breakthrough, se o dinheiro chegar');
