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
