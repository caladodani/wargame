-- JPN (k=9) — Japan Ground Self-Defense Force (JGSDF), ordem de batalha aproximada 2024-2026.
-- Fontes: 1ª Divisão Blindada (Kita-Chitose), Brigada de Desembarque Anfíbio Rápido (Nagasaki, ARDB,
-- estilo fuzileiros), Divisões de Infantaria de Hokkaidō e Kyushu, Type 10/16 e Type 87, unidades de
-- defesa das ilhas do sudoeste (Okinawa, Amami), NEC/Type 03 AA. Carácter: doutrina defensiva imposta
-- pela Constituição (Art. 9º) — ataque penalizado, defesa e defesa costeira/ilhas reforçadas; tecnologia
-- de ponta compensa a escala historicamente limitada.

-- ===== unit_type próprios (100+20*9 .. 119+20*9 = 280..299) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (280,'Type 10','ground',6.8,70,2.1,42),
 (281,'Brigada Anfíbia (ARDB)','ground',2.2,50,1.1,32),
 (282,'Infantaria de Defesa Insular','ground',1.3,35,0.9,22);

INSERT INTO unit_stat VALUES
 (280,'soft_atk',13),(280,'hard_atk',18),(280,'defense',15),(280,'breakthrough',31),(280,'armor',75),(280,'piercing',68),(280,'hardness',0.92),(280,'hp',23),
 (281,'soft_atk',9), (281,'hard_atk',3), (281,'defense',22),(281,'breakthrough',13),(281,'armor',5), (281,'piercing',14),(281,'hardness',0.25),(281,'hp',24),
 (282,'soft_atk',6), (282,'hard_atk',1), (282,'defense',30),(282,'breakthrough',7), (282,'armor',0), (282,'piercing',5), (282,'hardness',0.1), (282,'hp',26);

INSERT INTO unit_tag VALUES
 (280,'armored'),(280,'ground'),
 (281,'infantry'),(281,'ground'),(281,'especial'),
 (282,'infantry'),(282,'ground');

-- ===== espíritos nacionais + modificadores (ids 280..299) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('JPN_constituicao_pacifista','JPN','Constituição Pacifista (Art. 9º)',
   'O Artigo 9º limita as forças a um papel de autodefesa: unidades japonesas atacam com menos convicção do que defendem.'),
 ('JPN_defesa_arquipelago','JPN','Defesa do Arquipélago',
   'Décadas de planeamento em torno da defesa das ilhas do sudoeste (Okinawa, Ryukyu) tornam as tropas japonesas muito mais eficazes a defender terreno urbano e costeiro.'),
 ('JPN_industria_tecnologica','JPN','Base Industrial de Alta Tecnologia',
   'Mitsubishi Heavy Industries e Kawasaki sustentam blindados e electrónica de ponta: maior eficácia de comando e melhor desempenho dos blindados próprios.'),
 ('JPN_alianca_eua','JPN','Aliança Nipo-Americana',
   'O tratado de segurança com os EUA garante interoperabilidade e apoio logístico contínuo, reforçando a recuperação de organização das forças.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (280,'spirit',NULL,NULL,          'str_attacker',NULL,      'mul',0.85,'JPN','JPN_constituicao_pacifista'),
 (281,'spirit',NULL,NULL,          'str_defender',NULL,      'mul',1.15,'JPN','JPN_constituicao_pacifista'),
 (282,'spirit','terrain','urban',  'str_defender',NULL,      'mul',1.25,'JPN','JPN_defesa_arquipelago'),
 (283,'spirit','river','true',     'str_defender',NULL,      'add',0.10,'JPN','JPN_defesa_arquipelago'),
 (284,'spirit',NULL,NULL,          'command',     NULL,      'mul',1.10,'JPN','JPN_industria_tecnologica'),
 (285,'spirit',NULL,NULL,          'str_attacker','armored', 'mul',1.15,'JPN','JPN_industria_tecnologica'),
 (286,'spirit',NULL,NULL,          'command',     NULL,      'mul',1.08,'JPN','JPN_alianca_eua');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('JPN','production_speed',1.15),
 ('JPN','org_regain',1.10),
 ('JPN','start_army_mult',0.7);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('JPN','Monarquia constitucional parlamentar','Primeiro-Ministro do Japão',
  'Defesa exclusivamente territorial: prioridade à defesa das ilhas do sudoeste, dissuasão tecnológica e cooperação estreita com os EUA.',
  'Não-alinhado (tratado bilateral com os EUA)',
  'O Japão mantém uma das forças terrestres tecnologicamente mais avançadas da Ásia, mas constitucionalmente limitada a um papel defensivo. As Forças de Autodefesa Terrestres (JGSDF) concentram-se na proteção do arquipélago, com particular atenção às ilhas do sudoeste face à pressão regional. A indústria de defesa nacional (Mitsubishi, Kawasaki) garante blindados e sistemas de deteção de ponta, compensando um efetivo historicamente contido.');

-- ===== templates próprios (ids 1+50*9..50+50*9 = 451..500) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (451,'JPN','Divisão Blindada'),
 (452,'JPN','Brigada de Desembarque Anfíbio'),
 (453,'JPN','Divisão de Defesa Insular');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (451,280,4),(451,2,2),(451,4,1),(451,5,1),
 (452,281,4),(452,1,2),(452,6,1),
 (453,282,5),(453,1,2),(453,6,1);

-- ===== brigadas/regimentos reais nomeados (ids 451..500) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (454,'JPN','1ª Divisão Blindada','Divisão Blindada','Hokkaidō'),
 (455,'JPN','7ª Divisão (Kita-Chitose)','Divisão Blindada','Hokkaidō'),
 (456,'JPN','2ª Divisão de Infantaria','Infantaria','Hokkaidō'),
 (457,'JPN','Brigada de Desembarque Anfíbio Rápido','Brigada de Desembarque Anfíbio','Nagasaki'),
 (458,'JPN','8ª Divisão (Kumamoto)','Infantaria','Kumamoto'),
 (459,'JPN','15ª Brigada de Defesa de Okinawa','Divisão de Defesa Insular','Okinawa'),
 (460,'JPN','3ª Divisão (Hyōgo)','Mecanizada','Hyōgo'),
 (461,'JPN','9ª Divisão (Aomori)','Infantaria','Aomori'),
 (462,'JPN','12ª Brigada (Fukushima)','Infantaria','Fukushima'),
 (463,'JPN','1ª Brigada de Artilharia','Infantaria AT','Aichi');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='JPN')
   AND name IN ('Nagano','Gifu','Yamanashi','Tottori');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('jpn_shudanteki_jieiken','JPN','Direito de Autodefesa Coletiva','A reinterpretação constitucional de 2015 permite às Forças de Autodefesa apoiar aliados sob ataque, reforçando a doutrina de resposta conjunta.',35,NULL,1),
 ('jpn_boei_sochi','JPN','Nova Estratégia de Segurança Nacional','O compromisso de elevar a despesa de defesa para 2% do PIB financia um reequipamento plurianual sem precedentes.',42,NULL,2),
 ('jpn_jieitai_recrutamento','JPN','Campanha de Recrutamento das Forças de Autodefesa','Face à quebra demográfica, novos incentivos salariais e digitais tentam colmatar o défice crónico de recrutas.',28,NULL,3),
 ('jpn_nanseishoto','JPN','Reforço das Ilhas do Sudoeste','Guarnições e radares espalham-se pela cadeia Nansei, de Kyushu a Yonaguni, para vigiar o Estreito de Miyako.',49,'jpn_shudanteki_jieiken',4),
 ('jpn_mitsubishi_kawasaki','JPN','Eixo Mitsubishi-Kawasaki','A indústria pesada nacional expande linhas de blindados, mísseis e aviónica sob encomenda direta do Ministério da Defesa.',42,'jpn_boei_sochi',5),
 ('jpn_reserva_ampliada','JPN','Reserva Ampliada','Reservistas e antigos militares são reintegrados em unidades territoriais para aliviar a pressão sobre o efetivo permanente.',30,'jpn_jieitai_recrutamento',6),
 ('jpn_contra_ataque','JPN','Capacidade de Contra-Ataque','Mísseis de longo alcance e sistemas de deteção antecipada dão ao Japão uma resposta credível para além do território.',56,'jpn_nanseishoto',7),
 ('jpn_f35_reequipamento','JPN','Reequipamento F-35 e Aegis','A frota de caças de quinta geração e o sistema antimíssil naval Aegis chegam em maior número às bases operacionais.',49,'jpn_mitsubishi_kawasaki',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('jpn_shudanteki_jieiken','org_regain',1.08),
 ('jpn_boei_sochi','industry',1.08),
 ('jpn_jieitai_recrutamento','conscription',1.15),
 ('jpn_nanseishoto','org_regain',1.10),
 ('jpn_mitsubishi_kawasaki','industry',1.09),
 ('jpn_mitsubishi_kawasaki','production_speed',1.07),
 ('jpn_reserva_ampliada','conscription',1.12),
 ('jpn_contra_ataque','research_speed',1.12),
 ('jpn_f35_reequipamento','production_speed',1.10),
 ('jpn_f35_reequipamento','research_speed',1.06);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('jpn_f35_reequipamento','jpn_shudanteki_jieiken');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('JPN_adv_conglomerado','JPN','economia','Chefe do Conglomerado','🏯',200,'Uma casa só, e manda em estaleiros e aciarias.'),
 ('JPN_adv_robotica','JPN','ciencia','Mestre da Robótica','🤖',195,'Máquinas a fazer máquinas.');
INSERT INTO advisor_effect VALUES ('JPN_adv_conglomerado','production_speed',1.18);
INSERT INTO advisor_effect VALUES ('JPN_adv_robotica','research_speed',1.18);
INSERT INTO advisor_effect VALUES ('JPN_adv_robotica','industry',1.05);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('JPN_artigo9','Artigo 9.º','⛩',10,'JPN');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('JPN_law_estrita','JPN_artigo9','Autodefesa estrita','A constituição proíbe a guerra e a economia agradece.',0,1,'JPN'),
 ('JPN_law_reinterpretacao','JPN_artigo9','Reinterpretação','Defesa colectiva: pode-se ajudar quem nos ajuda.',1,0,'JPN'),
 ('JPN_law_revisao','JPN_artigo9','Revisão constitucional','As Forças de Autodefesa passam a exército, com o nome e tudo.',2,0,'JPN');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('JPN_law_estrita','industry',1.08),
 ('JPN_law_estrita','research_speed',1.05),
 ('JPN_law_estrita','conscription',0.85),
 ('JPN_law_reinterpretacao','conscription',1.1),
 ('JPN_law_reinterpretacao','defense',1.08),
 ('JPN_law_revisao','attack',1.12),
 ('JPN_law_revisao','conscription',1.2),
 ('JPN_law_revisao','industry',0.95);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('JPN_gen_ilhas_jpn','Comandante da Defesa Insular','defense',1.17,135,'JPN','⛩','Cada ilha é um forte e ele conhece a maré de todas.'),
 ('JPN_gen_anfibio_jpn','Chefe da Brigada Anfíbia','attack',1.14,145,'JPN','🌊','Retoma ilha tomada, que é a única ofensiva que a lei deixa treinar.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('JPN_escola','Defesa Insular','⛩',10,'JPN');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('JPN_doc_insular','JPN_escola','Escola da Defesa Insular','Cada ilha é um forte e a maré faz parte do plano.',50,NULL,1,'JPN'),
 ('JPN_doc_recuperacao','JPN_escola','Recuperação de Ilhas','Retomar ilha tomada é a única ofensiva que a lei deixa treinar.',110,'JPN_doc_insular',2,'JPN'),
 ('JPN_doc_tecnologia','JPN_escola','Exército Técnico','Poucos homens, muita máquina: é a conta que o país sabe fazer.',190,'JPN_doc_recuperacao',3,'JPN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('JPN_doc_insular','defense',1.08),
 ('JPN_doc_recuperacao','attack',1.07),
 ('JPN_doc_recuperacao','org_regain',1.05),
 ('JPN_doc_tecnologia','production_speed',1.06),
 ('JPN_doc_tecnologia','defense',1.07);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('JPN_ar','Asas do Sol','🌸',11,'JPN','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('JPN_ar_zero','JPN_ar','Alcance de Caça','Um caça que vai mais longe do que o inimigo pensa aparece onde não devia.',50,NULL,1,'JPN'),
 ('JPN_ar_embarcada','JPN_ar','Aviação Embarcada','A pista anda com a esquadra e chega onde não há aeródromo.',110,'JPN_ar_zero',2,'JPN'),
 ('JPN_ar_manutencao','JPN_ar','Manutenção de Precisão','Poucos aparelhos, todos a voar todos os dias.',185,'JPN_ar_embarcada',3,'JPN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('JPN_ar_zero','air_losses',0.92),
 ('JPN_ar_embarcada','air_bombing',1.09),
 ('JPN_ar_manutencao','air_upkeep',0.9);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('JPN_mar','Esquadra Combinada','⚓',12,'JPN','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('JPN_mar_kantai','JPN_mar','Batalha Decisiva','Cem anos a preparar um dia só de combate de esquadras.',50,NULL,1,'JPN'),
 ('JPN_mar_long_lance','JPN_mar','Torpedo de Longo Alcance','Atacar de vinte quilómetros à noite, quando ninguém acha que dá.',110,'JPN_mar_kantai',2,'JPN'),
 ('JPN_mar_asw_jpn','JPN_mar','Guerra Anti-Submarina','A ilha vive do que entra por mar: o que caça submarinos é o que come.',185,'JPN_mar_long_lance',3,'JPN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('JPN_mar_kantai','naval_losses',0.91),
 ('JPN_mar_long_lance','naval_blockade',1.1),
 ('JPN_mar_asw_jpn','naval_escort',1.11);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('JPN_ar_gen_embarcada','Chefe da Asa Embarcada','air_losses',0.88,145,'JPN','✈','Levanta de um convés a balançar e volta a pousar nele.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('JPN_mar_gen_combinada','Almirante da Frota Combinada','naval_patrol',1.15,145,'JPN','🐟','Junta a esquadra toda num ponto do mapa à hora certa.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('JPN_tech_ar_intercepcao_insular','Aviação','Intercepção Insular',260,'air_1','Alerta permanente sobre milhares de ilhas: sobe-se muitas vezes ao dia e não se perde ninguém.','JPN'),
 ('JPN_tech_mar_escolta_izumo','Marinha','Esquadra de Escolta',260,'nav_1','Uma marinha feita de raiz para acompanhar comboios e caçar submarinos, e nada mais.','JPN');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('JPN_tech_ar_intercepcao_insular','air_losses',0.86),
 ('JPN_tech_mar_escolta_izumo','naval_escort',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'Comandante de Brigada',0,0,'JPN'),
 ('exercito',2,'General-Adjunto',40,0.5,'JPN'),
 ('exercito',3,'General de Divisão do Japão',110,1,'JPN'),
 ('exercito',4,'General do Exército',220,1.75,'JPN'),
 ('exercito',5,'Chefe do Estado-Maior Conjunto',360,2.5,'JPN'),
 ('ar',1,'Comandante de Asa',0,0,'JPN'),
 ('ar',2,'General-Adjunto do Ar',40,0.5,'JPN'),
 ('ar',3,'General de Divisão Aérea',110,1,'JPN'),
 ('ar',4,'General da Força Aérea',220,1.75,'JPN'),
 ('ar',5,'Chefe do Estado-Maior Aéreo',360,2.5,'JPN'),
 ('mar',1,'Capitão de Mar',0,0,'JPN'),
 ('mar',2,'Contra-Almirante do Japão',40,0.5,'JPN'),
 ('mar',3,'Vice-Almirante do Japão',110,1,'JPN'),
 ('mar',4,'Almirante do Japão',220,1.75,'JPN'),
 ('mar',5,'Chefe do Estado-Maior Naval',360,2.5,'JPN');

-- ===== condecorações nacionais (medal.country_tag) =====
INSERT INTO medal (id,name,description,metric,threshold,bonus,sort,country_tag) VALUES
 ('JPN_baptismo','Jugun Kisho','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1,'JPN'),
 ('JPN_assalto','Zuiho-sho','Tomou três regiões ao inimigo.','captures',3,0.03,2,'JPN'),
 ('JPN_campanha','Louvor do Estado-Maior','Quarenta pontos de experiência em combate.','xp',40,0.02,3,'JPN'),
 ('JPN_aco','Kyokujitsu-sho','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4,'JPN'),
 ('JPN_imortais','Kinshi Kunsho','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5,'JPN');

-- ===== nomes de formação nacionais (formation_name.country_tag) =====
INSERT INTO formation_name (id,name,domain,sort,country_tag) VALUES
 ('JPN_ar_1','Hikotai 302','ar',1,'JPN'),
 ('JPN_ar_2','Hikotai 204','ar',2,'JPN'),
 ('JPN_ar_3','Hikotai 305','ar',3,'JPN'),
 ('JPN_mar_1','Frota de Escolta 1','mar',1,'JPN'),
 ('JPN_mar_2','Frota de Escolta 2','mar',2,'JPN'),
 ('JPN_mar_3','Flotilha de Yokosuka','mar',3,'JPN');
