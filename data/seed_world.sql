-- Política mundial no dia 0 (2030): guerras a decorrer, agressividade da IA, regras dos objectivos de guerra.
INSERT INTO rule (key,value,note) VALUES
 ('ai_war_min_day',30,'a IA não declara guerras antes deste dia'),
 ('ai_war_player_min_day',90,'a IA não declara guerra ao jogador antes deste dia'),
 ('ai_war_chance',0.03,'probabilidade por ronda da IA (× aggression do país) de declarar guerra a um vizinho fraco'),
 ('ai_war_ratio',2.0,'a IA só declara guerra a quem tem ≤ (divisões próprias / isto) divisões');

INSERT OR IGNORE INTO start_war (a_tag,b_tag) VALUES ('RUS','UKR');

-- Facções (alianças defensivas, HoI4): declarar guerra a um membro chama os outros contra o agressor
-- (DeclareWarCommand.Execute). Todas as tags confirmadas em data/static.db (tools/check_countries.py).
INSERT INTO faction (id,name,description) VALUES
 ('nato',    'NATO',                         'Aliança do Atlântico Norte: defesa colectiva entre a Europa e a América do Norte (artigo 5.º).'),
 ('otsc',    'OTSC',                         'Organização do Tratado de Segurança Colectiva: aliança militar liderada pela Rússia no espaço pós-soviético.'),
 ('anzus',   'ANZUS',                        'Tratado de segurança mútua entre os Estados Unidos, a Austrália e a Nova Zelândia.'),
 ('us_jpn',  'Aliança EUA-Japão',            'Tratado de segurança mútua entre os Estados Unidos e o Japão.'),
 ('us_kor',  'Aliança EUA-Coreia do Sul',    'Tratado de defesa mútua entre os Estados Unidos e a Coreia do Sul.'),
 ('cn_prk',  'Tratado Sino-Coreano',         'Tratado de amizade, cooperação e assistência mútua entre a China e a Coreia do Norte.'),
 ('ru_prk',  'Tratado Rússia-Coreia do Norte','Parceria estratégica global de 2024 entre a Rússia e a Coreia do Norte, com cláusula de defesa mútua.');

INSERT INTO faction_member (faction_id,country_tag) VALUES
 ('nato','USA'),('nato','CAN'),('nato','GBR'),('nato','FRA'),('nato','DEU'),('nato','ITA'),('nato','ESP'),
 ('nato','PRT'),('nato','NLD'),('nato','BEL'),('nato','LUX'),('nato','DNK'),('nato','NOR'),('nato','ISL'),
 ('nato','TUR'),('nato','GRC'),('nato','POL'),('nato','CZE'),('nato','HUN'),('nato','SVK'),('nato','ROU'),
 ('nato','BGR'),('nato','SVN'),('nato','HRV'),('nato','ALB'),('nato','MNE'),('nato','MKD'),('nato','EST'),
 ('nato','LVA'),('nato','LTU'),('nato','FIN'),('nato','SWE'),
 ('otsc','RUS'),('otsc','BLR'),('otsc','KAZ'),('otsc','KGZ'),('otsc','TJK'),('otsc','ARM'),
 ('anzus','USA'),('anzus','AUS'),('anzus','NZL'),
 ('us_jpn','USA'),('us_jpn','JPN'),
 ('us_kor','USA'),('us_kor','KOR'),
 ('cn_prk','CHN'),('cn_prk','PRK'),
 ('ru_prk','RUS'),('ru_prk','PRK');

-- ===== eventos noticiosos (NewsSystem) =====
INSERT INTO news_event (id,day,country_tag,title,body) VALUES
 ('cimeira_nato',14,NULL,'Cimeira de emergência da NATO','Os aliados reúnem-se em Bruxelas para rever os planos de defesa colectiva perante a escalada global.'),
 ('crise_energia',40,NULL,'Crise energética mundial','O preço do gás dispara; governos desviam orçamento para as reservas estratégicas.'),
 ('ciberataque',70,NULL,'Vaga de ciberataques','Infraestruturas críticas atacadas em três continentes; a atribuição aponta para actores estatais.'),
 ('prt_expo_defesa',25,'PRT','Feira de defesa em Lisboa','A indústria nacional mostra o Pandur II e sistemas anti-drone; o Governo promete encomendas.'),
 ('bra_carnaval_civico',30,'BRA','Mobilização cívica no Brasil','Campanha nacional de alistamento voluntário excede todas as expectativas.');
INSERT INTO news_event_effect (event_id,stat_key,value) VALUES
 ('crise_energia','industry',0.97),
 ('prt_expo_defesa','production_speed',1.03),
 ('bra_carnaval_civico','conscription',1.05);

-- rules: manpower (ManpowerSystem — pool de homens estilo HoI4)
INSERT INTO rule (key,value,note) VALUES
 ('manpower_per_million_daily',60,'homens novos por dia por milhão de população controlada (× country_stat conscription)'),
 ('manpower_cap_share',0.05,'tecto do pool: fracção da população controlada (× conscription)'),
 ('manpower_start_share',0.5,'pool inicial = tecto × isto'),
 ('manpower_per_cost',500,'homens gastos por ponto de custo do template ao entregar uma divisão'),
 ('reinforce_hp_manpower',30,'homens por ponto de HP recuperado (RecoverySystem)'),
 ('reinforce_hp_money',0.05,'pontos de produção por ponto de HP recuperado');

-- rules: stability (StabilitySystem — estabilidade 0..100, alvo situacional)
INSERT INTO rule (key,value,note) VALUES
 ('stability_speed',0.5,'pontos por dia em direcção ao alvo'),
 ('stability_war_penalty',10,'alvo desce isto por guerra activa (conta no máximo 2 guerras)'),
 ('stability_occupied_penalty',40,'alvo desce isto × fracção da população própria ocupada por inimigos');

-- rules: diplomacy (DiplomacySystem — justificar objectivo de guerra antes de declarar)
INSERT INTO rule (key,value,note) VALUES
 ('war_justify_days',30,'dias a justificar um objectivo de guerra antes de poder declarar (HoI4)');

-- rules: peace (PeaceSystem — capitulação estilo HoI4)
INSERT INTO rule (key,value,note) VALUES
 ('capitulate_share',0.75,'capitula quando os inimigos controlam esta fracção da população das suas regiões'),
 ('capitulate_share_capital',0.5,'fracção que chega quando a capital está controlada por um inimigo');

-- aggression: 0 (omisso) = nunca começa guerras. Só os países que na realidade as ameaçam.
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('RUS','aggression',1.0), ('PRK','aggression',0.6), ('CHN','aggression',0.4), ('IRN','aggression',0.5),
 ('ISR','aggression',0.3), ('PAK','aggression',0.3), ('IND','aggression',0.2), ('TUR','aggression',0.3),
 ('USA','aggression',0.2), ('SAU','aggression',0.2), ('AZE','aggression',0.4), ('ETH','aggression',0.4),
 ('RWA','aggression',0.4), ('VEN','aggression',0.3), ('SDN','aggression',0.3), ('MLI','aggression',0.2),
 ('BLR','aggression',0.2), ('EGY','aggression',0.2), ('MAR','aggression',0.2), ('DZA','aggression',0.2);
