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

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('TUR_adv_estreitos','TUR','seguranca','Guardião dos Estreitos','⚓',180,'Quem passa, passa com licença dele.'),
 ('TUR_adv_aparelhos','TUR','ciencia','Mestre dos Aparelhos Não Tripulados','🛩',190,'Oficinas pequenas com ideias grandes.');
INSERT INTO advisor_effect VALUES ('TUR_adv_estreitos','port_capacity',1.2);
INSERT INTO advisor_effect VALUES ('TUR_adv_estreitos','defense',1.06);
INSERT INTO advisor_effect VALUES ('TUR_adv_aparelhos','research_speed',1.16);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('TUR_estreitos','Convenção dos Estreitos','☾',10,'TUR');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('TUR_law_livre','TUR_estreitos','Passagem livre','Mercante que pague passa, seja de quem for.',0,1,'TUR'),
 ('TUR_law_beligerantes','TUR_estreitos','Fecho aos beligerantes','Navio de guerra em guerra não entra no Bósforo.',1,0,'TUR'),
 ('TUR_law_drones','TUR_estreitos','Indústria de drones','O país exporta o que fabrica e fabrica o que usa.',2,0,'TUR');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('TUR_law_livre','export_share',1.1),
 ('TUR_law_beligerantes','defense',1.08),
 ('TUR_law_beligerantes','export_price',1.1),
 ('TUR_law_drones','production_speed',1.12),
 ('TUR_law_drones','research_speed',1.06);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('TUR_gen_estreitos_tur','Guarda dos Estreitos','defense',1.16,130,'TUR','☾','Dois mares e uma cidade no meio: a passagem fecha-se com ele lá.'),
 ('TUR_gen_drone','Mestre dos Drones','attack',1.16,140,'TUR','🛩','Vê o campo de batalha inteiro antes de mandar um homem lá.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('TUR_escola','Escola dos Estreitos','☾',10,'TUR');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('TUR_doc_estreitos','TUR_escola','Guarda dos Estreitos','Dois mares e uma cidade no meio: a passagem fecha-se por terra.',50,NULL,1,'TUR'),
 ('TUR_doc_drone','TUR_escola','Guerra de Drones','Vê-se o campo de batalha inteiro antes de mandar um homem lá.',110,'TUR_doc_estreitos',2,'TUR'),
 ('TUR_doc_mehmetcik','TUR_escola','Exército de Conscritos','Um exército grande de gente que serve por dever, não por soldo.',190,'TUR_doc_drone',3,'TUR');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('TUR_doc_estreitos','defense',1.08),
 ('TUR_doc_drone','attack',1.07),
 ('TUR_doc_drone','production_speed',1.05),
 ('TUR_doc_mehmetcik','conscription',1.09),
 ('TUR_doc_mehmetcik','org_regain',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('TUR_ar','Asas do Bósforo','🌙',11,'TUR','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('TUR_ar_bayraktar','TUR_ar','Escola do Drone','O aparelho barato que muda guerras inteiras e não leva ninguém dentro.',50,NULL,1,'TUR'),
 ('TUR_ar_sead','TUR_ar','Caça à Antiaérea','Primeiro cega-se o radar; só depois é que entram os tripulados.',110,'TUR_ar_bayraktar',2,'TUR'),
 ('TUR_ar_interdicao_tur','TUR_ar','Interdição de Coluna','A coluna que se vê de cima nunca chega ao sítio.',185,'TUR_ar_sead',3,'TUR');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('TUR_ar_bayraktar','air_upkeep',0.88),
 ('TUR_ar_sead','air_losses',0.91),
 ('TUR_ar_interdicao_tur','air_bombing',1.11);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('TUR_mar','Pátria Azul','🐋',12,'TUR','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('TUR_mar_estreitos_tur','TUR_mar','Guarda dos Estreitos','Bósforo e Dardanelos: duas portas de um mar inteiro, ambas nossas.',50,NULL,1,'TUR'),
 ('TUR_mar_milgem','TUR_mar','Corvetas Nacionais','Desenhadas e construídas em casa: sem licença de ninguém para navegar.',110,'TUR_mar_estreitos_tur',2,'TUR'),
 ('TUR_mar_egeu','TUR_mar','Patrulha do Egeu','Mil ilhas à porta obrigam a ter sempre alguém no mar.',185,'TUR_mar_milgem',3,'TUR');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('TUR_mar_estreitos_tur','naval_blockade',1.12),
 ('TUR_mar_milgem','naval_upkeep',0.9),
 ('TUR_mar_egeu','naval_patrol',1.09);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('TUR_ar_gen_anatolia','Chefe da Asa da Anatólia','air_losses',0.88,145,'TUR','🦅','Um céu entre três mares e ele conhece o vento dos três.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('TUR_mar_gen_estreitos_tur','Almirante dos Estreitos','naval_blockade',1.16,145,'TUR','⚓','Tem a chave de duas portas de água e sabe quando as fechar.','mar',45);

-- ===== programas nacionais de aviação e de marinha (tech.country_tag) =====
INSERT INTO tech (id,branch,name,cost,requires,description,country_tag) VALUES
 ('TUR_tech_ar_drones_combate','Aviação','Drones de Combate',260,'air_1','Drones armados de fabrico próprio, baratos e aos milhares: mudaram a guerra e são daqui.','TUR'),
 ('TUR_tech_mar_bosforo','Marinha','Guarda do Bósforo',260,'nav_1','Quem tem os estreitos decide quem entra e quem sai do Mar Negro.','TUR');
INSERT INTO tech_effect (tech_id,stat_key,value) VALUES
 ('TUR_tech_ar_drones_combate','air_bombing',1.18),
 ('TUR_tech_mar_bosforo','naval_blockade',1.18);

-- ===== escada de postos nacional (general_rank.country_tag) =====
INSERT INTO general_rank (domain,level,name,xp,bonus,country_tag) VALUES
 ('exercito',1,'Tuğgeneral',0,0,'TUR'),
 ('exercito',2,'Tümgeneral',40,0.5,'TUR'),
 ('exercito',3,'Korgeneral',110,1,'TUR'),
 ('exercito',4,'Orgeneral',220,1.75,'TUR'),
 ('exercito',5,'Mareşal',360,2.5,'TUR'),
 ('ar',1,'Hava Tuğgeneral',0,0,'TUR'),
 ('ar',2,'Hava Tümgeneral',40,0.5,'TUR'),
 ('ar',3,'Hava Korgeneral',110,1,'TUR'),
 ('ar',4,'Hava Orgeneral',220,1.75,'TUR'),
 ('ar',5,'Hava Kuvvetleri Komutanı',360,2.5,'TUR'),
 ('mar',1,'Tuğamiral',0,0,'TUR'),
 ('mar',2,'Tümamiral',40,0.5,'TUR'),
 ('mar',3,'Koramiral',110,1,'TUR'),
 ('mar',4,'Oramiral',220,1.75,'TUR'),
 ('mar',5,'Deniz Kuvvetleri Komutanı',360,2.5,'TUR');

-- ===== condecorações nacionais (medal.country_tag) =====
INSERT INTO medal (id,name,description,metric,threshold,bonus,sort,country_tag) VALUES
 ('TUR_baptismo','Muharebe Madalyası','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1,'TUR'),
 ('TUR_assalto','Üstün Cesaret ve Feragat Madalyası','Tomou três regiões ao inimigo.','captures',3,0.03,2,'TUR'),
 ('TUR_campanha','Takdirname','Quarenta pontos de experiência em combate.','xp',40,0.02,3,'TUR'),
 ('TUR_aco','Şeref Madalyası','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4,'TUR'),
 ('TUR_imortais','İstiklal Madalyası','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5,'TUR');

-- ===== nomes de formação nacionais (formation_name.country_tag) =====
INSERT INTO formation_name (id,name,domain,sort,country_tag) VALUES
 ('TUR_ar_1','141 Filo Kurt','ar',1,'TUR'),
 ('TUR_ar_2','161 Filo Yarasa','ar',2,'TUR'),
 ('TUR_ar_3','182 Filo Atmaca','ar',3,'TUR'),
 ('TUR_mar_1','Muhrip Filosu','mar',1,'TUR'),
 ('TUR_mar_2','Firkateyn Filosu','mar',2,'TUR'),
 ('TUR_mar_3','Denizaltı Filosu','mar',3,'TUR');
