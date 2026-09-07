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
