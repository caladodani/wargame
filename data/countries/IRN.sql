-- IRN (k=16) — Irão: dualidade Artesh (exército regular) / IRGC (Guarda Revolucionária, força
-- paralela e ideológica), ordem de batalha aproximada 2024-2026. Doutrina assimétrica: mísseis
-- balísticos de precisão crescente, enxames de drones Shahed baratos, guerra de montanha nos
-- Zagros e no noroeste, e sanções internacionais que atrasam a indústria pesada e a manutenção
-- do parque blindado (largamente pré-1979 ou importado).

-- ===== unit_type próprios (100+20*16 .. 119+20*16 = 420..439) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (420,'Bateria de Mísseis Balísticos','support',2.2,55,1.6,18),
 (421,'Infantaria de Montanha IRGC','ground',1.3,35,0.9,22),
 (422,'Esquadrão de Drones Shahed','support',0.8,20,0.6,30);

INSERT INTO unit_stat VALUES
 (420,'soft_atk',26),(420,'hard_atk',3), (420,'defense',4), (420,'breakthrough',4), (420,'armor',0),(420,'piercing',10),(420,'hardness',0.15),(420,'hp',5),
 (421,'soft_atk',8), (421,'hard_atk',1), (421,'defense',30),(421,'breakthrough',10),(421,'armor',0),(421,'piercing',6), (421,'hardness',0.10),(421,'hp',28),
 (422,'soft_atk',14),(422,'hard_atk',3), (422,'defense',3), (422,'breakthrough',2), (422,'armor',0),(422,'piercing',12),(422,'hardness',0.10),(422,'hp',4);

INSERT INTO unit_tag VALUES
 (420,'support'),(420,'ground'),
 (421,'infantry'),(421,'ground'),(421,'irgc'),
 (422,'support'),(422,'ground');

-- ===== espíritos nacionais + modificadores (ids 420..439) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('IRN_guarda_revolucionaria','IRN','Guarda Revolucionária Paralela',
   'O Corpo dos Guardiães da Revolução Islâmica opera como força ideológica paralela ao Artesh, com moral e motivação superiores: as suas unidades atacam com mais convicção.'),
 ('IRN_programa_misseis_drones','IRN','Programa de Mísseis e Drones',
   'Décadas de investimento em mísseis balísticos e drones de baixo custo (como o Shahed) dão às armas de apoio iranianas um poder de fogo acima da média.'),
 ('IRN_guerra_assimetrica_montanha','IRN','Guerra Assimétrica de Montanha',
   'A cordilheira dos Zagros e o treino em guerrilha favorecem tanto a defesa como emboscadas de ataque em terreno montanhoso.'),
 ('IRN_sancoes_economicas','IRN','Sanções Económicas',
   'Décadas de sanções internacionais isolam a indústria pesada iraniana: o parque blindado é maioritariamente antigo ou importado, e o comando central perde eficiência a coordenar um esforço de guerra prolongado.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (420,'spirit',NULL,           NULL,     'str_attacker','irgc',    'mul',1.20,'IRN','IRN_guarda_revolucionaria'),
 (421,'spirit',NULL,           NULL,     'str_attacker','irgc',    'add',0.10,'IRN','IRN_guarda_revolucionaria'),
 (422,'spirit',NULL,           NULL,     'str_attacker','support', 'mul',1.15,'IRN','IRN_programa_misseis_drones'),
 (423,'spirit',NULL,           NULL,     'str',         'support', 'add',0.05,'IRN','IRN_programa_misseis_drones'),
 (424,'spirit','terrain','mountain','str_defender', NULL,     'mul',1.15,'IRN','IRN_guerra_assimetrica_montanha'),
 (425,'spirit','terrain','mountain','str_attacker', NULL,     'mul',1.10,'IRN','IRN_guerra_assimetrica_montanha'),
 (426,'spirit',NULL,           NULL,     'command',     NULL,      'mul',0.90,'IRN','IRN_sancoes_economicas'),
 (427,'spirit',NULL,           NULL,     'str_attacker','armored', 'mul',0.85,'IRN','IRN_sancoes_economicas');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('IRN','production_speed',0.55),
 ('IRN','org_regain',0.85),
 ('IRN','start_army_mult',1.3);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('IRN','República Islâmica','Líder Supremo e Comandante-Chefe das Forças Armadas',
  'Doutrina de dissuasão assimétrica: mísseis balísticos e enxames de drones para atingir alvos distantes, guerra de montanha nas fronteiras, e uma Guarda Revolucionária ideológica a par de um exército regular convencional.',
  'Não-alinhado (Eixo de Resistência: Hezbollah, milícias xiitas, cooperação com a Rússia e a China)',
  'O Irão sustenta um dos maiores e mais ideologicamente divididos exércitos do Médio Oriente, com o Artesh regular a par da Guarda Revolucionária (IRGC), estrutura paralela com forças terrestres, navais e aeroespaciais próprias. Décadas de sanções atrasaram a modernização do parque blindado, mas Teerão compensou com um dos maiores arsenais de mísseis balísticos e drones da região e uma sólida capacidade de guerra de montanha nos Zagros e a noroeste, junto à fronteira com o Iraque e a Turquia.');

-- ===== templates próprios (ids 1+50*16..50+50*16 = 801..850) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (801,'IRN','Brigada de Infantaria de Montanha IRGC'),
 (802,'IRN','Grupo de Mísseis Balísticos'),
 (803,'IRN','Esquadrão de Combate por Drones');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (801,421,5),(801,1,2),(801,4,1),
 (802,420,4),(802,5,2),
 (803,422,5),(803,1,2);

-- ===== brigadas/regimentos reais nomeados (ids 804..850) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (804,'IRN','Comando Terrestre do IRGC (Sarallah)','Infantaria','Tehran'),
 (805,'IRN','21ª Divisão de Infantaria (Tabriz)','Brigada de Infantaria de Montanha IRGC','East Azarbaijan'),
 (806,'IRN','77ª Divisão Blindada de Khorasan','Blindada','Razavi Khorasan'),
 (807,'IRN','92ª Divisão Blindada (Zahedan)','Blindada','Sistan and Baluchestan'),
 (808,'IRN','30ª Divisão Blindada de Gadir','Blindada','Esfahan'),
 (809,'IRN','Comando de Forças Especiais Nohed','Infantaria AT','Fars'),
 (810,'IRN','Corpo de Defesa do Cáucaso','Brigada de Infantaria de Montanha IRGC','Kermanshah'),
 (811,'IRN','Grupo de Mísseis de Khuzestão','Grupo de Mísseis Balísticos','Khuzestan'),
 (812,'IRN','Esquadrão de Drones de Qazvin','Esquadrão de Combate por Drones','Qazvin'),
 (813,'IRN','Guarnição da Capital','Infantaria','Tehran');

-- ===== correcção de terreno =====
UPDATE region SET terrain='mountain'
 WHERE owner_id=(SELECT id FROM country WHERE tag='IRN')
   AND name IN ('Chahar Mahall and Bakhtiari','Kohgiluyeh and Buyer Ahmad');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('irn_economia_resistencia','IRN','Economia de Resistência','As sanções empurram Teerão para a substituição de importações e a autossuficiência industrial.',35,NULL,1),
 ('irn_expansao_guarda_revolucionaria','IRN','Expansão da Guarda Revolucionária','O IRGC cresce como estrutura paralela às forças armadas regulares, reforçando doutrina e efectivos ideológicos.',42,NULL,2),
 ('irn_programa_misseis_drones','IRN','Programa de Mísseis e Drones','Investimento acelerado em mísseis balísticos e enxames de drones para projectar força a longa distância.',35,NULL,3),
 ('irn_industrializacao_defesa','IRN','Industrialização da Defesa','Fábricas estatais passam a produzir blindados, mísseis e componentes outrora importados.',49,'irn_economia_resistencia',4),
 ('irn_enriquecimento_nuclear','IRN','Programa de Enriquecimento Nuclear','Centrifugadoras em Natanz e Fordow avançam o programa nuclear, oficialmente dedicado à investigação científica.',70,'irn_programa_misseis_drones',5),
 ('irn_mobilizacao_basij','IRN','Mobilização do Basij','A milícia popular Basij é reorganizada como reserva de mobilização em massa em caso de guerra.',42,'irn_expansao_guarda_revolucionaria',6),
 ('irn_guerra_assimetrica_zagros','IRN','Guerra Assimétrica nos Zagros','Doutrina de defesa em profundidade nas cordilheiras fronteiriças, explorando o terreno montanhoso contra um invasor superior.',35,'irn_mobilizacao_basij',7),
 ('irn_industria_aeroespacial','IRN','Indústria Aeroespacial Nacional','Programas espaciais civis e militares, satélites e lançadores, impulsionam a investigação e a produção de tecnologia de mísseis.',56,'irn_industrializacao_defesa',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('irn_economia_resistencia','industry',1.08),
 ('irn_expansao_guarda_revolucionaria','conscription',1.10),
 ('irn_expansao_guarda_revolucionaria','org_regain',1.05),
 ('irn_programa_misseis_drones','production_speed',1.10),
 ('irn_industrializacao_defesa','industry',1.10),
 ('irn_industrializacao_defesa','production_speed',1.05),
 ('irn_enriquecimento_nuclear','research_speed',1.15),
 ('irn_mobilizacao_basij','conscription',1.15),
 ('irn_guerra_assimetrica_zagros','org_regain',1.10),
 ('irn_industria_aeroespacial','research_speed',1.10),
 ('irn_industria_aeroespacial','industry',1.05);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('irn_industria_aeroespacial','irn_expansao_guarda_revolucionaria');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('IRN_adv_bazar','IRN','economia','Mestre do Bazar','💰',170,'Vende bem o que ninguém devia poder comprar.'),
 ('IRN_adv_guardioes','IRN','seguranca','Comissário dos Guardiões','🕵',175,'Duas polícias a vigiarem-se uma à outra.');
INSERT INTO advisor_effect VALUES ('IRN_adv_bazar','export_price',1.18);
INSERT INTO advisor_effect VALUES ('IRN_adv_guardioes','counter_intel',1.3);
INSERT INTO advisor_effect VALUES ('IRN_adv_guardioes','conscription',1.08);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('IRN_resistencia','Economia de Resistência','☪',10,'IRN');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('IRN_law_vizinhos','IRN_resistencia','Comércio pelos vizinhos','O que não entra pela porta entra pela fronteira do lado.',0,1,'IRN'),
 ('IRN_law_contorno','IRN_resistencia','Contorno das sanções','Frota fantasma, bancos amigos e contabilidade criativa.',1,0,'IRN'),
 ('IRN_law_autarcia','IRN_resistencia','Autarcia revolucionária','Fecha-se a economia e fabrica-se tudo, mesmo mal.',2,0,'IRN');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('IRN_law_vizinhos','export_share',1.1),
 ('IRN_law_contorno','industry',1.06),
 ('IRN_law_contorno','counter_intel',1.1),
 ('IRN_law_autarcia','production_speed',1.1),
 ('IRN_law_autarcia','counter_intel',1.15),
 ('IRN_law_autarcia','research_speed',0.92);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('IRN_gen_assimetrico','Mestre da Guerra Assimétrica','defense',1.16,125,'IRN','☪','Não dá batalha onde o inimigo quer: dá-a onde o inimigo não pode.'),
 ('IRN_gen_milicia','Chefe das Milícias','org_regain',1.15,120,'IRN','🕌','Chama cem mil voluntários e sabe onde os pôr.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('IRN_escola','Guerra Assimétrica','☪',10,'IRN');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('IRN_doc_assimetrica','IRN_escola','Escola Assimétrica','Não se dá batalha onde o inimigo quer.',50,NULL,1,'IRN'),
 ('IRN_doc_mosaico','IRN_escola','Defesa em Mosaico','Trinta comandos que não dependem uns dos outros não caem juntos.',110,'IRN_doc_assimetrica',2,'IRN'),
 ('IRN_doc_basij','IRN_escola','Mobilização Popular','Cem mil voluntários chamados numa semana, e alguém que sabe onde os pôr.',190,'IRN_doc_mosaico',3,'IRN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('IRN_doc_assimetrica','defense',1.07),
 ('IRN_doc_mosaico','defense',1.06),
 ('IRN_doc_mosaico','org_regain',1.06),
 ('IRN_doc_basij','conscription',1.12),
 ('IRN_doc_basij','attack',1.05);

-- ===== escola nacional do ar (army_doctrine_branch.domain=ar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('IRN_ar','Asas Remendadas','🕌',11,'IRN','ar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('IRN_ar_remendo','IRN_ar','Escola do Remendo','Quarenta anos sem peças ensinaram a manter no ar o que devia estar no chão.',50,NULL,1,'IRN'),
 ('IRN_ar_drone_ar','IRN_ar','Enxame de Drones','Cem aparelhos baratos custam ao inimigo mais do que valem.',110,'IRN_ar_remendo',2,'IRN'),
 ('IRN_ar_profundidade_ar','IRN_ar','Golpe em Profundidade','O que não chega de avião chega de outra maneira, e chega longe.',185,'IRN_ar_drone_ar',3,'IRN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('IRN_ar_remendo','air_upkeep',0.88),
 ('IRN_ar_drone_ar','air_losses',0.93),
 ('IRN_ar_profundidade_ar','air_bombing',1.11);

-- ===== escola nacional do mar (army_doctrine_branch.domain=mar) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag,domain) VALUES
 ('IRN_mar','Enxame do Golfo','🏴',12,'IRN','mar');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('IRN_mar_enxame','IRN_mar','Ataque em Enxame','Quarenta lanchas rápidas contra um cruzador: alguma passa.',50,NULL,1,'IRN'),
 ('IRN_mar_ormuz','IRN_mar','Fecho de Ormuz','Um estreito de trinta e três quilómetros com um quinto do petróleo do mundo.',110,'IRN_mar_enxame',2,'IRN'),
 ('IRN_mar_mini_sub','IRN_mar','Submarinos de Bolso','Água rasa e barcos pequenos: onde o grande não entra.',185,'IRN_mar_ormuz',3,'IRN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('IRN_mar_enxame','naval_blockade',1.12),
 ('IRN_mar_ormuz','naval_patrol',1.1),
 ('IRN_mar_mini_sub','naval_upkeep',0.9);

-- ===== comandante nacional de asa (general.domain=ar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('IRN_ar_gen_golfo_irn','Chefe da Asa do Golfo','air_losses',0.88,145,'IRN','🦅','Sai do sol e volta antes de a defesa perceber de onde veio.','ar',45);

-- ===== comandante nacional de esquadra (general.domain=mar) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note,domain,xp) VALUES
 ('IRN_mar_gen_lanchas_irn','Chefe das Lanchas do Golfo','naval_blockade',1.16,145,'IRN','🐟','Com barcos pequenos fecha um golfo a petroleiros grandes.','mar',45);
