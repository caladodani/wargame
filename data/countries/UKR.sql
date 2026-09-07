-- UKR (k=14) — Zbroyni Syly Ukrayiny, ordem de batalha aproximada 2024-2026 (guerra em curso).
-- Fontes: 3ª e 5ª Brigadas de Assalto Separadas, 10ª Brigada de Montanha "Edelweiss", 24ª Brigada
-- Mecanizada "Rei Danylo", 28ª Brigada Mecanizada "Rainha Ana Yaroslavna", 92ª e 93ª Brigadas
-- Mecanizadas, 1ª Brigada Blindada "Zaporizhzhya Sich", 4ª Brigada Blindada, 44ª e 26ª Brigadas de
-- Artilharia, 95ª e 80ª Brigadas de Assalto Aéreo, Defesa Territorial de Kyiv.
-- Carácter: exército de guerra experiente, muito forte em defesa e uso massivo de drones e
-- artilharia de longo alcance; indústria danificada pela invasão russa.

-- ===== unit_type próprios (100+20*14 .. 119+20*14 = 380..399) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (380,'Drone FPV/Reconhecimento','support',0.7,20,0.6,20),
 (381,'Artilharia de Longo Alcance','support',2.6,50,1.6,22),
 (382,'Brigada Blindada T-64/Leopard','ground',4.2,62,2.1,36),
 (383,'Infantaria de Assalto','ground',1.2,32,1.0,24);

INSERT INTO unit_stat VALUES
 (380,'soft_atk',3), (380,'hard_atk',6), (380,'defense',4), (380,'breakthrough',2), (380,'armor',0), (380,'piercing',30),(380,'hardness',0.15),(380,'hp',5),
 (381,'soft_atk',28),(381,'hard_atk',3),(381,'defense',6), (381,'breakthrough',6), (381,'armor',0), (381,'piercing',12),(381,'hardness',0.2), (381,'hp',6),
 (382,'soft_atk',12),(382,'hard_atk',15),(382,'defense',13),(382,'breakthrough',29),(382,'armor',58),(382,'piercing',56),(382,'hardness',0.88),(382,'hp',21),
 (383,'soft_atk',8), (383,'hard_atk',1.5), (383,'defense',26),(383,'breakthrough',10),(383,'armor',0), (383,'piercing',6), (383,'hardness',0.12),(383,'hp',28);

INSERT INTO unit_tag VALUES
 (380,'support'),(380,'ground'),
 (381,'support'),(381,'ground'),
 (382,'armored'),(382,'ground'),
 (383,'infantry'),(383,'ground'),(383,'veterano');

-- ===== espíritos nacionais + modificadores (ids 380..399) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('UKR_veteranos_de_guerra','UKR','Veteranos de Guerra',
   'Anos de combate real endureceram a tropa: forte bónus defensivo e ligeiro bónus ofensivo em todas as unidades.'),
 ('UKR_guerra_de_drones','UKR','Guerra de Drones',
   'O uso massivo de drones de reconhecimento e ataque melhora a precisão e o apoio das unidades de suporte em qualquer ofensiva.'),
 ('UKR_defesa_urbana','UKR','Defesa Urbana Tenaz',
   'A experiência de Bakhmut, Mariupol e Avdiivka ensinou a defender cidade a cidade: bónus defensivo extra em terreno urbano.'),
 ('UKR_mobilizacao_total','UKR','Mobilização Total',
   'A economia de guerra e a mobilização em massa melhoram a coordenação de comando apesar da pressão constante da linha da frente.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (380,'spirit',NULL,NULL,        'str_defender',NULL,     'mul',1.25,'UKR','UKR_veteranos_de_guerra'),
 (381,'spirit',NULL,NULL,        'str_attacker',NULL,     'mul',1.05,'UKR','UKR_veteranos_de_guerra'),
 (382,'spirit',NULL,NULL,        'str_attacker','support','mul',1.15,'UKR','UKR_guerra_de_drones'),
 (383,'spirit','terrain','urban','str_defender',NULL,     'mul',1.20,'UKR','UKR_defesa_urbana'),
 (384,'spirit',NULL,NULL,        'command',     NULL,     'mul',1.08,'UKR','UKR_mobilizacao_total');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('UKR','industry',0.5),
 ('UKR','production_speed',0.75),
 ('UKR','org_regain',1.05),
 ('UKR','start_army_mult',1.40);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('UKR','República semipresidencialista (lei marcial)','Comandante-em-Chefe das Forças Armadas',
  'Defesa elástica em profundidade, guerra de drones e artilharia de longo alcance, mobilização total da população em idade militar.',
  'Não-alinhado (candidato à NATO)',
  'Depois de mais de dois anos de guerra total, a Ucrânia dispõe de um dos exércitos mais experientes da Europa, moldado por combate constante em terreno urbano e rural. A indústria de defesa, danificada por bombardeamentos sistemáticos, é parcialmente compensada por produção descentralizada de drones e por ajuda externa. A doutrina assenta em defesa tenaz, contra-ataques localizados e um uso intensivo de reconhecimento não tripulado para guiar a artilharia.');

-- ===== templates próprios (ids 701..750) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (701,'UKR','Brigada Mecanizada de Choque'),
 (702,'UKR','Batalhão de Assalto e Drones'),
 (703,'UKR','Regimento de Artilharia Pesada');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (701,382,3),(701,2,3),(701,381,2),
 (702,383,4),(702,380,3),(702,6,1),
 (703,381,4),(703,1,3),(703,5,1);

-- ===== brigadas reais nomeadas (ids 704..750) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (704,'UKR','3ª Brigada de Assalto Separada','Batalhão de Assalto e Drones','Kharkiv'),
 (705,'UKR','5ª Brigada de Assalto Separada','Batalhão de Assalto e Drones',"Donets'k"),
 (706,'UKR','10ª Brigada de Montanha "Edelweiss"','Infantaria',"Ivano-Frankivs'k"),
 (707,'UKR','24ª Brigada Mecanizada "Rei Danylo"','Brigada Mecanizada de Choque',"Donets'k"),
 (708,'UKR','28ª Brigada Mecanizada "Rainha Ana Yaroslavna"','Brigada Mecanizada de Choque','Zaporizhzhya'),
 (709,'UKR','92ª Brigada Mecanizada Separada','Mecanizada','Kharkiv'),
 (710,'UKR','93ª Brigada Mecanizada "Kholodnyi Yar"','Mecanizada',"Donets'k"),
 (711,'UKR','1ª Brigada Blindada "Zaporizhzhya Sich"','Brigada Mecanizada de Choque','Zaporizhzhya'),
 (712,'UKR','4ª Brigada Blindada','Brigada Mecanizada de Choque','Kharkiv'),
 (713,'UKR','44ª Brigada de Artilharia','Regimento de Artilharia Pesada','Poltava'),
 (714,'UKR','26ª Brigada de Artilharia','Regimento de Artilharia Pesada','Kirovohrad'),
 (715,'UKR','95ª Brigada de Assalto Aéreo','Infantaria AT',"Dnipropetrovs'k"),
 (716,'UKR','80ª Brigada de Assalto Aéreo','Infantaria AT',"L'viv"),
 (717,'UKR','Brigada de Defesa Territorial de Kiev','Infantaria','Kiev City');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('ukr_resistencia_territorial','UKR','Defesa em Profundidade','Linhas de trincheiras e fortificações ao longo da frente oriental.',35,NULL,1),
 ('ukr_drones_faixa_construcao','UKR','Exército de Drones','Produção em massa de drones FPV e de reconhecimento para compensar a escassez de artilharia.',28,'ukr_resistencia_territorial',2),
 ('ukr_himars_precisao','UKR','Fogo de Precisão','Integração de sistemas de longo alcance ocidentais na doutrina de artilharia.',42,'ukr_drones_faixa_construcao',3),
 ('ukr_industria_defesa','UKR','Indústria de Defesa Ucraniana','Ukroboronprom reorganizada e dispersa para resistir a ataques aéreos.',49,NULL,4),
 ('ukr_ajuda_ocidental','UKR','Corredor de Ajuda Ocidental','Logística dedicada a receber e distribuir equipamento da NATO com rapidez.',35,'ukr_industria_defesa',5),
 ('ukr_mobilizacao_geral','UKR','Lei de Mobilização Geral','Recenseamento alargado e reforço das fileiras para sustentar a guerra de desgaste.',35,NULL,6),
 ('ukr_veteranos_assalto','UKR','Brigadas de Assalto Veteranas','Doutrina de assalto e reorganização das brigadas mais experientes em núcleos de choque.',42,'ukr_mobilizacao_geral',7),
 ('ukr_reconstrucao_energia','UKR','Escudo Energético','Reforço da defesa antiaérea sobre infraestrutura crítica para manter a produção a funcionar.',49,'ukr_industria_defesa',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('ukr_resistencia_territorial','org_regain',1.10),
 ('ukr_drones_faixa_construcao','production_speed',1.10),
 ('ukr_himars_precisao','research_speed',1.10),
 ('ukr_industria_defesa','industry',1.10),
 ('ukr_ajuda_ocidental','production_speed',1.10),
 ('ukr_mobilizacao_geral','conscription',1.25),
 ('ukr_veteranos_assalto','org_regain',1.08),
 ('ukr_reconstrucao_energia','industry',1.08);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('ukr_ajuda_ocidental','ukr_reconstrucao_energia');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('ukr_reconstrucao_energia','ukr_resistencia_territorial');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('UKR_adv_celeiro','UKR','economia','Administrador do Celeiro','🌾',165,'Vende trigo caro e paga a guerra com ele.'),
 ('UKR_adv_oficinas','UKR','ciencia','Oficina de Aparelhos','🛠',175,'Do quintal para a linha da frente em dias.');
INSERT INTO advisor_effect VALUES ('UKR_adv_celeiro','industry',1.1);
INSERT INTO advisor_effect VALUES ('UKR_adv_celeiro','export_price',1.05);
INSERT INTO advisor_effect VALUES ('UKR_adv_oficinas','research_speed',1.14);
INSERT INTO advisor_effect VALUES ('UKR_adv_oficinas','production_speed',1.06);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('UKR_mobilizacao','Mobilização','🌻',10,'UKR');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('UKR_law_contrato','UKR_mobilizacao','Contrato voluntário','Quem quer servir assina; os outros trabalham.',0,1,'UKR'),
 ('UKR_law_geral','UKR_mobilizacao','Mobilização geral','Idades chamadas por decreto, província a província.',1,0,'UKR'),
 ('UKR_law_nacao','UKR_mobilizacao','Nação em guerra','Não há retaguarda: toda a gente está na frente ou a servi-la.',2,0,'UKR');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('UKR_law_contrato','org_regain',1.05),
 ('UKR_law_geral','conscription',1.25),
 ('UKR_law_geral','defense',1.06),
 ('UKR_law_nacao','conscription',1.4),
 ('UKR_law_nacao','attack',1.08),
 ('UKR_law_nacao','industry',0.92);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('UKR_gen_drone_ukr','Chefe dos Drones','attack',1.16,130,'UKR','🌻','Guerra de vídeo e bateria: cada carro inimigo tem quem o siga.'),
 ('UKR_gen_defesa_ukr','Comandante da Defesa em Profundidade','defense',1.17,135,'UKR','🛡','Três linhas de trincheira e a certeza de que a primeira vai cair.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('UKR_escola','Guerra de Drones','🌻',10,'UKR');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('UKR_doc_drone_ukr','UKR_escola','Escola dos Drones','Cada carro inimigo tem quem o siga desde que sai do abrigo.',50,NULL,1,'UKR'),
 ('UKR_doc_trincheira','UKR_escola','Três Linhas de Trincheira','A primeira linha é para cair: as outras duas é que contam.',110,'UKR_doc_drone_ukr',2,'UKR'),
 ('UKR_doc_adaptacao','UKR_escola','Adaptação Contínua','O que funcionou no mês passado já não funciona: muda-se todos os meses.',190,'UKR_doc_trincheira',3,'UKR');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('UKR_doc_drone_ukr','attack',1.07),
 ('UKR_doc_trincheira','defense',1.08),
 ('UKR_doc_adaptacao','org_regain',1.07),
 ('UKR_doc_adaptacao','attack',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('UKR_ar','Asas Teimosas','🌻',11,'UKR','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('UKR_ar_fantasma','UKR_ar','Voo Baixo','A dez metros do chão não há míssil que aponte.',50,NULL,1,'UKR'),
 ('UKR_ar_improviso','UKR_ar','Improviso de Armamento','Pendurou-se num caça soviético uma arma ocidental, e resultou.',110,'UKR_ar_fantasma',2,'UKR'),
 ('UKR_ar_dispersao_ukr','UKR_ar','Aeródromos Dispersos','Nunca dois aviões no mesmo sítio na mesma noite.',185,'UKR_ar_improviso',3,'UKR');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('UKR_ar_fantasma','air_losses',0.9),
 ('UKR_ar_improviso','air_bombing',1.1),
 ('UKR_ar_dispersao_ukr','air_upkeep',0.92),
 ('UKR_ar_dispersao_ukr','air_losses',0.96);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('UKR_mar','Mar Teimoso','🌻',12,'UKR','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('UKR_mar_neptune','UKR_mar','Míssil Costeiro','Afundar o navio-almirante da esquadra inimiga sem se ter esquadra.',50,NULL,1,'UKR'),
 ('UKR_mar_drone_mar','UKR_mar','Drone de Superfície','Um barco sem ninguém dentro entra no porto do outro e não volta.',110,'UKR_mar_neptune',2,'UKR'),
 ('UKR_mar_corredor','UKR_mar','Corredor de Cereal','Abrir uma rota de exportação debaixo de guerra é ganhar sem esquadra.',185,'UKR_mar_drone_mar',3,'UKR');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('UKR_mar_neptune','naval_blockade',1.12),
 ('UKR_mar_drone_mar','naval_losses',0.9),
 ('UKR_mar_corredor','naval_escort',1.1);
