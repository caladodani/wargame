-- Índia: guerreiros de montanha e massa populacional, mas base industrial ainda em desenvolvimento
-- (produção lenta, dependência de equipamento importado/licenciado). Ordem de batalha aprox.
-- 2024-2026: Corpos do Exército indiano, com forte concentração de tropas de montanha na fronteira
-- com o Paquistão e a China (Ladakh, J&K, Nordeste).

-- ===== Unidades próprias =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (200,'Infantaria de Montanha','ground',1.1,34,0.9,20),
 (201,'T-90 Bhishma (Blindados)','ground',3.8,65,2.0,38),
 (202,'Infantaria Motorizada de Massa','ground',0.9,28,1.0,27);

INSERT INTO unit_stat VALUES
 (200,'soft_atk',6),(200,'hard_atk',1),(200,'defense',27),(200,'breakthrough',7),(200,'armor',0),(200,'piercing',5),(200,'hardness',0.1),(200,'hp',26),
 (201,'soft_atk',12),(201,'hard_atk',15),(201,'defense',12),(201,'breakthrough',27),(201,'armor',56),(201,'piercing',50),(201,'hardness',0.85),(201,'hp',20),
 (202,'soft_atk',6),(202,'hard_atk',1),(202,'defense',22),(202,'breakthrough',9),(202,'armor',2),(202,'piercing',6),(202,'hardness',0.15),(202,'hp',24);

INSERT INTO unit_tag VALUES
 (200,'infantry'),(200,'ground'),(200,'montanha'),
 (201,'armored'),(201,'ground'),
 (202,'infantry'),(202,'ground');

-- ===== Espíritos nacionais =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('IND_guerreiros_montanha','IND','Guerreiros de Montanha','Décadas de guerra de altitude no Himalaia dão vantagem extra em ataque e defesa em terreno de montanha.'),
 ('IND_populacao_imensa','IND','População Imensa','A maior população do mundo garante um fluxo constante de recrutas, compensando com número o que falta em sofisticação.'),
 ('IND_industria_em_desenvolvimento','IND','Base Industrial em Desenvolvimento','A dependência de equipamento importado ou licenciado torna os blindados mais lentos a modernizar e a coordenar em ataque.'),
 ('IND_autonomia_estrategica','IND','Autonomia Estratégica','A doutrina de não-alinhamento é essencialmente defensiva — reforça ligeiramente qualquer defesa.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (210,'spirit','terrain','mountain','str_attacker',NULL,'mul',1.15,'IND','IND_guerreiros_montanha'),
 (211,'spirit','terrain','mountain','str_defender',NULL,'mul',1.20,'IND','IND_guerreiros_montanha'),
 (212,'spirit',NULL,NULL,'str','infantry','add',0.05,'IND','IND_populacao_imensa'),
 (213,'spirit',NULL,NULL,'str_attacker','armored','mul',0.90,'IND','IND_industria_em_desenvolvimento'),
 (214,'spirit',NULL,NULL,'str_defender',NULL,'mul',1.05,'IND','IND_autonomia_estrategica');

-- ===== country_stat / country_info =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('IND','production_speed',0.75),
 ('IND','org_regain',1.0),
 ('IND','start_army_mult',1.4);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('IND','República federal parlamentar','Primeiro-Ministro da Índia',
  'Defesa de montanha e massa populacional: segurar as fronteiras do Himalaia e compensar a falta de sofisticação industrial com número.',
  'Não-alinhado',
  'Com o maior exército de voluntários do mundo e décadas de experiência em guerra de altitude contra o Paquistão e a China, a Índia é formidável na defesa das suas fronteiras montanhosas. A sua indústria de defesa está em rápido crescimento mas ainda depende de licenças e importações, o que torna a modernização dos blindados mais lenta do que a dos rivais regionais.');

-- ===== Templates próprios =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (251,'IND','Divisão de Montanha'),
 (252,'IND','Divisão Blindada Bhishma'),
 (253,'IND','Infantaria de Massa');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (251,200,6),(251,4,2),
 (252,201,4),(252,2,3),(252,4,2),
 (253,202,8),(253,4,2);

-- ===== Brigadas/divisões reais (exército inicial) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (254,'IND','14º Corpo (Corpo do Fogo)','Divisão de Montanha','Ladakh'),
 (255,'IND','15º Corpo (Corpo Chinar)','Divisão de Montanha','Jammu and Kashmir'),
 (256,'IND','16º Corpo','Blindada','Punjab'),
 (257,'IND','1º Corpo (Corpo Strike)','Divisão Blindada Bhishma','Haryana'),
 (258,'IND','2º Corpo (Corpo Strike)','Divisão Blindada Bhishma','Rajasthan'),
 (259,'IND','21º Corpo (Corpo Strike)','Blindada','Madhya Pradesh'),
 (260,'IND','3º Corpo (Corpo de Montanha)','Divisão de Montanha','Nagaland'),
 (261,'IND','4º Corpo (Corpo Gajraj)','Divisão de Montanha','Assam'),
 (262,'IND','33º Corpo','Infantaria de Massa','West Bengal'),
 (263,'IND','17º Corpo (Corpo de Montanha)','Divisão de Montanha','Uttarakhand'),
 (264,'IND','9º Corpo','Divisão de Montanha','Himachal Pradesh'),
 (265,'IND','10º Corpo','Mecanizada','Punjab'),
 (266,'IND','11º Corpo','Infantaria','Chandigarh'),
 (267,'IND','Comando do Sul (Divisão Blindada)','Blindada','Maharashtra'),
 (268,'IND','Divisão de Infantaria de Massa de Uttar Pradesh','Infantaria de Massa','Uttar Pradesh'),
 (269,'IND','Guarnição do Nordeste','Divisão de Montanha','Arunachal Pradesh');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('ind_agnipath','IND','Reforma Agnipath','Novo modelo de recrutamento de curto prazo alarga a base de reservistas treinados.',35,NULL,1),
 ('ind_make_in_india_defesa','IND','Make in India: Defesa','Política de substituição de importações abre linhas de produção nacionais de armamento.',42,NULL,2),
 ('ind_corpo_montanha_ataque','IND','Corpo de Ataque de Montanha','Novo corpo ofensivo de montanha reforça a fronteira do Himalaia face à China.',49,'ind_agnipath',3),
 ('ind_drdo_agni','IND','Programa Agni (DRDO)','A DRDO acelera o desenvolvimento dos mísseis balísticos Agni de longo alcance.',56,'ind_make_in_india_defesa',4),
 ('ind_triade_nuclear','IND','Tríade Nuclear Completa','Submarinos da classe Arihant fecham a tríade nuclear com segundo ataque garantido.',63,'ind_drdo_agni',5),
 ('ind_rafale_su30','IND','Esquadrilhas Rafale e Su-30MKI','Modernização da força aérea com caças multifunção franceses e russos.',49,'ind_make_in_india_defesa',6),
 ('ind_indo_pacifico','IND','Doutrina do Indo-Pacífico','Marinha projecta poder do Estreito de Malaca ao Golfo de Adem, em coordenação com o Quad.',56,'ind_corpo_montanha_ataque',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('ind_agnipath','conscription',1.20),
 ('ind_make_in_india_defesa','industry',1.10),
 ('ind_corpo_montanha_ataque','org_regain',1.10),
 ('ind_drdo_agni','research_speed',1.12),
 ('ind_triade_nuclear','research_speed',1.08),
 ('ind_rafale_su30','production_speed',1.15),
 ('ind_indo_pacifico','org_regain',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('ind_drdo_agni','ind_rafale_su30');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('ind_indo_pacifico','ind_make_in_india_defesa');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('IND_adv_aco','IND','economia','Ministro do Aço','🏭',185,'Altos-fornos acesos de norte a sul.'),
 ('IND_adv_provincias','IND','propaganda','Recrutador das Províncias','📣',175,'Traz homens de onde ninguém julgava haver.');
INSERT INTO advisor_effect VALUES ('IND_adv_aco','industry',1.14);
INSERT INTO advisor_effect VALUES ('IND_adv_provincias','conscription',1.22);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('IND_autossuficiencia','Índia Autossuficiente','🕉',10,'IND');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('IND_law_compras','IND_autossuficiencia','Compras no estrangeiro','O melhor material do mundo, pago a peso de ouro.',0,1,'IND'),
 ('IND_law_fabricar','IND_autossuficiencia','Fabricar na Índia','Quem vende tem de montar cá dentro e ensinar a fazer.',1,0,'IND'),
 ('IND_law_total','IND_autossuficiencia','Autossuficiência total','Da espingarda ao caça, tudo sai de fábrica nacional.',2,0,'IND');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('IND_law_compras','research_speed',1.08),
 ('IND_law_compras','export_share',1.1),
 ('IND_law_fabricar','industry',1.08),
 ('IND_law_fabricar','production_speed',1.05),
 ('IND_law_total','production_speed',1.12),
 ('IND_law_total','industry',1.08),
 ('IND_law_total','export_share',0.75);
