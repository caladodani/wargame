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

-- Mar: travessias marítimas entre regiões costeiras (sea_link na static.db).
INSERT INTO rule (key,value,note) VALUES
 ('sea_speed_kmd',400,'km por dia de uma divisão embarcada (Atlântico ≈ 8 dias)'),
 ('sea_min_days',2,'dias mínimos de qualquer travessia marítima (embarque + desembarque)');

-- Paz branca: guerra sem captura entre os dois durante isto fecha em uti possidetis (TruceSystem).
INSERT INTO rule (key,value,note) VALUES
 ('war_white_peace_days',240,'dias de estagnação até paz branca automática');

-- Vitória: domínio mundial por população controlada (VictorySystem).
INSERT INTO rule (key,value,note) VALUES
 ('victory_pop_share',0.6,'fracção da população mundial controlada que dá o domínio do mundo'),
 ('victory_check_days',7,'de quantos em quantos dias se verifica o domínio');

-- Eventos com escolhas (news_event_option; jogador escolhe, IA fica com a primeira).
INSERT INTO news_event (id,day,country_tag,title,body) VALUES
 ('prt_orcamento_defesa',60,'PRT','Orçamento da Defesa','O parlamento debate para onde vai o reforço orçamental das Forças Armadas.'),
 ('bra_reforma_forcas',80,'BRA','Reforma das Forças Armadas','Brasília decide a prioridade da reestruturação militar.');
INSERT INTO news_event_option (id,event_id,sort,title) VALUES
 ('prt_orc_industria','prt_orcamento_defesa',0,'Investir na indústria de defesa'),
 ('prt_orc_pessoal','prt_orcamento_defesa',1,'Reforçar o recrutamento'),
 ('bra_ref_producao','bra_reforma_forcas',0,'Modernizar as linhas de produção'),
 ('bra_ref_treino','bra_reforma_forcas',1,'Apostar no treino e prontidão');
INSERT INTO news_event_option_effect (option_id,stat_key,value) VALUES
 ('prt_orc_industria','industry',1.05),
 ('prt_orc_pessoal','conscription',1.08),
 ('bra_ref_producao','production_speed',1.06),
 ('bra_ref_treino','org_regain',1.06);

-- Construção de infraestrutura (ConstructionSystem + BuildInfrastructureCommand).
INSERT INTO rule (key,value,note) VALUES
 ('infra_max',2.0,'tecto da infraestrutura por região'),
 ('infra_step',0.25,'quanto sobe por obra concluída'),
 ('infra_build_cost',40,'pontos de produção pagos ao iniciar a obra'),
 ('infra_build_days',30,'dias de obra'),
 ('capture_infra_hit',0.15,'infraestrutura perdida quando a região é capturada'),
 ('infra_min',0.3,'chão da infraestrutura'),
 ('ai_build_reserve',150,'a IA só inicia obras com dinheiro acima disto');

-- Leis nacionais (law + law_effect; uma activa por grupo, mudança custa law_change_cost).
INSERT INTO rule (key,value,note) VALUES
 ('law_change_cost',30,'pontos de produção por mudança de lei'),
 ('ai_law_escalate_money',120,'a IA em guerra sobe de lei com dinheiro acima disto');
INSERT INTO law (id,grp,name,description,sort,is_default) VALUES
 ('consc_volunteer','conscription','Exército voluntário','Só voluntários: sem penalizações.',0,1),
 ('consc_limited','conscription','Conscrição limitada','Serviço militar parcial.',1,0),
 ('consc_extensive','conscription','Conscrição alargada','Grande parte da população em idade militar é chamada.',2,0),
 ('consc_service','conscription','Serviço obrigatório total','Mobilização em massa: a economia ressente-se.',3,0),
 ('econ_civilian','economy','Economia civil','Produção civil normal.',0,1),
 ('econ_partial','economy','Mobilização parcial','Parte da indústria vira produção militar.',1,0),
 ('econ_war','economy','Economia de guerra','Tudo para o esforço de guerra.',2,0);
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('consc_limited','conscription',1.25),('consc_limited','industry',0.98),
 ('consc_extensive','conscription',1.6),('consc_extensive','industry',0.95),('consc_extensive','org_regain',0.97),
 ('consc_service','conscription',2.5),('consc_service','industry',0.90),('consc_service','org_regain',0.94),
 ('econ_partial','production_speed',1.10),('econ_partial','research_speed',0.97),
 ('econ_war','production_speed',1.25),('econ_war','research_speed',0.93),('econ_war','org_regain',1.03);

-- Fortificações (BuildFortCommand + ConstructionSystem; defensores × (1 + nível × fort_defense_per_level)).
INSERT INTO rule (key,value,note) VALUES
 ('fort_max',5,'nível máximo de fortificação por região'),
 ('fort_build_cost',30,'pontos de produção por nível'),
 ('fort_build_days',20,'dias de obra por nível'),
 ('fort_defense_per_level',0.15,'bónus de força dos defensores por nível');

-- Apoio financeiro entre aliados de facção (TransferMoneyCommand).
INSERT INTO rule (key,value,note) VALUES
 ('ai_aid_reserve',300,'a IA só envia apoio com dinheiro acima disto'),
 ('ai_aid_share',0.25,'fracção do excedente enviada por ronda ao aliado em guerra mais pobre');

-- Propor paz branca (OfferPeaceCommand): a IA aceita com a guerra parada há peace_stale_days
-- ou sem exército para continuar.
INSERT INTO rule (key,value,note) VALUES
 ('peace_stale_days',60,'dias sem progresso a partir dos quais a IA aceita paz branca');

-- Espionagem (StartSpyOpCommand + EspionageSystem): operações one-shot pagas à partida,
-- concluem passado `days` e aplicam o efeito ao alvo nesse dia.
INSERT INTO spy_op (id,name,description,cost,days,effect,magnitude) VALUES
 ('roubo_fundos','Roubo de fundos','Agentes desviam uma fracção do tesouro do alvo.',40,20,'steal_money',0.20),
 ('sabotagem_fabrica','Sabotagem industrial','Explosões nas linhas de produção: parte do progresso das encomendas perde-se.',50,25,'sabotage_production',0.50),
 ('agitacao','Agitação social','Propaganda e greves: a estabilidade do alvo cai.',60,30,'stability_hit',15),
 ('rede_info','Rede de informação','Espiões infiltrados: vês o tesouro, os homens e a produção do alvo durante uns tempos.',30,15,'intel',60);
INSERT INTO rule (key,value,note) VALUES
 ('ai_spy_reserve',200,'a IA só lança operações de espionagem com dinheiro acima disto');

-- Resistência nas regiões ocupadas (ResistanceSystem)
INSERT INTO rule VALUES ('resistance_growth', 0.02, 'subida diária da resistência numa região ocupada sem guarnição');
INSERT INTO rule VALUES ('resistance_suppress', 0.04, 'descida diária com divisão do ocupante presente (ou ocupação terminada)');
INSERT INTO rule VALUES ('resistance_output_hit', 0.5, 'corte máximo do rendimento da região ocupada (a resistência 1.0)');

-- Leis de segurança interna (grupo security): contra-espionagem — operações inimigas
-- demoram × counter_intel do alvo (StartSpyOpCommand), à custa de indústria.
INSERT INTO law (id,grp,name,description,sort,is_default) VALUES
 ('seg_liberdades','security','Liberdades civis','Sem vigilância interna: espiões estrangeiros circulam à vontade.',0,1),
 ('seg_vigilancia','security','Vigilância interna','Contra-espionagem activa: operações inimigas demoram mais.',1,0),
 ('seg_policial','security','Estado policial','Repressão total: espiar-nos é quase impossível, a economia sofre.',2,0);
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('seg_vigilancia','counter_intel',1.5),('seg_vigilancia','industry',0.97),
 ('seg_policial','counter_intel',2.0),('seg_policial','industry',0.92),('seg_policial','org_regain',0.97);

-- Políticas de ocupação (grupo occupation): modulam a resistência nas regiões ocupadas
-- (ResistanceSystem × resistance_growth do ocupante) e o rendimento ocupado (EconomySystem × occupied_yield).
INSERT INTO law (id,grp,name,description,sort,is_default) VALUES
 ('occ_gentle','occupation','Ocupação branda','Mão leve: menos resistência, menos extração.',0,0),
 ('occ_standard','occupation','Ocupação padrão','Administração militar normal.',1,1),
 ('occ_harsh','occupation','Ocupação dura','Extração máxima: a população resiste mais.',2,0);
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('occ_gentle','resistance_growth',0.5),('occ_gentle','occupied_yield',0.85),
 ('occ_harsh','resistance_growth',1.5),('occ_harsh','occupied_yield',1.2);

-- Dissolver divisões (DisbandDivisionCommand): refund parcial de homens, proporcional ao HP.
INSERT INTO rule (key,value,note) VALUES
 ('disband_manpower_refund',0.5,'fracção dos homens recuperada ao dissolver uma divisão');

-- Roubo de tecnologia (efeito research_boost): dá `magnitude` dias de progresso à investigação activa do autor.
INSERT INTO spy_op (id,name,description,cost,days,effect,magnitude) VALUES
 ('roubo_tech','Roubo de tecnologia','Agentes copiam os planos do alvo: a tua investigação em curso avança de um golpe.',70,35,'research_boost',30);

-- Pacto de não-agressão (ProposeNonAggressionCommand): bloqueia declarações de guerra entre os dois
-- durante nap_days. A IA aceita se não está a justificar guerra contra o proponente e é mais fraca
-- ou partilha um inimigo.
INSERT INTO rule (key,value,note) VALUES
 ('nap_days',180,'duração do pacto de não-agressão'),
 ('nap_cost',20,'pontos de produção para propor o pacto');

-- Recursos estratégicos (ResourceSystem): controlar depósitos multiplica stats do país.
INSERT INTO resource (id,name,stat_key,per_unit,cap) VALUES
 ('aco','Aço','production_speed',0.02,10),
 ('petroleo','Petróleo','industry',0.015,10),
 ('raros','Metais raros','research_speed',0.02,5);

-- Retirada manual de batalha (RetreatFromBattleCommand): sai do combate com penalização de organização.
INSERT INTO rule (key,value,note) VALUES
 ('retreat_org_penalty',0.5,'multiplicador de organização ao retirar de uma batalha');

-- A IA retira de batalhas muito desequilibradas (org própria < org inimiga × ai_retreat_ratio).
INSERT INTO rule (key,value,note) VALUES
 ('ai_retreat_ratio',0.25,'limiar de org relativa abaixo do qual a IA retira da batalha');

-- Comércio de recursos (TradeSystem)
INSERT INTO rule VALUES ('trade_price_per_unit', 2, 'pontos por dia por unidade de recurso alugada');

-- Reserva de dinheiro abaixo da qual a IA não propõe pactos de não-agressão.
INSERT INTO rule (key,value,note) VALUES
 ('ai_nap_reserve',100,'em guerra, a IA propõe NAP a vizinhos neutros se tiver dinheiro acima disto');

-- Gráficos de evolução (HistorySystem)
INSERT INTO rule VALUES ('history_sample_days', 7, 'dias entre amostras dos gráficos');
INSERT INTO rule VALUES ('history_tracked', 8, 'países por amostra (jogador + maiores)');

-- Contra-espionagem: expulsa todas as operações do alvo contra nós (efeito purge_spies).
INSERT INTO spy_op VALUES ('contra_espionagem','Contra-espionagem','Expulsa as redes de espionagem deste país contra nós.',35,12,'purge_spies',0);
