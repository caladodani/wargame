-- POL (k=13) — Wojsko Polskie, ordem de batalha aproximada 2024-2026.
-- Fontes: Warszawska Dywizja Pancerna (Wesoła), 11. Dywizja Kawalerii Pancernej (Żagań, Abrams),
-- 16. Pomorska e 12. Dywizja Zmechanizowana, 18. Dywizja Zmechanizowana (K2/K9, Siedlce),
-- 6. Brygada Powietrznodesantowa, 25. Brygada Kawalerii Powietrznej, 21. Brygada Strzelców
-- Podhalańskich, 17. Wielkopolska e 15. Brygada Zmechanizowana, 9. Brygada Kawalerii Pancernej,
-- Wojska Obrony Terytorialnej (WOT).
-- Carácter: exército em expansão acelerada (compras massivas Abrams/K2/K9 sul-coreano-americanas),
-- doutrina de defesa territorial na flanco leste da NATO.

-- ===== unit_type próprios (100+20*13 .. 119+20*13 = 360..379) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (360,'K2 Black Panther','ground',7.5,80,2.1,42),
 (361,'Abrams M1A2','ground',7.2,78,2.2,35),
 (362,'Rosomak','ground',2.6,45,1.5,50),
 (363,'K9 Thunder','support',2.4,45,1.6,25);

INSERT INTO unit_stat VALUES
 (360,'soft_atk',14),(360,'hard_atk',19),(360,'defense',15),(360,'breakthrough',32),(360,'armor',80),(360,'piercing',68),(360,'hardness',0.94),(360,'hp',24),
 (361,'soft_atk',13),(361,'hard_atk',18),(361,'defense',14),(361,'breakthrough',30),(361,'armor',78),(361,'piercing',62),(361,'hardness',0.93),(361,'hp',22),
 (362,'soft_atk',10),(362,'hard_atk',4), (362,'defense',26),(362,'breakthrough',17),(362,'armor',16),(362,'piercing',20),(362,'hardness',0.5), (362,'hp',30),
 (363,'soft_atk',26),(363,'hard_atk',3), (363,'defense',7), (363,'breakthrough',7), (363,'armor',5), (363,'piercing',12),(363,'hardness',0.25),(363,'hp',7);

INSERT INTO unit_tag VALUES
 (360,'armored'),(360,'ground'),
 (361,'armored'),(361,'ground'),
 (362,'infantry'),(362,'armored'),(362,'ground'),
 (363,'support'),(363,'ground');

-- ===== espíritos nacionais + modificadores (ids 360..379) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('POL_rearmamento_acelerado','POL','Rearmamento Acelerado',
   'Compras massivas de Abrams, K2 e K9 e uma indústria em expansão constante elevam ligeiramente a força de combate de toda a força modernizada.'),
 ('POL_obrona_terytorialna','POL','Obrona Terytorialna',
   'As Wojska Obrony Terytorialnej treinam para a defesa do território nacional palmo a palmo: maior resiliência defensiva em solo próprio.'),
 ('POL_flanco_leste','POL','Sentinela da Flanco Leste',
   'Anos de exercícios NATO na fronteira oriental deram ao comando polaco rotinas de coordenação aliada muito treinadas.'),
 ('POL_tradicao_podhalanska','POL','Tradição Podhalańska',
   'Os Strzelcy Podhalańscy mantêm viva a tradição de combate de montanha nos Cárpatos, dando vantagem tanto a atacar como a defender em relevo acidentado.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (360,'spirit',NULL,NULL,           'str',         NULL,'mul',1.05,'POL','POL_rearmamento_acelerado'),
 (361,'spirit',NULL,NULL,           'str_defender',NULL,'mul',1.15,'POL','POL_obrona_terytorialna'),
 (362,'spirit',NULL,NULL,           'command',     NULL,'mul',1.10,'POL','POL_flanco_leste'),
 (363,'spirit','terrain','mountain','str_defender',NULL,'mul',1.15,'POL','POL_tradicao_podhalanska'),
 (364,'spirit','terrain','mountain','str_attacker',NULL,'mul',1.10,'POL','POL_tradicao_podhalanska');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('POL','production_speed',1.35),
 ('POL','org_regain',1.05),
 ('POL','start_army_mult',1.30);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('POL','República parlamentar','Chefe do Estado-Maior-General das Forças Armadas',
  'Reconstrução acelerada de uma força blindada pesada (Abrams/K2/K9), defesa territorial em profundidade da flanco leste, plena integração de comando com a OTAN.',
  'NATO',
  'A Polónia atravessa a maior expansão militar da sua história recente, com encomendas maciças de blindados sul-coreanos e norte-americanos a reforçar divisões que já contavam com tradição de cavalaria blindada. A doutrina assenta na defesa em profundidade da fronteira oriental, apoiada por uma Obrona Terytorialna cada vez mais robusta. A indústria de defesa nacional cresce em paralelo com a produção sob licença de sistemas estrangeiros.');

-- ===== templates próprios (ids 651..700) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (651,'POL','Dywizja Pancerna'),
 (652,'POL','Brygada Zmechanizowana K9'),
 (653,'POL','Brygada Górska Podhalańska');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (651,360,3),(651,361,3),(651,362,2),
 (652,362,4),(652,1,2),(652,363,2),
 (653,1,4),(653,6,2),(653,4,1);

-- ===== brigadas/divisões reais nomeadas (ids 654..700) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (654,'POL','Warszawska Dywizja Pancerna im. Tadeusza Kościuszki','Dywizja Pancerna','Masovian'),
 (655,'POL','11. Dywizja Kawalerii Pancernej','Dywizja Pancerna','Lubusz'),
 (656,'POL','16. Pomorska Dywizja Zmechanizowana','Brygada Zmechanizowana K9','Warmian-Masurian'),
 (657,'POL','12. Dywizja Zmechanizowana','Brygada Zmechanizowana K9','West Pomeranian'),
 (658,'POL','18. Dywizja Zmechanizowana','Mecanizada','Masovian'),
 (659,'POL','6. Brygada Powietrznodesantowa','Infantaria','Lesser Poland'),
 (660,'POL','25. Brygada Kawalerii Powietrznej','Infantaria','Łódź'),
 (661,'POL','21. Brygada Strzelców Podhalańskich','Brygada Górska Podhalańska','Subcarpathian'),
 (662,'POL','17. Wielkopolska Brygada Zmechanizowana','Mecanizada','Lubusz'),
 (663,'POL','15. Brygada Zmechanizowana','Mecanizada','Warmian-Masurian'),
 (664,'POL','9. Brygada Kawalerii Pancernej','Dywizja Pancerna','Warmian-Masurian'),
 (665,'POL','1. Brygada Obrony Terytorialnej Mazowiecka','Infantaria AT','Masovian');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('pol_wschodnia_tarcza','POL','Escudo do Flanco Oriental','A Polónia reforça a defesa em profundidade da fronteira com a Rússia e a Bielorrússia, da Brecha de Suwałki ao rio Bug.',35,NULL,1),
 ('pol_modernizacja_pancerna','POL','Modernização Blindada','Encomendas maciças de Abrams M1A2 e K2 Black Panther reconstroem o poder blindado polaco.',42,NULL,2),
 ('pol_obrona_terytorialna','POL','Wojska Obrony Terytorialnej','A Defesa Territorial expande-se em cada voivodia, ligando o exército regular à sociedade civil.',28,NULL,3),
 ('pol_nato_flanka','POL','Vanguarda da NATO','Varsóvia acolhe quartéis-generais multinacionais e exercícios permanentes da Aliança no flanco leste.',35,'pol_wschodnia_tarcza',4),
 ('pol_przemysl_zbrojeniowy','POL','Polska Grupa Zbrojeniowa','O grupo estatal PGZ recebe investimento maciço para produzir sob licença os sistemas importados.',49,'pol_modernizacja_pancerna',5),
 ('pol_licencja_k9','POL','Produção Nacional do Krab','A linha de montagem do obuseiro Krab, herdeiro do K9 sul-coreano, passa a produção em larga escala em solo polaco.',42,'pol_przemysl_zbrojeniowy',6),
 ('pol_rezerwa_terytorialna','POL','Reserva Estratégica Nacional','Novos incentivos ao recrutamento voluntário reforçam a reserva mobilizável para um conflito prolongado.',35,'pol_obrona_terytorialna',7);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('pol_wschodnia_tarcza','org_regain',1.08),
 ('pol_modernizacja_pancerna','production_speed',1.10),
 ('pol_obrona_terytorialna','conscription',1.15),
 ('pol_nato_flanka','org_regain',1.07),
 ('pol_nato_flanka','research_speed',1.05),
 ('pol_przemysl_zbrojeniowy','industry',1.10),
 ('pol_licencja_k9','industry',1.08),
 ('pol_licencja_k9','production_speed',1.08),
 ('pol_rezerwa_terytorialna','conscription',1.10);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('pol_rezerwa_terytorialna','pol_wschodnia_tarcza');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('POL_adv_vistula','POL','economia','Engenheiro do Vístula','🏭',175,'Reconstrói uma fábrica no tempo de a discutir.'),
 ('POL_adv_territorial','POL','seguranca','Chefe da Defesa Territorial','🎖',170,'Cada aldeia com a sua companhia.');
INSERT INTO advisor_effect VALUES ('POL_adv_vistula','industry',1.12);
INSERT INTO advisor_effect VALUES ('POL_adv_vistula','production_speed',1.05);
INSERT INTO advisor_effect VALUES ('POL_adv_territorial','conscription',1.15);
INSERT INTO advisor_effect VALUES ('POL_adv_territorial','defense',1.05);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('POL_flanco','Flanco Oriental','🦬',10,'POL');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('POL_law_aliada','POL_flanco','Presença aliada','Batalhões de fora rodam pelas bases do Leste.',0,1,'POL'),
 ('POL_law_muro','POL_flanco','Muro do Leste','Fossos, arame e sensores em toda a fronteira.',1,0,'POL'),
 ('POL_law_trezentos','POL_flanco','Exército de trezentos mil','O maior exército de terra da Europa, pago a crédito.',2,0,'POL');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('POL_law_aliada','defense',1.06),
 ('POL_law_muro','defense',1.12),
 ('POL_law_muro','industry',1.04),
 ('POL_law_trezentos','conscription',1.25),
 ('POL_law_trezentos','attack',1.06),
 ('POL_law_trezentos','industry',0.96);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('POL_gen_hussardo','Herdeiro dos Hussardos','attack',1.15,140,'POL','🦬','Cavalaria alada há quatrocentos anos, blindada agora.'),
 ('POL_gen_flanco_pol','Comandante do Flanco Oriental','defense',1.16,135,'POL','🛡','A fronteira do Leste é a dele, e ele prepara-a há uma década.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('POL_escola','Escola do Flanco Oriental','🦬',10,'POL');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('POL_doc_flanco','POL_escola','Guarda do Flanco','A fronteira do Leste é preparada há uma década, metro a metro.',50,NULL,1,'POL'),
 ('POL_doc_hussardo','POL_escola','Herança dos Hussardos','Cavalaria alada há quatrocentos anos, blindada agora.',110,'POL_doc_flanco',2,'POL'),
 ('POL_doc_mobilizacao','POL_escola','Mobilização Nacional','Um país que já foi apagado do mapa não discute o recrutamento.',190,'POL_doc_hussardo',3,'POL');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('POL_doc_flanco','defense',1.07),
 ('POL_doc_hussardo','attack',1.07),
 ('POL_doc_hussardo','move_speed',1.05),
 ('POL_doc_mobilizacao','conscription',1.1),
 ('POL_doc_mobilizacao','org_regain',1.06);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('POL_ar','Asas do Leste','🦅',11,'POL','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('POL_ar_dywizjon','POL_ar','Esquadrilha 303','A esquadrilha polaca foi a que mais abateu na Batalha de Inglaterra.',50,NULL,1,'POL'),
 ('POL_ar_dispersao_pol','POL_ar','Pistas de Recurso','Uma estrada larga é um aeródromo quando o aeródromo já não existe.',110,'POL_ar_dywizjon',2,'POL'),
 ('POL_ar_flanco_ar','POL_ar','Interdição do Flanco','Cortar a estrada por onde vem o segundo escalão é ganhar a semana.',185,'POL_ar_dispersao_pol',3,'POL');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('POL_ar_dywizjon','air_losses',0.9),
 ('POL_ar_dispersao_pol','air_upkeep',0.94),
 ('POL_ar_dispersao_pol','air_losses',0.95),
 ('POL_ar_flanco_ar','air_bombing',1.09);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('POL_mar','Guarda do Báltico','🧭',12,'POL','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('POL_mar_baltico','POL_mar','Patrulha do Báltico','Um mar fechado e estreito onde toda a gente se vê.',50,NULL,1,'POL'),
 ('POL_mar_orzel','POL_mar','Fuga do Orzeł','O submarino que fugiu sem cartas nem sextante e chegou a Inglaterra.',110,'POL_mar_baltico',2,'POL'),
 ('POL_mar_gdansk','POL_mar','Estaleiros de Gdańsk','Os cais que fizeram história também fazem navios.',185,'POL_mar_orzel',3,'POL');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('POL_mar_baltico','naval_patrol',1.09),
 ('POL_mar_orzel','naval_losses',0.92),
 ('POL_mar_gdansk','naval_upkeep',0.92);
