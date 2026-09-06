-- EGY (k=20) — Egipto: um dos maiores exércitos do mundo em efectivo (largamente conscrito),
-- ordem de batalha aproximada 2024-2026. Doutrina moldada pelas guerras árabe-israelitas: defesa
-- em profundidade do deserto ocidental e do Sinai, dois Exércitos de Campo junto ao Canal do
-- Suez, blindados Abrams de produção licenciada em série no Egipto, e um comando de forças
-- especiais (Saiqa) de referência regional.

-- ===== unit_type próprios (100+20*20 .. 119+20*20 = 500..519) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (500,'M1A1 Abrams (produção licenciada)','ground',4.3,60,2.1,39),
 (501,'Infantaria Mecanizada do Deserto','ground',1.9,42,1.3,34),
 (502,'Comandos do Deserto (Saiqa)','ground',1.1,28,0.7,30);

INSERT INTO unit_stat VALUES
 (500,'soft_atk',13),(500,'hard_atk',15),(500,'defense',13),(500,'breakthrough',29),(500,'armor',65),(500,'piercing',58),(500,'hardness',0.90),(500,'hp',21),
 (501,'soft_atk',9), (501,'hard_atk',3), (501,'defense',24),(501,'breakthrough',13),(501,'armor',10),(501,'piercing',15),(501,'hardness',0.35),(501,'hp',26),
 (502,'soft_atk',8), (502,'hard_atk',1), (502,'defense',16),(502,'breakthrough',10),(502,'armor',0), (502,'piercing',6), (502,'hardness',0.10),(502,'hp',18);

INSERT INTO unit_tag VALUES
 (500,'armored'),(500,'ground'),
 (501,'infantry'),(501,'ground'),
 (502,'infantry'),(502,'ground'),(502,'especial');

-- ===== espíritos nacionais + modificadores (ids 500..519) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('EGY_conscricao_de_massa','EGY','Conscrição em Massa',
   'Um exército enorme sustentado por conscrição quase universal é difícil de coordenar com eficiência a partir do comando central.'),
 ('EGY_guerra_do_deserto','EGY','Veteranos da Guerra do Deserto',
   'A experiência acumulada em várias guerras no Sinai e no deserto ocidental dá às tropas egípcias uma vantagem clara em terreno desértico, tanto a atacar como a defender.'),
 ('EGY_abrams_licenciado','EGY','Abrams de Produção Licenciada',
   'A produção local sob licença do M1A1 mantém os blindados egípcios modernos, ainda que sem as melhorias mais recentes dos modelos de exportação.'),
 ('EGY_comandos_saiqa','EGY','Comandos Saiqa',
   'O comando de forças especiais Saiqa ("Raio") é treinado para operações rápidas de infiltração no deserto e nas zonas urbanas do Delta.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (500,'spirit',NULL,           NULL,     'command',     NULL,      'mul',0.90,'EGY','EGY_conscricao_de_massa'),
 (501,'spirit','terrain','desert','str_defender', NULL,     'mul',1.15,'EGY','EGY_guerra_do_deserto'),
 (502,'spirit','terrain','desert','str_attacker', NULL,     'mul',1.10,'EGY','EGY_guerra_do_deserto'),
 (503,'spirit',NULL,           NULL,     'str_attacker','armored', 'mul',1.05,'EGY','EGY_abrams_licenciado'),
 (504,'spirit',NULL,           NULL,     'str_attacker','armored', 'add',0.05,'EGY','EGY_abrams_licenciado'),
 (505,'spirit',NULL,           NULL,     'str_attacker','especial','mul',1.15,'EGY','EGY_comandos_saiqa');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('EGY','production_speed',0.85),
 ('EGY','org_regain',0.90),
 ('EGY','start_army_mult',1.7);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('EGY','República presidencialista (com forte peso militar)','Presidente e Comandante Supremo das Forças Armadas',
  'Doutrina de defesa em profundidade herdada das guerras árabe-israelitas: dois Exércitos de Campo a guardar o Canal do Suez e o Sinai, blindados de produção nacional licenciada e um comando de forças especiais para operações rápidas no deserto.',
  'Não-alinhado (cooperação militar dual com os EUA e a Rússia)',
  'O Egipto mantém um dos maiores exércitos do mundo em efectivo, sustentado por conscrição quase universal e desdobrado sobretudo ao longo do Canal do Suez, do Sinai e do deserto ocidental. Décadas de guerras árabe-israelitas deixaram uma sólida doutrina de defesa em profundidade, blindados Abrams produzidos sob licença no país e um comando de forças especiais (Saiqa) de referência na região, ainda que a dimensão do exército pese sobre a eficiência de comando e a modernização do equipamento.');

-- ===== templates próprios (ids 1+50*20..50+50*20 = 1001..1050) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1001,'EGY','Divisão Mecanizada do Deserto'),
 (1002,'EGY','Divisão Blindada Abrams'),
 (1003,'EGY','Grupo de Comandos Saiqa');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1001,501,5),(1001,1,2),(1001,4,2),
 (1002,500,4),(1002,2,2),(1002,4,1),
 (1003,502,4),(1003,1,2);

-- ===== brigadas/divisões reais nomeadas (ids 1004..1050) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1004,'EGY','2º Exército de Campo','Divisão Mecanizada do Deserto','Al Isma`iliyah'),
 (1005,'EGY','3º Exército de Campo','Divisão Mecanizada do Deserto','As Suways'),
 (1006,'EGY','1ª Divisão Blindada','Divisão Blindada Abrams','Al Qahirah'),
 (1007,'EGY','4ª Divisão Blindada','Divisão Blindada Abrams','Al Jizah'),
 (1008,'EGY','3ª Divisão Mecanizada','Divisão Mecanizada do Deserto','Ad Daqahliyah'),
 (1009,'EGY','7ª Divisão de Infantaria (Sinai)','Infantaria',"Shamal Sina'"),
 (1010,'EGY','9ª Divisão Blindada','Divisão Blindada Abrams','Al Buhayrah'),
 (1011,'EGY','Comando de Comandos Saiqa (Inshas)','Grupo de Comandos Saiqa','Ash Sharqiyah'),
 (1012,'EGY','Comando Militar Sul','Infantaria','Aswan'),
 (1013,'EGY','Comando Militar do Mar Vermelho','Infantaria','Al Bahr al Ahmar'),
 (1014,'EGY','Guarda Republicana','Infantaria AT','Al Qahirah'),
 (1015,'EGY','2ª Divisão de Infantaria (Deserto Ocidental)','Infantaria','Al Wadi at Jadid');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('egy_soberania_canal','EGY','Soberania do Canal','O Canal do Suez continua a maior fonte de divisas do país: reforça-se a sua defesa e vigilância.',35,NULL,1),
 ('egy_seguranca_sinai','EGY','Operação Sinai','Continuação das operações contra a insurgência na Península do Sinai, com maior mobilidade no deserto.',42,'egy_soberania_canal',2),
 ('egy_comandos_saiqa','EGY','Escola de Comandos Saiqa','Expansão do centro de Inshas: mais coortes de forças especiais treinadas para o deserto e o litoral.',35,'egy_seguranca_sinai',3),
 ('egy_modernizacao_forcas','EGY','Modernização das Forças Armadas','Programa plurianual de reequipamento do Terceiro Exército, herdeiro da doutrina pós-1973.',49,NULL,4),
 ('egy_industria_defesa','EGY','Organização Árabe de Industrialização','Investimento na AOI: fábricas próprias de munições, blindados e sobressalentes reduzem a dependência externa.',56,'egy_modernizacao_forcas',5),
 ('egy_producao_licenciada','EGY','Linha M1A1 no Egipto','Ampliação da co-produção de blindados Abrams em Helwan, com maior nacionalização de componentes.',63,'egy_industria_defesa',6),
 ('egy_servico_nacional','EGY','Reforma do Serviço Militar','Actualização das quotas de conscrição para sustentar um dos maiores efectivos do mundo árabe.',28,NULL,7),
 ('egy_reserva_estrategica','EGY','Reserva Estratégica do Delta','Depósitos de mobilização espalhados pelo Delta do Nilo aceleram a reconstituição de unidades desgastadas.',42,'egy_servico_nacional',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('egy_soberania_canal','org_regain',1.06),
 ('egy_seguranca_sinai','org_regain',1.08),
 ('egy_comandos_saiqa','org_regain',1.10),
 ('egy_modernizacao_forcas','production_speed',1.08),
 ('egy_industria_defesa','industry',1.10),
 ('egy_industria_defesa','production_speed',1.06),
 ('egy_producao_licenciada','production_speed',1.09),
 ('egy_servico_nacional','conscription',1.20),
 ('egy_reserva_estrategica','conscription',1.10);
