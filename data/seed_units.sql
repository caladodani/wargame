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

-- ================= A PRANCHETA DOS CARROS (TankShop; HoI4: tank designer) =================
-- As marcas de cima são as gerações que a INVESTIGAÇÃO abre: toda a gente sobe a mesma ladeira e chega ao
-- mesmo carro. O que a prancheta faz é deixar o país desenhar a geração SEGUINTE com as mãos dele — mais
-- canhão e menos aço, ou aço a mais e um motor que o arrasta, ou um carro barato que se faz aos molhos.
-- O que sai não é uma classe à parte: é uma linha de equipment_mark com dono, a mark a seguir à última da
-- tabela. Por isso a fábrica reafina-se para ela sozinha, o armazém mistura-a na pilha e a divisão bate-se
-- com ela sem que uma única linha do jogo saiba que aquilo foi desenhado em casa.
INSERT INTO tank_chassis (id,name,unit_type_id,cost,power,wear,tech_id,note,sort,glyph,slots) VALUES
 ('ligeiro',  'Casco ligeiro',   3,0.75,0.90,1.05,'',     'Pouco aço, muita estrada: chega primeiro e foge antes de levar.',1,'lagarta','canhao,torre,motor,blindagem,extra'),
 ('medio',    'Casco médio',     3,1.10,1.15,1.00,'',     'O carro de que se fazem exércitos: nem o mais grosso nem o mais rápido.',2,'lagarta','canhao,torre,motor,blindagem,lagartas,extra'),
 ('pesado',   'Casco pesado',    3,1.70,1.35,0.95,'arm_1','Aço a sério e o preço que isso tem: abre a porta e aguenta lá dentro.',3,'muro','canhao,torre,motor,blindagem,blindagem,lagartas,extra'),
 ('moderno',  'Casco moderno',   3,2.10,1.50,0.88,'arm_3','Torre não tripulada e tudo em rede: o carro que ainda está a chegar.',4,'coroa','canhao,torre,motor,blindagem,lagartas,extra,extra'),
 ('rodas',    'Casco de rodas',  2,0.90,0.95,1.00,'',     'Viatura da infantaria: leva os homens ao sítio e fica a bater com eles.',5,'camiao','canhao,torre,motor,blindagem,extra'),
 ('caca',     'Casco sem torre', 6,1.00,1.10,0.98,'arm_1','Canhão grande num casco baixo: espera o carro do outro e não o deixa passar.',6,'obus','canhao,motor,blindagem,extra');

INSERT INTO tank_slot (id,name,required,note,sort,glyph) VALUES
 ('canhao',   'Canhão',    1,'A boca de fogo. Sem ela isto é um tractor com chapa.',1,'canhao'),
 ('torre',    'Torre',     0,'Onde o canhão gira — e quanta gente lá cabe a olhar para fora.',2,'coroa'),
 ('motor',    'Motor',     0,'O que arrasta o aço todo: sem cavalos, blindagem é peso morto.',3,'mola'),
 ('blindagem','Blindagem', 0,'O aço, e o que se lhe cola por fora para o míssil não entrar.',4,'muro'),
 ('lagartas', 'Lagartas',  0,'Onde o carro assenta: lama, neve e serra decidem-se aqui.',5,'lagarta'),
 ('extra',    'Extras',    0,'Óptica, rádio, fumo, drones — o que faz o carro ver antes de ser visto.',6,'antena');

INSERT INTO tank_module (id,name,slot,cost,power,wear,tech_id,note,sort,glyph) VALUES
 ('canhao_curto',  'Canhão curto',        'canhao',   0.05,0.05, 0.00,'',        'Bate bem em casa e em quem anda a pé; contra aço não faz nada.',1,'canhao'),
 ('canhao_longo',  'Canhão de alta velocidade','canhao',0.20,0.20,0.02,'arm_1',  'Cano comprido e projéctil rápido: é assim que se fura um carro.',2,'canhao'),
 ('canhao_130',    'Canhão de 130 mm',    'canhao',   0.38,0.34, 0.05,'arm_3',   'Passa por qualquer aço que hoje ande no mundo — e come munição a esse preço.',3,'obus'),
 ('misseis_carro', 'Mísseis no carro',    'canhao',   0.30,0.28,-0.04,'art_2',   'Dispara-se de dois quilómetros, do sítio onde o outro nem olha.',4,'alvo'),
 ('torre_dupla',   'Torre de dois homens','torre',    0.06,0.04, 0.02,'',        'Um aponta, o outro carrega — e o comandante faz as duas coisas mal.',5,'coroa'),
 ('torre_tres',    'Torre de três homens','torre',    0.14,0.12,-0.02,'arm_1',   'Comandante só a mandar: o carro vê o combate em vez de olhar pela mira.',6,'coroa'),
 ('torre_vazia',   'Torre não tripulada', 'torre',    0.28,0.24,-0.06,'arm_3',   'Tripulação fechada na cuba, torre só com máquina: o que a mata já não os mata.',7,'drone'),
 ('motor_diesel',  'Motor diesel',        'motor',    0.05,0.04,-0.05,'',        'Puxa, bebe pouco e não pega fogo por qualquer coisa.',8,'mola'),
 ('motor_turbina', 'Turbina a gás',       'motor',    0.22,0.16, 0.06,'ind_2',   'Cavalos a mais e uma sede de camião-cisterna atrás.',9,'mola'),
 ('motor_hibrido', 'Transmissão híbrida', 'motor',    0.30,0.20,-0.10,'ind_3',   'Anda calado, arranca do nada e passa o dia à espera sem gastar.',10,'raio'),
 ('chapa_soldada', 'Chapa soldada',       'blindagem',0.06,0.05, 0.00,'',        'Aço e mais nada: contra estilhaço chega, contra carga oca é papel.',11,'escudo'),
 ('reactiva',      'Blindagem reactiva',  'blindagem',0.16,0.14, 0.02,'arm_1',   'Tijolos que rebentam para fora antes de o jacto entrar.',12,'escudo'),
 ('composita',     'Blindagem compósita', 'blindagem',0.26,0.22,-0.03,'arm_2',   'Camadas de cerâmica: pesa menos do que o aço que substitui.',13,'muro'),
 ('gaiola_drones', 'Gaiola anti-drone',   'blindagem',0.10,0.09,-0.05,'drones_2','A guerra de hoje cai de cima: rede, jammer e o carro chega ao fim do dia.',14,'antena'),
 ('lagarta_larga', 'Lagarta larga',       'lagartas', 0.08,0.06,-0.04,'',        'Assenta na lama e na neve em vez de as escavar.',15,'lagarta'),
 ('suspensao',     'Suspensão hidráulica','lagartas', 0.18,0.13,-0.03,'ind_2',   'Baixa-se para se esconder e sobe para bater por cima da crista.',16,'mola'),
 ('optica',        'Óptica térmica',      'extra',    0.12,0.12,-0.02,'inf_2',   'Vê o outro pelo calor, de noite e através do fumo dele.',17,'luneta'),
 ('radio_rede',    'Rádio em rede',       'extra',    0.14,0.14,-0.02,'doc_1',   'O que um carro vê, o esquadrão inteiro aponta.',18,'antena'),
 ('aps',           'Protecção activa',    'extra',    0.24,0.20,-0.08,'arm_2',   'Deita abaixo o míssil antes de ele chegar ao aço.',19,'raio'),
 ('drone_carro',   'Drone de acompanhamento','extra', 0.20,0.18,-0.03,'drones_3','Olha a curva antes do carro lá chegar, e às vezes bate primeiro.',20,'drone'),
 ('fumo',          'Cortina de fumo',     'extra',    0.04,0.03,-0.04,'',        'Três segundos de nada à frente: às vezes é o dia todo.',21,'floco');

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
