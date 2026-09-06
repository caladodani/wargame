-- TUR (k=15) — Kara Kuvvetleri Komutanlığı (Forças Terrestres), ordem de batalha aproximada 2024-2026.
-- Fontes: brigadas blindadas com Altay/Leopard 2A4, 3ª Divisão Mecanizada, 20ª Brigada Blindada,
-- 65ª Brigada Mecanizada, 1ª e 2ª Brigadas de Comandos "Bordo Bereliler" (boinas granate), Brigadas
-- de Comandos de Montanha de Hakkari e Şırnak, brigadas de infantaria do sudeste (Van, Diyarbakır,
-- Mardin), 1ª Divisão de Fuzileiros Navais (Izmir).
-- Carácter: exército muito grande e numeroso, forte tradição de forças de comandos e de infantaria
-- de montanha (guerra assimétrica no sudeste), indústria de drones (Bayraktar) de referência mundial.

-- ===== unit_type próprios (100+20*15 .. 119+20*15 = 400..419) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (400,'Altay','ground',7.6,82,2.1,40),
 (401,'Leopard 2A4','ground',5.8,70,2.0,36),
 (402,'Comando (Bordo Bereliler)','ground',1.7,45,0.9,34),
 (403,'Infantaria de Montanha','ground',1.3,36,0.9,22);

INSERT INTO unit_stat VALUES
 (400,'soft_atk',14),(400,'hard_atk',19),(400,'defense',15),(400,'breakthrough',31),(400,'armor',82),(400,'piercing',70),(400,'hardness',0.94),(400,'hp',24),
 (401,'soft_atk',12),(401,'hard_atk',16),(401,'defense',13),(401,'breakthrough',28),(401,'armor',65),(401,'piercing',58),(401,'hardness',0.90),(401,'hp',21),
 (402,'soft_atk',9), (402,'hard_atk',1.5), (402,'defense',25),(402,'breakthrough',12),(402,'armor',0), (402,'piercing',6), (402,'hardness',0.1), (402,'hp',24),
 (403,'soft_atk',7), (403,'hard_atk',1), (403,'defense',26),(403,'breakthrough',9), (403,'armor',0), (403,'piercing',5), (403,'hardness',0.1), (403,'hp',26);

INSERT INTO unit_tag VALUES
 (400,'armored'),(400,'ground'),
 (401,'armored'),(401,'ground'),
 (402,'infantry'),(402,'ground'),(402,'elite'),
 (403,'infantry'),(403,'ground');

-- ===== espíritos nacionais + modificadores (ids 400..419) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('TUR_exercito_grande','TUR','Segundo Maior Exército da NATO',
   'A dimensão e a tradição de conscrição da Kara Kuvvetleri dão pequena vantagem ofensiva geral, fruto do volume e da rotação constante de efetivos.'),
 ('TUR_bordo_bereliler','TUR','Bordo Bereliler',
   'Os comandos de boina granate treinam para operações de alto risco: forte bónus ofensivo às unidades de elite.'),
 ('TUR_guerra_de_montanha','TUR','Guerra de Montanha no Sudeste',
   'Décadas de operações em terreno montanhoso no sudeste da Anatólia deram vantagem clara a atacar e a defender em relevo acidentado.'),
 ('TUR_industria_de_drones','TUR','Indústria de Drones',
   'A produção nacional de Bayraktar e Anka faz da Turquia referência mundial em drones: as unidades de apoio contribuem mais para o ataque.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (400,'spirit',NULL,NULL,           'str_attacker',NULL,     'mul',1.05,'TUR','TUR_exercito_grande'),
 (401,'spirit',NULL,NULL,           'str_attacker','elite',  'mul',1.20,'TUR','TUR_bordo_bereliler'),
 (402,'spirit','terrain','mountain','str_defender',NULL,     'mul',1.15,'TUR','TUR_guerra_de_montanha'),
 (403,'spirit','terrain','mountain','str_attacker',NULL,     'mul',1.10,'TUR','TUR_guerra_de_montanha'),
 (404,'spirit',NULL,NULL,           'str_attacker','support', 'mul',1.12,'TUR','TUR_industria_de_drones');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('TUR','production_speed',1.15),
 ('TUR','org_regain',1.0),
 ('TUR','start_army_mult',1.20);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('TUR','República presidencialista','Comandante-em-Chefe das Forças Armadas',
  'Massa blindada nacional (Altay/Leopard), comandos de elite para operações de alto risco, infantaria de montanha treinada em guerra assimétrica e apoio aéreo não tripulado de produção própria.',
  'NATO',
  'A Turquia mantém um dos maiores exércitos de terra da NATO, apoiado numa indústria de defesa cada vez mais autónoma, símbolo maior o carro de combate nacional Altay. Décadas de operações contra a insurgência no sudeste montanhoso forjaram uma tradição sólida de comandos e infantaria de montanha. A produção nacional de drones de combate, referência mundial, dá às forças terrestres um apoio de reconhecimento e ataque de precisão sem paralelo na região.');

-- ===== templates próprios (ids 751..800) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (751,'TUR','Tümen Zırhlı'),
 (752,'TUR','Bordo Bereliler Tugayı'),
 (753,'TUR','Dağ Komando Tugayı');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (751,400,3),(751,401,3),(751,2,2),
 (752,402,4),(752,1,3),(752,4,1),
 (753,403,4),(753,1,2),(753,6,1),(753,4,1);

-- ===== brigadas/divisões reais nomeadas (ids 754..800) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (754,'TUR','1ª Brigada Blindada (Altay)','Tümen Zırhlı','Ankara'),
 (755,'TUR','2ª Brigada Blindada (Leopard 2A4)','Tümen Zırhlı','Kayseri'),
 (756,'TUR','3ª Divisão Mecanizada','Mecanizada','Bursa'),
 (757,'TUR','20ª Brigada Blindada','Tümen Zırhlı','Sakarya'),
 (758,'TUR','65ª Brigada de Infantaria Mecanizada','Mecanizada','Erzurum'),
 (759,'TUR','1ª Brigada de Comandos "Bordo Bereliler"','Bordo Bereliler Tugayı','Kayseri'),
 (760,'TUR','2ª Brigada de Comandos','Bordo Bereliler Tugayı','Bolu'),
 (761,'TUR','Brigada de Comandos de Montanha de Hakkari','Dağ Komando Tugayı','Hakkari'),
 (762,'TUR','Brigada de Comandos de Montanha de Şırnak','Dağ Komando Tugayı','Sirnak'),
 (763,'TUR','Brigada de Infantaria de Van','Infantaria','Van'),
 (764,'TUR','Brigada de Infantaria de Diyarbakır','Infantaria','Diyarbakir'),
 (765,'TUR','Brigada de Infantaria de Mardin','Infantaria AT','Mardin'),
 (766,'TUR','1ª Divisão de Fuzileiros Navais','Infantaria','Izmir');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='TUR')
   AND name IN ('Hakkari','Van','Bitlis','Mus','Agri','Erzurum','Tunceli','Bingöl','Sirnak','Bolu');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('tur_patria_azul','TUR','Pátria Azul (Mavi Vatan)','Doutrina naval de afirmação no Egeu e no Mediterrâneo Oriental.',42,NULL,1),
 ('tur_industria_drones','TUR','Indústria Nacional de Drones','Baykar e TAI aceleram a produção de UAVs e munições de precisão.',35,NULL,2),
 ('tur_altay','TUR','Programa de Carro de Combate Altay','Série nacional do Altay substitui progressivamente a frota de blindados importados.',49,'tur_industria_drones',3),
 ('tur_bosforo','TUR','Guarda dos Estreitos','Reforço da vigilância e defesa costeira do Bósforo e dos Dardanelos.',28,'tur_patria_azul',4),
 ('tur_reserva_anatolia','TUR','Mobilização da Anatólia','Reorganização do serviço militar obrigatório e das reservas territoriais.',35,NULL,5),
 ('tur_forcas_especiais','TUR','Bordô: Elite das Forças Especiais','Expansão das brigadas de comandos e das forças de montanha no Sudeste.',42,'tur_reserva_anatolia',6),
 ('tur_exportacao_defesa','TUR','Diplomacia da Indústria de Defesa','Exportação de sistemas turcos reforça alianças e financia a produção interna.',56,'tur_altay',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('tur_patria_azul','research_speed',1.05),
 ('tur_industria_drones','production_speed',1.10),
 ('tur_altay','industry',1.08),
 ('tur_bosforo','org_regain',1.06),
 ('tur_reserva_anatolia','conscription',1.20),
 ('tur_forcas_especiais','org_regain',1.08),
 ('tur_exportacao_defesa','industry',1.10),
 ('tur_exportacao_defesa','production_speed',1.05);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('tur_exportacao_defesa','tur_patria_azul');
