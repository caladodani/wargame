-- PRT (k=0) — Exército Português, ordem de batalha aproximada 2024-2026.
-- Fontes: Brigada de Reação Rápida (Tancos), Brigada Mecanizada (Santa Margarida, Leopard 2A6 + Pandur II),
-- Brigada de Intervenção (Porto/Coimbra/Viseu/Vila Real), Zonas Militares da Madeira e dos Açores,
-- regimentos de infantaria distritais, Regimento de Artilharia (Leiria/Vendas Novas), Regimento de Cavalaria.
-- Carácter: exército pequeno, profissional (sem conscrição), tradição de missões NATO/ONU, defesa do
-- território atlântico incluindo os arquipélagos.

-- ===== unit_type próprios (100+20*0 .. 119+20*0 = 100..119) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (100,'Comandos','ground',1.6,45,0.8,30),
 (101,'Paraquedistas','ground',1.5,40,0.9,35),
 (102,'Pandur II','ground',2.0,40,1.3,50),
 (103,'Leopard 2A6','ground',6.5,75,2.0,38);

INSERT INTO unit_stat VALUES
 (100,'soft_atk',8), (100,'hard_atk',1.5),(100,'defense',22),(100,'breakthrough',11),(100,'armor',0), (100,'piercing',6), (100,'hardness',0.1),(100,'hp',22),
 (101,'soft_atk',7), (101,'hard_atk',1),  (101,'defense',20),(101,'breakthrough',11),(101,'armor',0), (101,'piercing',5), (101,'hardness',0.1),(101,'hp',20),
 (102,'soft_atk',9), (102,'hard_atk',3),  (102,'defense',24),(102,'breakthrough',14),(102,'armor',8), (102,'piercing',15),(102,'hardness',0.4),(102,'hp',26),
 (103,'soft_atk',13),(103,'hard_atk',17), (103,'defense',14),(103,'breakthrough',30),(103,'armor',72),(103,'piercing',63),(103,'hardness',0.92),(103,'hp',22);

INSERT INTO unit_tag VALUES
 (100,'infantry'),(100,'ground'),(100,'especial'),
 (101,'infantry'),(101,'ground'),(101,'especial'),(101,'airborne'),
 (102,'infantry'),(102,'ground'),
 (103,'armored'),(103,'ground');

-- ===== espíritos nacionais + modificadores (ids 100..119) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('PRT_tradicao_expedicionaria','PRT','Tradição Expedicionária',
   'Décadas de missões da NATO e da ONU (Kosovo, Afeganistão, Líbano, República Centro-Africana) deram às forças especiais experiência de combate real e melhoraram a coordenação de comando.'),
 ('PRT_exercito_profissional','PRT','Forças Pequenas mas Profissionais',
   'Sem conscrição em massa, o Exército assenta em voluntários bem treinados: menos divisões, mas cada uma luta e recupera organização acima da média.'),
 ('PRT_defesa_atlantica','PRT','Defesa Atlântica e Insular',
   'Doutrina centrada na defesa do território continental montanhoso e dos arquipélagos dos Açores e da Madeira: as tropas portuguesas rendem mais em terreno montanhoso.'),
 ('PRT_alianca_atlantica','PRT','Aliança Atlântica',
   'Membro fundador da NATO, com décadas de exercícios e comando integrados: maior eficiência de comando em operações conjuntas.'),
 ('PRT_veteranos','PRT','Espírito de Resistência',
   'A memória histórica de linhas defensivas bem-sucedidas (Torres Vedras, Aljubarrota) reforça a moral e a firmeza defensiva em território próprio, sobretudo atrás de linhas de água.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (100,'spirit',NULL,NULL,     'str_attacker','especial','mul',1.20,'PRT','PRT_tradicao_expedicionaria'),
 (101,'spirit',NULL,NULL,     'command',     NULL,      'mul',1.05,'PRT','PRT_tradicao_expedicionaria'),
 (102,'spirit',NULL,NULL,     'str_defender',NULL,      'mul',1.08,'PRT','PRT_exercito_profissional'),
 (103,'spirit','terrain','mountain','str_defender',NULL,'mul',1.15,'PRT','PRT_defesa_atlantica'),
 (104,'spirit','terrain','mountain','str_attacker',NULL,'mul',1.10,'PRT','PRT_defesa_atlantica'),
 (105,'spirit',NULL,NULL,     'command',     NULL,      'mul',1.10,'PRT','PRT_alianca_atlantica'),
 (106,'spirit',NULL,NULL,     'str_defender',NULL,      'add',0.10,'PRT','PRT_veteranos'),
 (107,'spirit','river','true','str_defender',NULL,      'add',0.05,'PRT','PRT_veteranos');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('PRT','production_speed',0.95),
 ('PRT','org_regain',1.25),
 ('PRT','start_army_mult',0.6);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('PRT','República semipresidencialista','Presidente da República e Comandante Supremo das Forças Armadas',
  'Defesa territorial ligeira, forças de reação rápida e interoperabilidade total com a NATO.',
  'NATO',
  'Portugal mantém um exército pequeno mas inteiramente profissional, moldado por décadas de missões internacionais da NATO e da ONU. A prioridade é a defesa do território continental e dos arquipélagos atlânticos, apoiada numa Brigada de Reação Rápida capaz de projectar força além-fronteiras. A indústria de defesa é modesta, mas a experiência operacional e a integração aliada compensam a escala reduzida.');

-- ===== templates próprios (ids 1..50) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1,'PRT','Brigada de Reação Rápida'),
 (2,'PRT','Brigada Mecanizada Pesada'),
 (3,'PRT','Brigada de Intervenção'),
 (4,'PRT','Regimento de Artilharia');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1,100,3),(1,101,3),(1,1,2),(1,4,1),
 (2,103,3),(2,102,4),(2,4,1),(2,1,1),
 (3,102,5),(3,1,3),(3,6,1),
 (4,4,4),(4,1,3),(4,5,1);

-- ===== brigadas/regimentos reais nomeados (ids 1..50) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (5,'PRT','Brigada de Reação Rápida','Brigada de Reação Rápida','Santarém'),
 (6,'PRT','Brigada Mecanizada','Brigada Mecanizada Pesada','Santarém'),
 (7,'PRT','Brigada de Intervenção','Brigada de Intervenção','Porto'),
 (8,'PRT','Regimento de Infantaria de Coimbra','Infantaria','Coimbra'),
 (9,'PRT','Regimento de Infantaria de Viseu','Infantaria','Viseu'),
 (10,'PRT','Regimento de Infantaria de Vila Real','Infantaria','Vila Real'),
 (11,'PRT','Zona Militar da Madeira','Infantaria','Madeira'),
 (12,'PRT','Zona Militar dos Açores','Infantaria','Azores'),
 (13,'PRT','Regimento de Infantaria do Porto','Infantaria','Porto'),
 (14,'PRT','Regimento de Infantaria de Lisboa','Infantaria','Lisboa'),
 (15,'PRT','Regimento de Infantaria de Braga','Infantaria','Braga'),
 (16,'PRT','Regimento de Artilharia de Leiria','Regimento de Artilharia','Leiria'),
 (17,'PRT','Grupo de Artilharia de Vendas Novas','Regimento de Artilharia','Évora'),
 (18,'PRT','Regimento de Cavalaria de Braga','Infantaria AT','Braga'),
 (19,'PRT','Regimento de Cavalaria de Estremoz','Infantaria AT','Portalegre'),
 (20,'PRT','Regimento de Infantaria de Castelo Branco','Infantaria','Castelo Branco');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='PRT')
   AND name IN ('Bragança','Vila Real','Guarda','Viseu','Castelo Branco','Viana do Castelo','Madeira','Azores');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('prt_atlantico','PRT','Vocação Atlântica','Portugal volta-se para o mar: cooperação naval e científica com os aliados.',35,NULL,1),
 ('prt_nato','PRT','Pilar da NATO','Integração profunda nas estruturas da Aliança: comando, doutrina e exercícios.',42,'prt_atlantico',2),
 ('prt_lusofonia','PRT','Comunidade Lusófona','A CPLP como rede económica e diplomática de Lisboa.',35,'prt_atlantico',3),
 ('prt_reequipamento','PRT','Lei de Programação Militar','Reequipamento plurianual: Pandur, F-16 MLU, fragatas modernizadas.',42,NULL,4),
 ('prt_industria_defesa','PRT','Indústria de Defesa Nacional','OGMA, Arsenal do Alfeite e novas tecnológicas puxam pela economia.',49,'prt_reequipamento',5),
 ('prt_servico_militar','PRT','Reserva Mobilizável','Recenseamento renovado e incentivos à reserva: mais homens disponíveis.',35,NULL,6),
 ('prt_comandos','PRT','Tradição dos Comandos','As forças especiais formam o núcleo duro de um exército pequeno mas afiado.',42,'prt_servico_militar',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('prt_atlantico','research_speed',1.05),
 ('prt_nato','org_regain',1.10),
 ('prt_lusofonia','industry',1.05),
 ('prt_reequipamento','production_speed',1.10),
 ('prt_industria_defesa','industry',1.08),
 ('prt_servico_militar','conscription',1.25),
 ('prt_comandos','org_regain',1.05),
 ('prt_comandos','conscription',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('prt_nato','prt_lusofonia');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('prt_comandos','prt_atlantico');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('PRT_adv_estaleiros','PRT','economia','Mestre dos Estaleiros do Tejo','🛳',180,'Os cais dele despacham o dobro da carga.'),
 ('PRT_adv_ultramar','PRT','seguranca','Veterano do Ultramar','🎖',175,'Fez guerra em três continentes e ensina-a.');
INSERT INTO advisor_effect VALUES ('PRT_adv_estaleiros','port_capacity',1.25);
INSERT INTO advisor_effect VALUES ('PRT_adv_estaleiros','industry',1.08);
INSERT INTO advisor_effect VALUES ('PRT_adv_ultramar','org_regain',1.12);
INSERT INTO advisor_effect VALUES ('PRT_adv_ultramar','defense',1.05);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('PRT_mar','Economia do Mar','⚓',10,'PRT');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('PRT_law_pescas','PRT_mar','Pesca e cabotagem','A frota pesca e o cais despacha o que houver.',0,1,'PRT'),
 ('PRT_law_plataforma','PRT_mar','Plataforma continental alargada','O mar do país passa a ser quatro vezes o continente.',1,0,'PRT'),
 ('PRT_law_acores','PRT_mar','Base atlântica dos Açores','A meio do oceano, quem lá está manda na travessia.',2,0,'PRT');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('PRT_law_pescas','export_share',1.1),
 ('PRT_law_plataforma','research_speed',1.08),
 ('PRT_law_plataforma','industry',1.05),
 ('PRT_law_acores','defense',1.1),
 ('PRT_law_acores','org_regain',1.06),
 ('PRT_law_acores','export_price',1.1);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('PRT_gen_comandos_prt','Chefe dos Comandos','attack',1.15,135,'PRT','🇵🇹','Três guerras em África e a boina ainda se ganha a suar.'),
 ('PRT_gen_atlantico_prt','Comandante do Comando Atlântico','org_regain',1.15,130,'PRT','⚓','Sustenta forças a mil milhas de casa como quem manda um recado.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('PRT_escola','Escola Expedicionária','🇵🇹',10,'PRT');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('PRT_doc_africa','PRT_escola','Lição de África','Três guerras longe de casa ensinaram a fazer muito com pouco.',50,NULL,1,'PRT'),
 ('PRT_doc_comandos','PRT_escola','Comandos','A boina ganha-se a suar e a companhia entra primeiro.',110,'PRT_doc_africa',2,'PRT'),
 ('PRT_doc_atlantico','PRT_escola','Comando Atlântico','Sustentar forças a mil milhas de casa é a especialidade da casa.',190,'PRT_doc_comandos',3,'PRT');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('PRT_doc_africa','org_regain',1.07),
 ('PRT_doc_comandos','attack',1.07),
 ('PRT_doc_comandos','move_speed',1.05),
 ('PRT_doc_atlantico','move_speed',1.08),
 ('PRT_doc_atlantico','org_regain',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('PRT_ar','Asas do Atlântico','🛩',11,'PRT','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('PRT_ar_lajes','PRT_ar','Placa dos Açores','Uma pista no meio do oceano vale por uma esquadra.',50,NULL,1,'PRT'),
 ('PRT_ar_busca','PRT_ar','Busca e Salvamento','A maior área de busca da Europa treina quem voa longe de terra.',110,'PRT_ar_lajes',2,'PRT'),
 ('PRT_ar_africa_ar','PRT_ar','Apoio em África','Três guerras a apoiar colunas em mato ensinaram a largar onde é preciso.',185,'PRT_ar_busca',3,'PRT');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('PRT_ar_lajes','air_upkeep',0.92),
 ('PRT_ar_busca','air_losses',0.92),
 ('PRT_ar_africa_ar','air_bombing',1.09);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('PRT_mar','Escola do Mar Largo','🧭',12,'PRT','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('PRT_mar_descobrimentos','PRT_mar','Arte de Navegar','Quinhentos anos a ir aonde ninguém tinha ido deixaram maneira de estar no mar.',50,NULL,1,'PRT'),
 ('PRT_mar_zee','PRT_mar','Guarda da Zona Económica','A maior zona de mar da Europa vigia-se com poucos navios e muita rota.',110,'PRT_mar_descobrimentos',2,'PRT'),
 ('PRT_mar_acores','PRT_mar','Triângulo dos Açores','Do meio do Atlântico chega-se aos dois lados antes dos dois lados.',185,'PRT_mar_zee',3,'PRT');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('PRT_mar_descobrimentos','naval_patrol',1.1),
 ('PRT_mar_zee','naval_escort',1.1),
 ('PRT_mar_acores','naval_losses',0.93);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('PRT_ar_gen_patrulha_prt','Chefe da Patrulha Atlântica','air_losses',0.88,145,'PRT','🕊','Voa horas sobre água sem terra à vista e traz todos de volta.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('PRT_mar_gen_costa_prt','Almirante da Costa Atlântica','naval_escort',1.16,145,'PRT','🌊','Leva o comboio ao porto com a mesma conta com que o levava a Goa.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('PRT_tech_ar_lajes','Aviação','Ponte Aérea das Lajes',260,'air_1','Uma base no meio do Atlântico transforma a travessia numa escala: a asa destacada custa menos.','PRT'),
 ('PRT_tech_mar_fragatas_zee','Marinha','Fragatas da Zona Económica',260,'nav_1','A maior zona económica da Europa vigiada por poucos navios que nunca estão no porto.','PRT');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('PRT_tech_ar_lajes','air_upkeep',0.86),
 ('PRT_tech_mar_fragatas_zee','naval_patrol',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'Brigadeiro-General',0,0,'PRT'),
 ('exercito',2,'Major-General',40,0.5,'PRT'),
 ('exercito',3,'Tenente-General',110,1,'PRT'),
 ('exercito',4,'General',220,1.75,'PRT'),
 ('exercito',5,'Marechal de Portugal',360,2.5,'PRT'),
 ('ar',1,'Brigadeiro-General do Ar',0,0,'PRT'),
 ('ar',2,'Major-General do Ar',40,0.5,'PRT'),
 ('ar',3,'Tenente-General do Ar',110,1,'PRT'),
 ('ar',4,'General do Ar',220,1.75,'PRT'),
 ('ar',5,'Chefe do Estado-Maior da Força Aérea',360,2.5,'PRT'),
 ('mar',1,'Comodoro',0,0,'PRT'),
 ('mar',2,'Contra-Almirante da Armada',40,0.5,'PRT'),
 ('mar',3,'Vice-Almirante da Armada',110,1,'PRT'),
 ('mar',4,'Almirante',220,1.75,'PRT'),
 ('mar',5,'Almirante da Armada Portuguesa',360,2.5,'PRT');

-- ===== condecorações nacionais (medal.country_tag) =====
INSERT INTO medal (id,name,description,metric,threshold,bonus,sort,country_tag) VALUES
 ('PRT_baptismo','Medalha de Comportamento Exemplar','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1,'PRT'),
 ('PRT_assalto','Cruz de Guerra','Tomou três regiões ao inimigo.','captures',3,0.03,2,'PRT'),
 ('PRT_campanha','Louvor do Chefe do Estado-Maior','Quarenta pontos de experiência em combate.','xp',40,0.02,3,'PRT'),
 ('PRT_aco','Medalha de Valor Militar','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4,'PRT'),
 ('PRT_imortais','Ordem da Torre e Espada','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5,'PRT');

-- ===== nomes de formação nacionais (formation_name.country_tag) =====
INSERT INTO formation_name (id,name,domain,sort,country_tag) VALUES
 ('PRT_ar_1','Esquadra 201 Falcões','ar',1,'PRT'),
 ('PRT_ar_2','Esquadra 301 Jaguares','ar',2,'PRT'),
 ('PRT_ar_3','Esquadra 601 Lobos','ar',3,'PRT'),
 ('PRT_mar_1','Esquadra do Atlântico','mar',1,'PRT'),
 ('PRT_mar_2','Flotilha do Tejo','mar',2,'PRT'),
 ('PRT_mar_3','Divisão Naval dos Açores','mar',3,'PRT');
