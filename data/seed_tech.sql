-- Árvore tecnológica (2030+). cost = dias com research_speed 1. Efeitos de combate = modifier com condition_key
-- 'tech:<id>' (ids 900-999, reservados); efeitos de país = tech_effect (× Country.Stat). Nada disto está em código.
INSERT INTO tech (id,branch,name,cost,requires,description) VALUES
 ('inf_1',   'Infantaria', 'Equipamento de infantaria moderno', 90,  NULL,     'Fuzis, óptica e protecção individual actuais: +5 % força para infantaria.'),
 ('inf_2',   'Infantaria', 'Visão nocturna e comunicações',     120, 'inf_1',  'Combate 24 h e rádio encriptado: +6 % ao atacar com infantaria.'),
 ('inf_3',   'Infantaria', 'Soldado do futuro',                 180, 'inf_2',  'Exosqueleto, drones de esquadra, munição inteligente: +8 % força para infantaria.'),
 ('arm_1',   'Blindados',  'Blindagem reactiva',                100, NULL,     'ERA nos carros e VBCI: +6 % em defesa para blindados.'),
 ('arm_2',   'Blindados',  'Protecção activa (APS)',            150, 'arm_1',  'Intercepta mísseis e RPG antes do impacto: +8 % força para blindados.'),
 ('arm_3',   'Blindados',  'Carro de combate de nova geração',  220, 'arm_2',  'Torre não tripulada, canhão de 130 mm, IA de tiro: +10 % força para blindados.'),
 ('art_1',   'Artilharia', 'Munições guiadas',                  100, NULL,     'Excalibur e afins: +6 % ao atacar com apoio de fogo.'),
 ('art_2',   'Artilharia', 'Contra-bateria por radar',          140, 'art_1',  'Localiza e silencia a artilharia inimiga: +6 % em defesa para unidades de apoio.'),
 ('art_3',   'Artilharia', 'Foguetes de longo alcance',         200, 'art_2',  'HIMARS/PrSM ao nível de brigada: +8 % ao atacar.'),
 ('drones_1','Drones',     'Drones tácticos',                   80,  NULL,     'Reconhecimento e FPV em cada batalhão: +10 % força (já existia na base).'),
 ('drones_2','Drones',     'Enxames de drones',                 160, 'drones_1','Centenas de FPV coordenados: +10 % ao atacar.'),
 ('drones_3','Drones',     'Drones autónomos',                  240, 'drones_2','Sem operador, imunes a jamming: +10 % força.'),
 ('log_1',   'Logística',  'Logística digital',                 90,  NULL,     'Abastecimento por dados em tempo real: organização recupera 10 % mais depressa.'),
 ('log_2',   'Logística',  'Mobilidade estratégica',            140, 'log_1',  'Transportadores e comboios logísticos: divisões movem-se 15 % mais depressa.'),
 ('log_3',   'Logística',  'Cadeia de abastecimento resiliente',200, 'log_2',  'Redundância e stocks avançados: organização recupera mais 10 %.'),
 ('ind_1',   'Indústria',  'Produção em série',                 100, NULL,     'Linhas de montagem de defesa: encomendas 10 % mais rápidas.'),
 ('ind_2',   'Indústria',  'Automação industrial',              160, 'ind_1',  'Robótica e fábricas digitais: rendimento +10 %.'),
 ('ind_3',   'Indústria',  'Base industrial de defesa',         240, 'ind_2',  'Capacidade de guerra prolongada: encomendas mais 15 % rápidas.'),
 ('doc_1',   'Doutrina',   'Armas combinadas',                  110, NULL,     'Infantaria, blindados, artilharia e drones a operar juntos: +5 % comando.'),
 ('doc_2',   'Doutrina',   'Guerra de manobra',                 170, 'doc_1',  'Penetrar e envolver em vez de empurrar: +8 % ao atacar.'),
 ('doc_3',   'Doutrina',   'Defesa em profundidade',            170, 'doc_1',  'Linhas sucessivas e reservas móveis: +8 % em defesa.'),
 ('res_1',   'Ciência',    'Investigação em rede',              120, NULL,     'Universidades e indústria a trabalhar com as Forças Armadas: investigação 15 % mais rápida.'),
 -- Aviação e Marinha: a árvore era toda de terra e de fábrica, e quem comprava esquadrões e navios não
 -- tinha uma única linha de investigação onde os melhorar. Os efeitos são de país (tech_effect) porque é
 -- assim que o AirMissionSystem e o NavalMissionSystem perguntam pelo que a arma vale.
 ('air_1',   'Aviação',    'Caça de superioridade aérea',       110, NULL,     'Caças de nova geração e ligação de dados entre esquadras: perde-se menos gente no céu disputado.'),
 ('air_2',   'Aviação',    'Munições de precisão a distância',  170, 'air_1',  'Bate-se o alvo de fora do alcance da defesa antiaérea: bombardeamento 12 % mais fundo.'),
 ('air_3',   'Aviação',    'Manutenção expedicionária',         230, 'air_2',  'Oficina que vai com as asas para onde elas forem: 10 % menos de sustento por asa destacada.'),
 ('nav_1',   'Marinha',    'Guerra anti-submarina',             110, NULL,     'Sonares rebocados, helicópteros e drones de superfície: afunda-se menos aço nosso em cada combate.'),
 ('nav_2',   'Marinha',    'Mísseis anti-navio de longo alcance',170,'nav_1',  'Fecha-se um mar de muito mais longe: bloqueio 12 % mais apertado.'),
 ('nav_3',   'Marinha',    'Reabastecimento no mar',            230, 'nav_2',  'A esquadra deixa de voltar ao porto para comer: 10 % menos de sustento por navio no mar.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value) VALUES
 (901,'tech','tech:inf_1',   'true','str',         'infantry','add',0.05),
 (902,'tech','tech:inf_2',   'true','str_attacker','infantry','add',0.06),
 (903,'tech','tech:inf_3',   'true','str',         'infantry','add',0.08),
 (904,'tech','tech:arm_1',   'true','str_defender','armored', 'add',0.06),
 (905,'tech','tech:arm_2',   'true','str',         'armored', 'add',0.08),
 (906,'tech','tech:arm_3',   'true','str',         'armored', 'add',0.10),
 (907,'tech','tech:art_1',   'true','str_attacker','support', 'add',0.06),
 (908,'tech','tech:art_2',   'true','str_defender','support', 'add',0.06),
 (909,'tech','tech:art_3',   'true','str_attacker',NULL,      'add',0.08),
 (910,'tech','tech:drones_2','true','str_attacker',NULL,      'add',0.10),
 (911,'tech','tech:drones_3','true','str',         NULL,      'add',0.10),
 (912,'tech','tech:doc_1',   'true','command',     NULL,      'mul',1.05),
 (913,'tech','tech:doc_2',   'true','str_attacker',NULL,      'add',0.08),
 (914,'tech','tech:doc_3',   'true','str_defender',NULL,      'add',0.08);

INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('log_1','org_regain',1.10), ('log_2','move_speed',1.15), ('log_3','org_regain',1.10),
 ('ind_1','production_speed',1.10), ('ind_2','industry',1.10), ('ind_3','production_speed',1.15),
 ('res_1','research_speed',1.15),
 ('air_1','air_losses',0.92), ('air_2','air_bombing',1.12), ('air_3','air_upkeep',0.90),
 ('nav_1','naval_losses',0.92), ('nav_2','naval_blockade',1.12), ('nav_3','naval_upkeep',0.90);

-- Tecnologias iniciais: NATO/aliados avançados começam à frente; o resto começa do zero.
INSERT OR IGNORE INTO country_tech (country_tag,tech_id) VALUES
 ('USA','inf_1'),('USA','inf_2'),('USA','arm_1'),('USA','arm_2'),('USA','art_1'),('USA','drones_1'),('USA','log_1'),('USA','log_2'),('USA','ind_1'),('USA','doc_1'),
 ('GBR','inf_1'),('GBR','arm_1'),('GBR','art_1'),('GBR','drones_1'),('GBR','log_1'),('GBR','doc_1'),
 ('FRA','inf_1'),('FRA','arm_1'),('FRA','art_1'),('FRA','drones_1'),('FRA','log_1'),('FRA','doc_1'),
 ('DEU','inf_1'),('DEU','arm_1'),('DEU','arm_2'),('DEU','art_1'),('DEU','ind_1'),('DEU','doc_1'),
 ('ISR','inf_1'),('ISR','inf_2'),('ISR','arm_1'),('ISR','arm_2'),('ISR','drones_1'),('ISR','drones_2'),('ISR','doc_1'),
 ('KOR','inf_1'),('KOR','arm_1'),('KOR','art_1'),('KOR','ind_1'),('KOR','ind_2'),
 ('JPN','inf_1'),('JPN','arm_1'),('JPN','ind_1'),('JPN','ind_2'),('JPN','res_1'),
 ('CHN','inf_1'),('CHN','arm_1'),('CHN','drones_1'),('CHN','ind_1'),('CHN','ind_2'),
 ('RUS','arm_1'),('RUS','art_1'),('RUS','art_2'),('RUS','drones_1'),('RUS','drones_2'),
 ('UKR','inf_1'),('UKR','drones_1'),('UKR','drones_2'),('UKR','art_1'),('UKR','doc_3'),
 ('TUR','inf_1'),('TUR','drones_1'),('TUR','drones_2'),('TUR','ind_1'),
 ('POL','inf_1'),('POL','arm_1'),('POL','art_1'),('POL','ind_1'),
 ('IND','inf_1'),('IND','arm_1'),('IND','doc_1'),
 ('PRT','inf_1'),('PRT','drones_1'),('PRT','log_1'),('PRT','doc_1'),
 ('ESP','inf_1'),('ESP','arm_1'),('ESP','log_1'),('ESP','doc_1'),
 ('ITA','inf_1'),('ITA','arm_1'),('ITA','art_1'),('ITA','doc_1'),
 ('BRA','inf_1'),('BRA','ind_1'),
 ('AUS','inf_1'),('AUS','arm_1'),('AUS','drones_1'),('AUS','log_1'),
 ('CAN','inf_1'),('CAN','arm_1'),('CAN','log_1'),
 ('IRN','drones_1'),('IRN','drones_2'),('IRN','art_1'),
 ('PRK','art_1'),('PRK','art_2'),
 ('SAU','inf_1'),('SAU','arm_1'),
 ('EGY','inf_1'),('EGY','arm_1'),
 ('PAK','inf_1'),('PAK','drones_1'),
 ('IDN','inf_1'),
 ('ARG','inf_1');

-- Quem já chega a 2030 com força aérea e marinha a sério começa com o primeiro degrau da arma feito: sem
-- isto o mundo abria com toda a gente ao mesmo nível no ar e no mar, que é o contrário do que se vê.
INSERT OR IGNORE INTO country_tech (country_tag,tech_id) VALUES
 ('USA','air_1'),('USA','air_2'),('USA','nav_1'),('USA','nav_2'),
 ('GBR','air_1'),('GBR','nav_1'),('FRA','air_1'),('FRA','nav_1'),
 ('RUS','air_1'),('RUS','nav_1'),('CHN','air_1'),('CHN','nav_1'),
 ('JPN','nav_1'),('KOR','air_1'),('IND','nav_1'),('ISR','air_1'),
 ('TUR','air_1'),('ITA','nav_1'),('ESP','nav_1'),('AUS','nav_1'),
 ('PRT','nav_1'),('CAN','nav_1'),('BRA','nav_1');

-- Programa nuclear (appended): 2 patamares; nuc_2 dá o multiplicador "nuclear" que desbloqueia
-- BuildNukeCommand (Stat("nuclear") > 1). Só investigação — as ogivas compram-se depois.
INSERT INTO tech (id,branch,name,cost,requires,description) VALUES
 ('nuc_1', 'Nuclear', 'Enriquecimento de urânio', 200, 'res_1', 'Centrifugadoras e ciclo de combustível próprio: o caminho para a bomba abre-se.'),
 ('nuc_2', 'Nuclear', 'Arma nuclear',             320, 'nuc_1', 'Ogiva operacional e vector de lançamento: permite construir ogivas (☢).');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('nuc_2', 'nuclear', 2.0);

-- ===== A árvore de terra a sério (0.3.76) =====
-- O utilizador disse que a pesquisa do exército estava curta, e estava: a Aviação e a Marinha tinham 31
-- degraus cada uma (3 comuns + 28 programas nacionais) e a Infantaria, os Blindados e a Artilharia tinham
-- três. No HoI4 o exército é a maior parte da árvore — armas de infantaria, blindados, artilharia,
-- companhias de apoio, forças especiais e quatro doutrinas de terra — e é isso que entra aqui.
--
-- Nada disto é código: cada degrau é uma linha em `tech`, o que ele faz ao combate é uma linha em
-- `modifier` com condition_key 'tech:<id>' (ids 900-999 reservados; 901-924 já estavam) e o que faz ao
-- país é uma linha em `tech_effect`. Três ramos novos entram em tech_branch (seed_world.sql).

INSERT INTO tech (id,branch,name,cost,requires,description) VALUES
 -- Infantaria: a arma que se leva ao ombro, o chão onde ela combate e o assalto pesado
 ('inf_4', 'Infantaria','Fuzil de calibre intermédio',        110,'inf_1','Cartucho intermédio, óptica de dia e de noite em todos os pelotões: +4 % força para infantaria.'),
 ('inf_5', 'Infantaria','Anti-carro portátil',                130,'inf_1','Javelin e NLAW até ao esquadrão: a infantaria deixa de fugir de um carro, +6 % em defesa.'),
 ('inf_6', 'Infantaria','Combate urbano',                     150,'inf_2','Assalto a quarteirão, drone de janela e demolição controlada: +5 % ao atacar com infantaria.'),
 ('inf_7', 'Infantaria','Tropas de montanha',                 160,'inf_2','Aclimatação, mula mecânica e tiro em encosta: +10 % força para tropas de montanha.'),
 ('inf_8', 'Infantaria','Guerra em clima árctico',            170,'inf_5','Aquecimento, esqui e armamento que não gripa a 40 negativos: +10 % força para tropas árcticas.'),
 ('inf_9', 'Infantaria','Guerra na selva',                    170,'inf_6','Patrulha longa, água e febre: +10 % força para tropas de selva.'),
 ('inf_10','Infantaria','Batalhão de assalto pesado',         220,'inf_3','Secções de brecha com protecção pesada e apoio orgânico: +6 % ao atacar com infantaria.'),

 -- Blindados: proteger, ver, furar
 ('arm_4', 'Blindados','Veículo de combate de infantaria',    120,'arm_1','Canhão de 30 mm e mísseis no VCI: a coluna deixa de andar cega, +5 % força para blindados.'),
 ('arm_5', 'Blindados','Munição de energia cinética',         160,'arm_2','Penetradores longos de urânio ou tungsténio: +8 % ao atacar com blindados.'),
 ('arm_6', 'Blindados','Caçador-atirador e visão panorâmica', 170,'arm_2','O chefe procura enquanto o atirador bate: +5 % força para blindados.'),
 ('arm_7', 'Blindados','Blindados de rodas',                  140,'arm_4','Brigadas de rodas que chegam num dia onde a lagarta leva três: divisões 8 % mais rápidas.'),
 ('arm_8', 'Blindados','Engenharia blindada',                 150,'arm_4','Carro de brecha, rolo de minas e ponte lançada: +5 % ao atacar com blindados.'),
 ('arm_9', 'Blindados','Carro pesado de ruptura',             240,'arm_5','Blindagem de assalto para furar linha preparada: +8 % ao atacar com blindados.'),
 ('arm_10','Blindados','Formação blindada autónoma',          300,'arm_3','Carros, VCI, drones e artilharia sob um só comando: +8 % força para blindados.'),

 -- Artilharia: o fogo que decide a batalha sem lá estar
 ('art_4', 'Artilharia','Obus autopropulsado',                130,'art_1','Dispara e sai antes da contra-bateria chegar: +6 % ao atacar com apoio de fogo.'),
 ('art_5', 'Artilharia','Defesa antiaérea de campanha',       140,'art_1','SHORAD junto da coluna: o céu inimigo deixa de mandar, +5 % em defesa.'),
 ('art_6', 'Artilharia','Morteiros pesados de batalhão',      120,'art_2','Fogo próprio a 120 mm sem pedir nada a ninguém: +5 % força para unidades de apoio.'),
 ('art_7', 'Artilharia','Míssil anti-carro de longo alcance', 160,'art_2','Bate a coluna antes de ela ver a linha: +6 % em defesa.'),
 ('art_8', 'Artilharia','Artilharia assistida por drone',     190,'art_3','O drone corrige o tiro em segundos: +6 % ao atacar.'),
 ('art_9', 'Artilharia','Fogos de profundidade',              260,'art_3','Depósitos, pontes e postos de comando na retaguarda: +8 % ao atacar.'),
 ('art_10','Artilharia','Rede de fogos conjunta',             300,'art_8','Quem vê pede e quem tem dispara, sem passar por dez postos: +5 % comando.'),

 -- Apoio de combate: as companhias que o HoI4 mete na divisão e que aqui não existiam
 ('sup_1','Apoio','Companhia de engenharia',                   90, NULL,  'Sapa, mina, ponte e abrigo: +6 % em defesa.'),
 ('sup_2','Apoio','Companhia de reconhecimento',              110,'sup_1','Ver primeiro é andar primeiro: divisões 8 % mais rápidas.'),
 ('sup_3','Apoio','Hospital de campanha',                     120,'sup_1','O ferido volta à companhia em vez de ir para casa: organização recupera 8 % mais depressa.'),
 ('sup_4','Apoio','Companhia de manutenção',                  140,'sup_2','Menos carros parados por avaria do que por fogo: +5 % força para blindados.'),
 ('sup_5','Apoio','Companhia de sinais',                      150,'sup_2','Rádio que chega e ordem que se percebe: +6 % comando.'),
 ('sup_6','Apoio','Polícia militar',                          130,'sup_3','Retaguarda arrumada e estrada aberta: a revolta em terra ocupada cresce 12 % mais devagar.'),
 ('sup_7','Apoio','Companhia logística',                      170,'sup_4','A divisão passa a carregar o seu abastecimento: 6 % mais rápida e organização mais firme.'),
 ('sup_8','Apoio','Estado-maior de brigada',                  220,'sup_5','Planeamento próprio ao nível da brigada: +6 % comando.'),

 -- Forças especiais: o ramo que dá sentido às tropas com marca (especial, pára-quedista, anfíbia, COIN)
 ('esp_1','Forças Especiais','Comandos',                             100, NULL,  'Selecção dura, tiro de perto e autonomia: +8 % força para tropas especiais.'),
 ('esp_2','Forças Especiais','Pára-quedistas',                       140,'esp_1','Salto em massa com material pesado: +10 % força para tropas pára-quedistas.'),
 ('esp_3','Forças Especiais','Infantaria de marinha',                140,'esp_1','Desembarque contra praia defendida: +10 % força para tropas anfíbias.'),
 ('esp_4','Forças Especiais','Reconhecimento de longo alcance',      180,'esp_2','Equipas na retaguarda a marcar alvos durante semanas: +8 % ao atacar com tropas especiais.'),
 ('esp_5','Forças Especiais','Contra-guerrilha',                     170,'esp_3','Guerra entre a população sem a virar contra nós: +10 % força em contra-guerrilha e revolta 10 % mais lenta.'),
 ('esp_6','Forças Especiais','Comando conjunto de operações especiais',240,'esp_4','Terra, ar e mar numa só cadeia de operações: +6 % força de elite e +5 % comando.'),

 -- Drones: a arma que a Ucrânia ensinou ao mundo
 ('drones_4','Drones','Munições vagabundas',                  180,'drones_2','O drone espera pelo alvo em vez de o procurar: +6 % ao atacar.'),
 ('drones_5','Drones','Drones de vigilância de teatro',       200,'drones_2','Olho permanente sobre a frente inteira: +5 % comando.'),
 ('drones_6','Drones','Veículos terrestres não tripulados',   260,'drones_3','Reabastecer, evacuar e disparar sem pôr gente na linha: +5 % força.'),
 ('drones_7','Drones','Enxame cooperativo autónomo',          320,'drones_6','Centenas de aparelhos a repartir alvos entre si, sem rádio para cortar: +8 % ao atacar.'),

 -- Guerra electrónica: o espectro, que hoje decide tanto como o obus
 ('ew_1','Guerra Electrónica','Empastelamento táctico',       100, NULL, 'Corta o rádio e o GPS de quem assalta: +5 % em defesa.'),
 ('ew_2','Guerra Electrónica','Guerra anti-drone',            150,'ew_1','Detecção, empastelamento e tiro contra o que voa baixo: +6 % em defesa.'),
 ('ew_3','Guerra Electrónica','Rede de dados do campo de batalha',160,'ew_1','Toda a gente vê o mesmo mapa ao mesmo tempo: +6 % comando.'),
 ('ew_4','Guerra Electrónica','Contra-medidas electrónicas',  200,'ew_2','Sobrevive-se ao espectro do outro lado: +4 % força.'),
 ('ew_5','Guerra Electrónica','Ciberdefesa militar',          210,'ew_3','Redes militares que aguentam o primeiro dia: contra-espionagem 25 % melhor.'),
 ('ew_6','Guerra Electrónica','Comando e controlo integrado', 280,'ew_4','Sensor, decisão e fogo na mesma rede: +8 % comando.'),

 -- Doutrina: quatro caminhos, como no HoI4 — manobra, fogo, defesa e massa
 ('doc_4', 'Doutrina','Superioridade de fogo',                170,'doc_1','Ganha-se com o obus e não com o homem: +8 % ao atacar com apoio de fogo.'),
 ('doc_5', 'Doutrina','Assalto em massa',                     170,'doc_1','Frente larga e reservas atrás de reservas: +3 % força e 10 % mais recrutamento.'),
 ('doc_6', 'Doutrina','Guerra relâmpago',                     240,'doc_2','Ponta blindada que não pára para consolidar: +10 % ao atacar com blindados.'),
 ('doc_7', 'Doutrina','Elasticidade defensiva',               240,'doc_3','Ceder terreno para partir o assalto e contra-atacar: +6 % em defesa.'),
 ('doc_8', 'Doutrina','Concentração de fogos',                250,'doc_4','Toda a artilharia do corpo sobre um só ponto: +6 % ao atacar.'),
 ('doc_9', 'Doutrina','Vaga de assalto',                      240,'doc_5','Escalões sucessivos sem dar descanso a quem defende: +8 % ao atacar com infantaria.'),
 ('doc_10','Doutrina','Comando conjunto',                     320,'doc_6','Terra, ar e mar debaixo de um só plano: +8 % comando.'),

 -- Logística: o que faz a diferença entre uma frente que anda e uma frente que morre de fome
 ('log_4','Logística','Comboios blindados',                   160,'log_2','Camião protegido que entrega debaixo de fogo: divisões 8 % mais rápidas.'),
 ('log_5','Logística','Depósitos avançados',                  210,'log_3','O depósito segue a frente em vez de esperar por ela: organização recupera mais 8 %.'),
 ('log_6','Logística','Reservas estratégicas de combustível',  220,'log_3','Tanques cheios e reserva nacional: 25 % mais combustível guardado.'),
 ('log_7','Logística','Manutenção preventiva',                240,'log_4','Revisão antes da avaria: organização recupera mais 6 %.'),

 -- Ciência e indústria: o motor que paga tudo o resto
 ('res_2','Ciência','Laboratório nacional',                   200,'res_1','Um centro de investigação de defesa a sério: mais uma linha de investigação a andar.'),
 ('res_3','Ciência','Cooperação universidade-indústria',      240,'res_2','O que sai do laboratório entra na fábrica no mesmo ano: investigação 15 % mais rápida.'),
 ('res_4','Ciência','Inteligência artificial aplicada',       300,'res_3','Modelos a desenhar material e a planear produção: investigação 15 % mais rápida e rendimento +5 %.'),
 ('ind_4','Indústria','Ferramentas de precisão',              200,'ind_2','Máquinas-ferramenta próprias: encomendas 10 % mais rápidas.'),
 ('ind_5','Indústria','Indústria dispersa',                   260,'ind_3','Fábricas espalhadas que um bombardeamento não apaga: rendimento +10 %.'),
 ('ind_6','Indústria','Economia de guerra total',             320,'ind_5','O país inteiro a produzir para a frente: encomendas mais 15 % rápidas e 10 % mais recrutamento.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value) VALUES
 (925,'tech','tech:inf_4',    'true','str',         'infantry','add',0.04),
 (926,'tech','tech:inf_5',    'true','str_defender','infantry','add',0.06),
 (927,'tech','tech:inf_6',    'true','str_attacker','infantry','add',0.05),
 (928,'tech','tech:inf_7',    'true','str',         'montanha','add',0.10),
 (929,'tech','tech:inf_8',    'true','str',         'artico',  'add',0.10),
 (930,'tech','tech:inf_9',    'true','str',         'selva',   'add',0.10),
 (931,'tech','tech:inf_10',   'true','str_attacker','infantry','add',0.06),
 (932,'tech','tech:arm_4',    'true','str',         'armored', 'add',0.05),
 (933,'tech','tech:arm_5',    'true','str_attacker','armored', 'add',0.08),
 (934,'tech','tech:arm_6',    'true','str',         'armored', 'add',0.05),
 (935,'tech','tech:arm_8',    'true','str_attacker','armored', 'add',0.05),
 (936,'tech','tech:arm_9',    'true','str_attacker','armored', 'add',0.08),
 (937,'tech','tech:arm_10',   'true','str',         'armored', 'add',0.08),
 (938,'tech','tech:art_4',    'true','str_attacker','support', 'add',0.06),
 (939,'tech','tech:art_5',    'true','str_defender',NULL,      'add',0.05),
 (940,'tech','tech:art_6',    'true','str',         'support', 'add',0.05),
 (941,'tech','tech:art_7',    'true','str_defender',NULL,      'add',0.06),
 (942,'tech','tech:art_8',    'true','str_attacker',NULL,      'add',0.06),
 (943,'tech','tech:art_9',    'true','str_attacker',NULL,      'add',0.08),
 (944,'tech','tech:art_10',   'true','command',     NULL,      'mul',1.05),
 (945,'tech','tech:sup_1',    'true','str_defender',NULL,      'add',0.06),
 (946,'tech','tech:sup_4',    'true','str',         'armored', 'add',0.05),
 (947,'tech','tech:sup_5',    'true','command',     NULL,      'mul',1.06),
 (948,'tech','tech:sup_8',    'true','command',     NULL,      'mul',1.06),
 (949,'tech','tech:esp_1',    'true','str',         'especial','add',0.08),
 (950,'tech','tech:esp_2',    'true','str',         'airborne','add',0.10),
 (951,'tech','tech:esp_3',    'true','str',         'anfibio', 'add',0.10),
 (952,'tech','tech:esp_4',    'true','str_attacker','especial','add',0.08),
 (953,'tech','tech:esp_5',    'true','str',         'coin',    'add',0.10),
 (954,'tech','tech:esp_6',    'true','str',         'elite',   'add',0.06),
 (955,'tech','tech:esp_6',    'true','command',     NULL,      'mul',1.05),
 (956,'tech','tech:drones_4', 'true','str_attacker',NULL,      'add',0.06),
 (957,'tech','tech:drones_5', 'true','command',     NULL,      'mul',1.05),
 (958,'tech','tech:drones_6', 'true','str',         NULL,      'add',0.05),
 (959,'tech','tech:drones_7', 'true','str_attacker',NULL,      'add',0.08),
 (960,'tech','tech:ew_1',     'true','str_defender',NULL,      'add',0.05),
 (961,'tech','tech:ew_2',     'true','str_defender',NULL,      'add',0.06),
 (962,'tech','tech:ew_3',     'true','command',     NULL,      'mul',1.06),
 (963,'tech','tech:ew_4',     'true','str',         NULL,      'add',0.04),
 (964,'tech','tech:ew_6',     'true','command',     NULL,      'mul',1.08),
 (965,'tech','tech:doc_4',    'true','str_attacker','support', 'add',0.08),
 (966,'tech','tech:doc_5',    'true','str',         NULL,      'add',0.03),
 (967,'tech','tech:doc_6',    'true','str_attacker','armored', 'add',0.10),
 (968,'tech','tech:doc_7',    'true','str_defender',NULL,      'add',0.06),
 (969,'tech','tech:doc_8',    'true','str_attacker',NULL,      'add',0.06),
 (970,'tech','tech:doc_9',    'true','str_attacker','infantry','add',0.08),
 (971,'tech','tech:doc_10',   'true','command',     NULL,      'mul',1.08);

INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('arm_7','move_speed',1.08),
 ('sup_2','move_speed',1.08), ('sup_3','org_regain',1.08), ('sup_6','resistance_growth',0.88),
 ('sup_7','move_speed',1.06), ('sup_7','org_regain',1.05),
 ('esp_5','resistance_growth',0.90),
 ('ew_5','counter_intel',1.25),
 ('doc_5','conscription',1.10),
 ('log_4','move_speed',1.08), ('log_5','org_regain',1.08), ('log_6','fuel_capacity',1.25), ('log_7','org_regain',1.06),
 ('res_2','research_slots',1.5), ('res_3','research_speed',1.15),
 ('res_4','research_speed',1.15), ('res_4','industry',1.05),
 ('ind_4','production_speed',1.10), ('ind_5','industry',1.10),
 ('ind_6','production_speed',1.15), ('ind_6','conscription',1.10);

-- Quem já chega a 2030 com estes degraus feitos. Não é generosidade: um exército que treina comandos há
-- setenta anos não começa a estudá-los no primeiro dia, e sem isto o mundo abre todo igual.
INSERT OR IGNORE INTO country_tech (country_tag,tech_id) VALUES
 ('USA','inf_4'),('USA','inf_5'),('USA','arm_4'),('USA','art_4'),('USA','sup_1'),('USA','sup_2'),('USA','sup_3'),('USA','sup_5'),('USA','esp_1'),('USA','esp_2'),('USA','esp_3'),('USA','ew_1'),('USA','ew_3'),('USA','doc_4'),('USA','res_2'),
 ('GBR','inf_4'),('GBR','sup_1'),('GBR','sup_2'),('GBR','esp_1'),('GBR','esp_3'),('GBR','ew_1'),('GBR','doc_4'),
 ('FRA','inf_4'),('FRA','arm_4'),('FRA','sup_1'),('FRA','esp_1'),('FRA','ew_1'),('FRA','doc_4'),
 ('DEU','inf_4'),('DEU','arm_4'),('DEU','arm_6'),('DEU','sup_1'),('DEU','sup_4'),('DEU','ew_1'),
 ('ISR','inf_4'),('ISR','inf_6'),('ISR','arm_4'),('ISR','arm_6'),('ISR','esp_1'),('ISR','esp_4'),('ISR','ew_1'),('ISR','ew_2'),('ISR','ew_3'),
 ('RUS','inf_5'),('RUS','art_4'),('RUS','art_5'),('RUS','esp_1'),('RUS','esp_2'),('RUS','ew_1'),('RUS','ew_2'),('RUS','doc_5'),
 ('UKR','inf_5'),('UKR','inf_6'),('UKR','drones_4'),('UKR','ew_1'),('UKR','ew_2'),('UKR','sup_1'),
 ('CHN','inf_4'),('CHN','arm_4'),('CHN','art_4'),('CHN','sup_1'),('CHN','ew_1'),('CHN','doc_5'),('CHN','res_2'),
 ('KOR','inf_4'),('KOR','art_4'),('KOR','art_5'),('KOR','sup_1'),('KOR','ew_1'),
 ('JPN','inf_4'),('JPN','sup_1'),('JPN','sup_3'),('JPN','ew_1'),('JPN','res_2'),
 ('IND','inf_7'),('IND','sup_1'),('IND','esp_1'),('IND','doc_5'),
 ('TUR','inf_4'),('TUR','drones_4'),('TUR','esp_1'),('TUR','sup_1'),
 ('POL','inf_5'),('POL','arm_4'),('POL','sup_1'),('POL','esp_1'),
 ('PRT','inf_9'),('PRT','esp_1'),('PRT','esp_3'),('PRT','sup_1'),
 ('ESP','inf_4'),('ESP','esp_1'),('ESP','sup_1'),
 ('ITA','inf_7'),('ITA','esp_1'),('ITA','sup_1'),
 ('AUS','inf_9'),('AUS','esp_1'),('AUS','sup_1'),
 ('CAN','inf_8'),('CAN','sup_1'),('CAN','esp_1'),
 ('BRA','inf_9'),('BRA','esp_1'),
 ('IRN','inf_5'),('IRN','drones_4'),('IRN','esp_5'),
 ('PRK','art_4'),('PRK','art_5'),('PRK','esp_1'),
 ('SAU','arm_4'),('SAU','art_5'),
 ('EGY','inf_4'),('EGY','sup_1'),
 ('PAK','inf_7'),('PAK','esp_1'),
 ('IDN','inf_9'),
 ('ARG','inf_9'),
 ('MOZ','inf_9'),
 ('AGO','inf_9');
