-- USA: superpotência expedicionária. Ordem de batalha aprox. 2024-2026 (Exército + Fuzileiros ativos).
-- Carácter: logística/mobilidade e poder de fogo tecnológico excelentes, exército pequeno face à
-- população (voluntário, não conscrito) — força projetada, não massa.

-- ===== Unidades próprias =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (140,'Aerotransportados (Airborne)','ground',1.3,35,1.0,55),
 (141,'Stryker (Blindados Ligeiros)','ground',2.2,40,1.3,55),
 (142,'Abrams M1A2 (Blindados Pesados)','ground',5.5,70,2.4,42);

INSERT INTO unit_stat VALUES
 (140,'soft_atk',7),(140,'hard_atk',1),(140,'defense',18),(140,'breakthrough',11),(140,'armor',0),(140,'piercing',6),(140,'hardness',0.1),(140,'hp',22),
 (141,'soft_atk',9),(141,'hard_atk',5),(141,'defense',20),(141,'breakthrough',20),(141,'armor',20),(141,'piercing',22),(141,'hardness',0.55),(141,'hp',26),
 (142,'soft_atk',14),(142,'hard_atk',18),(142,'defense',14),(142,'breakthrough',32),(142,'armor',78),(142,'piercing',70),(142,'hardness',0.92),(142,'hp',24);

INSERT INTO unit_tag VALUES
 (140,'infantry'),(140,'ground'),(140,'airborne'),
 (141,'infantry'),(141,'armored'),(141,'ground'),
 (142,'armored'),(142,'ground');

-- ===== Espíritos nacionais =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('USA_forca_expedicionaria','USA','Força Expedicionária','Décadas de guerra fora de portas dão eficácia extra a qualquer ataque, seja qual for o terreno.'),
 ('USA_exercito_voluntario','USA','Exército de Voluntários','Sem serviço militar obrigatório, a base de reservistas é pequena — a infantaria de reforço rende menos na defesa.'),
 ('USA_apoio_aereo_naval','USA','Superioridade Aérea e Naval','Apoio aéreo próximo constante multiplica o efeito das colunas blindadas em ataque.'),
 ('USA_arsenal_democracia','USA','Arsenal da Democracia','A maior base industrial do mundo mantém a artilharia e o apoio logístico sempre bem municiados.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (150,'spirit',NULL,NULL,'str_attacker',NULL,'mul',1.10,'USA','USA_forca_expedicionaria'),
 (151,'spirit',NULL,NULL,'str_defender','infantry','mul',0.95,'USA','USA_exercito_voluntario'),
 (152,'spirit',NULL,NULL,'str_attacker','armored','mul',1.15,'USA','USA_apoio_aereo_naval'),
 (153,'spirit',NULL,NULL,'str','support','add',0.10,'USA','USA_arsenal_democracia');

-- ===== country_stat / country_info =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('USA','production_speed',1.2),
 ('USA','org_regain',1.1),
 ('USA','start_army_mult',0.6);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('USA','República federal presidencialista','Presidente dos Estados Unidos',
  'Projeção de força expedicionária apoiada em superioridade aérea, naval e logística global.',
  'NATO',
  'A maior economia e o maior orçamento de defesa do mundo, com bases e frotas em todos os oceanos. O exército é profissional e tecnologicamente avançado, mas relativamente pequeno face à população — a doutrina assenta em mobilidade, apoio aéreo e capacidade industrial para reequipar rapidamente em caso de guerra prolongada.');

-- ===== Templates próprios =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (101,'USA','Divisão Aerotransportada'),
 (102,'USA','Brigada Stryker'),
 (103,'USA','Divisão Blindada Pesada');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (101,140,6),(101,4,2),
 (102,141,6),(102,4,2),(102,6,1),
 (103,142,4),(103,2,3),(103,4,2);

-- ===== Brigadas/divisões reais (exército inicial) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (104,'USA','1ª Divisão de Infantaria (Big Red One)','Infantaria','Kansas'),
 (105,'USA','1ª Divisão Blindada','Divisão Blindada Pesada','Texas'),
 (106,'USA','1ª Divisão de Cavalaria','Blindada','Texas'),
 (107,'USA','3ª Divisão de Infantaria (Mecanizada)','Mecanizada','Georgia'),
 (108,'USA','4ª Divisão de Infantaria','Blindada','Colorado'),
 (109,'USA','10ª Divisão de Montanha','Infantaria','New York'),
 (110,'USA','82ª Divisão Aerotransportada','Divisão Aerotransportada','North Carolina'),
 (111,'USA','101ª Divisão Aerotransportada (Assalto Aéreo)','Divisão Aerotransportada','Kentucky'),
 (112,'USA','11ª Divisão Aerotransportada (Ártico)','Divisão Aerotransportada','Alaska'),
 (113,'USA','25ª Divisão de Infantaria','Infantaria','Hawaii'),
 (114,'USA','2ª Brigada de Combate Stryker','Brigada Stryker','Washington'),
 (115,'USA','7ª Divisão de Infantaria','Infantaria AT','Washington'),
 (116,'USA','1ª Divisão de Fuzileiros Navais','Infantaria','California'),
 (117,'USA','2ª Divisão de Fuzileiros Navais','Infantaria AT','North Carolina'),
 (118,'USA','29ª Divisão de Infantaria (Guarda Nacional)','Infantaria','Virginia');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('usa_reindustrializacao','USA','Reindustrialização Estratégica','Reshoring de semicondutores e minerais críticos: a Lei CHIPS reforçada reduz a dependência de cadeias estrangeiras.',42,NULL,1),
 ('usa_estaleiros_navais','USA','Renascimento dos Estaleiros Navais','Investimento maciço em Newport News e Bath Iron Works para recuperar o ritmo de construção da Marinha.',49,'usa_reindustrializacao',2),
 ('usa_defesa_avancada','USA','DARPA e Tecnologias de Ruptura','Financiamento acelerado a hipersónicos, inteligência artificial militar e sistemas autónomos.',56,'usa_estaleiros_navais',3),
 ('usa_guarda_nacional','USA','Mobilização da Guarda Nacional','Reforço do recrutamento estadual e incentivos à Guarda Nacional para engrossar as fileiras sem alistamento obrigatório.',35,NULL,4),
 ('usa_doutrina_expedicionaria','USA','Doutrina Expedicionária Conjunta','Exercícios conjuntos e projeção rápida de força inspirados no modelo de resposta global do Pentágono.',42,'usa_guarda_nacional',5),
 ('usa_pivo_pacifico','USA','Pivô para o Indo-Pacífico','Redireccionamento estratégico de recursos e investigação para conter a expansão chinesa no Pacífico.',28,NULL,6),
 ('usa_forcas_especiais','USA','Comando de Operações Especiais Reforçado','O SOCOM ganha mais unidades e autonomia operacional, replicando o sucesso da era pós-11 de Setembro.',35,'usa_doutrina_expedicionaria',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('usa_reindustrializacao','industry',1.08),
 ('usa_estaleiros_navais','production_speed',1.10),
 ('usa_defesa_avancada','research_speed',1.10),
 ('usa_defesa_avancada','industry',1.05),
 ('usa_guarda_nacional','conscription',1.15),
 ('usa_doutrina_expedicionaria','org_regain',1.10),
 ('usa_pivo_pacifico','research_speed',1.08),
 ('usa_forcas_especiais','org_regain',1.08),
 ('usa_forcas_especiais','conscription',1.05);
