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

-- Entrincheiramento (EntrenchSystem): a tropa parada cava, e o que cavou só vale a defender. Marchar zera,
-- assaltar gasta, e o forte da região levanta o tecto do que ali se pode cavar.
INSERT INTO rule (key,value,note) VALUES
 ('entrench_per_day',0.5,'degraus de trincheira ganhos por dia parado'),
 ('entrench_max',5,'tecto de trincheira em campo aberto'),
 ('entrench_per_fort',1,'degraus a mais no tecto por nível de forte'),
 ('entrench_defense_per_level',0.06,'bónus de força a defender por degrau'),
 ('entrench_attack_loss',1.5,'degraus perdidos por dia de assalto');

-- Largura de frente (Frontage + CombatSystem): quantas divisões de cada lado tocam no inimigo por dia. O que
-- não cabe fica em reserva, sem bater nem apanhar, e rende a linha quando ela se parte. Terreno sem regra
-- própria usa front_width.
INSERT INTO rule (key,value,note) VALUES
 ('front_width',4,'largura de frente por omissão'),
 ('front_width_plain',6,'planície: campo aberto, cabe muita gente'),
 ('front_width_desert',6,'deserto: sem obstáculos, frente larga'),
 ('front_width_tundra',5,'tundra: aberta mas dura'),
 ('front_width_forest',4,'floresta: a mata parte a frente'),
 ('front_width_urban',3,'cidade: combate rua a rua'),
 ('front_width_mountain',3,'montanha: passa-se pelos desfiladeiros'),
 ('front_width_river',1,'quanto o rio aperta a frente de quem o atravessa'),
 ('front_width_min',1,'nunca menos do que isto');

-- Planos de batalha (BattlePlanSystem): um grupo de exércitos com frente atribuída e postura de linha
-- prepara o terreno enquanto está quieto e gasta o preparado quando marcha ou se bate.
INSERT INTO rule (key,value,note) VALUES
 ('planning_per_day',0.05,'preparação ganha por dia de frente parada'),
 ('planning_decay',0.18,'preparação perdida por dia, à conta das divisões em marcha ou combate'),
 ('planning_max',1,'preparação máxima de um plano'),
 ('planning_bonus',0.25,'força de combate que o plano completo acrescenta');

-- Doutrinas de exército (ArmyXpSystem + AdoptDoctrineCommand): escolas de guerra pagas com a experiência
-- que o exército junta em campanha. Escolhido um ramo, os outros fecham-se. Nada disto são as leis do
-- grupo doctrine: uma lei muda-se por decreto, uma escola aprende-se e não se desaprende.
INSERT INTO rule (key,value,note) VALUES
 ('army_xp_per_battle_day',0.4,'experiência por divisão nossa em batalha, por dia'),
 ('army_xp_per_day',0.1,'manobras: experiência por dia a quem tem exército no terreno'),
 ('army_xp_max',600,'tecto da experiência por gastar');

INSERT INTO army_doctrine_branch (id,name,icon,sort) VALUES
 ('movimento','Guerra de Movimento','⚡',1),
 ('fogo','Superioridade de Fogo','🎯',2),
 ('massa','Assalto em Massa','♟',3);

INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort) VALUES
 ('mov_1','movimento','Escola de Movimento','Colunas que andam mais do que combatem: quem chega primeiro escolhe o terreno.',40,NULL,1),
 ('mov_2','movimento','Concentração Blindada','Os carros deixam de ser apoio de infantaria e passam a punho fechado.',80,'mov_1',2),
 ('mov_3','movimento','Ponta de Lança','Rompe-se num ponto só e explora-se a brecha até à retaguarda.',140,'mov_2',3),
 ('mov_4','movimento','Guerra Relâmpago','A decisão vem da velocidade: o inimigo perde a guerra antes de perceber que começou.',220,'mov_3',4),
 ('fog_1','fogo','Escola de Fogo','A artilharia mata, a infantaria ocupa. Tudo o resto é detalhe.',40,NULL,1),
 ('fog_2','fogo','Artilharia de Corpo','Fogo de corpo de exército concentrado no sector que interessa.',80,'fog_1',2),
 ('fog_3','fogo','Apoio Aproximado','Observadores à frente e fogo a cair a duzentos metros da nossa linha.',140,'fog_2',3),
 ('fog_4','fogo','Barragem Rolante','A cortina de fogo anda à frente da infantaria ao ritmo do passo.',220,'fog_3',4),
 ('mas_1','massa','Escola de Massa','Homens é o que há: forma-se, arma-se e manda-se.',40,NULL,1),
 ('mas_2','massa','Ondas Sucessivas','Uma vaga atrás da outra até a linha inimiga não ter com que responder.',80,'mas_1',2),
 ('mas_3','massa','Profundidade Operacional','Reservas escalonadas em profundidade: o que se perde à frente reconstitui-se atrás.',140,'mas_2',3),
 ('mas_4','massa','Guerra Total','O país inteiro é retaguarda de uma frente só.',220,'mas_3',4);

INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('mov_1','move_speed',1.08),('mov_1','attack',1.03),
 ('mov_2','attack',1.06),('mov_2','org_regain',1.03),
 ('mov_3','attack',1.08),('mov_3','move_speed',1.05),
 ('mov_4','attack',1.10),('mov_4','move_speed',1.08),('mov_4','defense',0.97),
 ('fog_1','defense',1.06),
 ('fog_2','defense',1.06),('fog_2','attack',1.04),
 ('fog_3','attack',1.07),('fog_3','production_speed',1.03),
 ('fog_4','attack',1.08),('fog_4','defense',1.06),
 ('mas_1','conscription',1.10),
 ('mas_2','org_regain',1.06),('mas_2','conscription',1.05),
 ('mas_3','defense',1.05),('mas_3','org_regain',1.05),
 ('mas_4','conscription',1.15),('mas_4','industry',1.03);

-- Adidos militares (AttacheSystem + SendAttacheCommand): observar a guerra dos outros custa dinheiro por
-- dia e traz experiência de exército, que é o que paga as escolas de guerra a quem vive em paz.
INSERT INTO rule (key,value,note) VALUES
 ('attache_cost_per_day',0.5,'estadia diária do adido militar destacado'),
 ('attache_xp_per_day',0.3,'experiência de exército por dia de missão, enquanto o anfitrião estiver em guerra'),
 ('attache_min_days',10,'dias de estadia que o cofre tem de aguentar para a missão poder partir'),
 ('attache_ai_money',60,'cofre a partir do qual a IA em paz manda um adido observar guerra alheia');

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
INSERT INTO rule VALUES ('trade_price_per_unit', 2, 'preço de tabela por unidade de recurso alugada');
-- Mercado de recursos (TradeSystem.Price): o preço sobe com o que o vendedor já prometeu a terceiros e com
-- a guerra dele; o contrato trava-o até ao fim do prazo.
INSERT INTO rule (key,value,note) VALUES
 ('trade_price_scarcity',1.5,'peso da fatia já vendida no preço do vendedor'),
 ('trade_war_premium',1.4,'prémio de quem vende a meio de uma guerra'),
 ('trade_price_min',1,'chão do preço por unidade'),
 ('trade_price_max',12,'tecto do preço por unidade'),
 ('trade_deal_days',180,'prazo de um tratado longo, em dias'),
 ('trade_deal_deposit_days',10,'dias de contrato que o cofre tem de cobrir à assinatura');

-- Reserva de dinheiro abaixo da qual a IA não propõe pactos de não-agressão.
INSERT INTO rule (key,value,note) VALUES
 ('ai_nap_reserve',100,'em guerra, a IA propõe NAP a vizinhos neutros se tiver dinheiro acima disto');

-- Gráficos de evolução (HistorySystem)
INSERT INTO rule VALUES ('history_sample_days', 7, 'dias entre amostras dos gráficos');
INSERT INTO rule VALUES ('history_tracked', 8, 'países por amostra (jogador + maiores)');
INSERT INTO rule VALUES ('war_history_max', 40, 'guerras terminadas guardadas no resumo');
INSERT INTO rule VALUES ('war_goal_max', 4, 'regiões exigidas por objectivo de guerra');
INSERT INTO rule VALUES ('war_goal_capital_ratio', 2, 'vantagem em divisões para pôr a capital inimiga no objectivo');
INSERT INTO rule VALUES ('war_goal_period_days', 3, 'de quantos em quantos dias se revêem os objectivos');
INSERT INTO rule VALUES ('port_supply_factor', 0.85, 'abastecimento que chega por mar (1 = tão bom como por terra)');
INSERT INTO rule VALUES ('port_capacity_per_level', 6, 'divisões que cada nível de porto consegue abastecer do outro lado do mar');
INSERT INTO rule VALUES ('port_overflow_min', 0.35, 'chão do abastecimento por mar quando o cais está a rebentar pelas costuras');
INSERT INTO rule VALUES ('ai_port_supply_floor', 0.9, 'abaixo deste supply a IA manda construir porto para as tropas de além-mar');

-- Contra-espionagem: expulsa todas as operações do alvo contra nós (efeito purge_spies).
INSERT INTO spy_op (id,name,description,cost,days,effect,magnitude) VALUES ('contra_espionagem','Contra-espionagem','Expulsa as redes de espionagem deste país contra nós.',35,12,'purge_spies',0);

-- Desgaste de guerra: cada divisão perdida acumula desgaste (tecto exhaustion_max) que puxa
-- o alvo da estabilidade para baixo; em paz decai exhaustion_decay/dia.
INSERT INTO rule (key,value,note) VALUES
 ('exhaustion_per_division',2,'desgaste por divisão destruída'),
 ('exhaustion_max',30,'tecto do desgaste de guerra'),
 ('exhaustion_decay',0.1,'decaimento diário do desgaste em paz');

-- Veterania: XP por dia de combate (tecto xp_max) dá até veterancy_bonus de força extra.
INSERT INTO rule (key,value,note) VALUES
 ('xp_per_battle_day',1,'XP ganho por divisão por dia de batalha'),
 ('xp_max',100,'tecto de XP'),
 ('veterancy_bonus',0.25,'bónus de força a XP máximo');

-- Condecorações de divisão (tabela medal; MedalSystem). metric: xp | battles | captures.
-- Cada medalha ganha dá bonus de força, somado até medal_bonus_max.
CREATE TABLE IF NOT EXISTS medal (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT NOT NULL,
  metric TEXT NOT NULL, threshold REAL NOT NULL, bonus REAL NOT NULL, sort INTEGER NOT NULL);
INSERT INTO medal VALUES ('baptismo','Baptismo de Fogo','Aguentou a primeira batalha até ao fim.','battles',1,0.01,1);
INSERT INTO medal VALUES ('assalto','Estrela de Assalto','Tomou três regiões ao inimigo.','captures',3,0.03,2);
INSERT INTO medal VALUES ('campanha','Louvor de Campanha','Quarenta pontos de experiência em combate.','xp',40,0.02,3);
INSERT INTO medal VALUES ('aco','Cruz de Aço','Dez batalhas travadas e sobrevividas.','battles',10,0.04,4);
INSERT INTO medal VALUES ('imortais','Ordem dos Imortais','Veterania quase no tecto: a divisão é uma lenda.','xp',90,0.05,5);
INSERT INTO rule (key,value,note) VALUES
 ('medal_bonus_max',0.12,'tecto do bónus de força somado das condecorações'),
 ('medal_check_days',2,'de quantos em quantos dias se atribuem condecorações');

-- Honras de batalha (tabela division_honour; DivisionHonourSystem). Só UMA por divisão — a mais alta
-- merecida — e ela passa a fazer parte do nome. {r} é o nome da região onde a honra foi ganha.
-- metric: xp | battles | captures | medals. bonus = quanto mais depressa se recompõe fora de combate.
CREATE TABLE IF NOT EXISTS division_honour (
  id TEXT PRIMARY KEY, title TEXT NOT NULL, description TEXT NOT NULL,
  metric TEXT NOT NULL, threshold REAL NOT NULL, bonus REAL NOT NULL, sort INTEGER NOT NULL);
INSERT INTO division_honour VALUES ('ferro','Punhos de Ferro de {r}','Três batalhas travadas e ganhas de pé.','battles',3,0.10,1);
INSERT INTO division_honour VALUES ('lanceiros','Lanceiros de {r}','Cinco regiões tomadas ao inimigo.','captures',5,0.15,2);
INSERT INTO division_honour VALUES ('muralha','Muralha de {r}','Oito batalhas: a linha nunca cedeu onde ela estava.','battles',8,0.20,3);
INSERT INTO division_honour VALUES ('leoes','Leões de {r}','Sessenta pontos de experiência de guerra.','xp',60,0.30,4);
INSERT INTO division_honour VALUES ('imortal','Imortais de {r}','Quatro condecorações e a veterania quase no tecto.','medals',4,0.45,5);
INSERT INTO rule (key,value,note) VALUES
 ('honour_check_days',3,'de quantos em quantos dias se conferem honras de batalha'),
 ('honour_bonus_max',0.5,'tecto do bónus de recomposição dado pela honra');

-- Estações do ano (tabelas season, season_month, season_terrain; WeatherSystem). move_mult multiplica a
-- marcha, org_mult a recomposição de organização e attrition é a organização gasta por dia a quem está em
-- campo. season_terrain diz que terrenos a estação castiga mais (1 = a média).
CREATE TABLE IF NOT EXISTS season (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL,
  move_mult REAL NOT NULL, org_mult REAL NOT NULL, attrition REAL NOT NULL, note TEXT NOT NULL);
INSERT INTO season VALUES ('inverno','Inverno','❄',0.62,0.70,0.9,'Colunas atoladas, tropa gasta em campo aberto.');
INSERT INTO season VALUES ('primavera','Primavera','🌧',0.85,1.00,0.3,'Degelo e lama: anda-se mal, mas a tropa refaz-se.');
INSERT INTO season VALUES ('verao','Verão','☀',1.15,1.10,0.2,'Estradas secas e dias longos: é quando se ganham guerras.');
INSERT INTO season VALUES ('outono','Outono','🍂',0.90,0.95,0.4,'Chuva a chegar: as ofensivas começam a pesar.');
CREATE TABLE IF NOT EXISTS season_month (month INTEGER PRIMARY KEY, season_id TEXT NOT NULL);
INSERT INTO season_month VALUES (1,'inverno'),(2,'inverno'),(3,'primavera'),(4,'primavera'),(5,'primavera'),
 (6,'verao'),(7,'verao'),(8,'verao'),(9,'outono'),(10,'outono'),(11,'outono'),(12,'inverno');
CREATE TABLE IF NOT EXISTS season_terrain (season_id TEXT NOT NULL, terrain TEXT NOT NULL, bite REAL NOT NULL,
  PRIMARY KEY (season_id, terrain));
INSERT INTO season_terrain VALUES ('inverno','tundra',2.2),('inverno','mountain',1.8),('inverno','forest',1.2),
 ('inverno','urban',0.5),('inverno','desert',0.8),('inverno','plain',1.0);
INSERT INTO season_terrain VALUES ('verao','desert',2.0),('verao','urban',0.6),('verao','tundra',0.4);
INSERT INTO season_terrain VALUES ('primavera','forest',1.3),('primavera','plain',1.2),('primavera','urban',0.5);
INSERT INTO season_terrain VALUES ('outono','forest',1.2),('outono','mountain',1.3),('outono','urban',0.5);
INSERT INTO rule (key,value,note) VALUES
 ('season_shelter',0.4,'quanto do desgaste da estação sobra a quem está em terreno próprio');

-- Crónica da campanha (tabela chronicle_kind; ChronicleSystem). weight: 1 rotina, 2 de peso, 3 história.
-- chronicle_min_weight decide o que chega a ser escrito; chronicle_max é o tecto de entradas guardadas.
CREATE TABLE IF NOT EXISTS chronicle_kind (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL, weight INTEGER NOT NULL);
INSERT INTO chronicle_kind VALUES ('guerra','Guerra','⚔',3);
INSERT INTO chronicle_kind VALUES ('paz','Paz','🕊',3);
INSERT INTO chronicle_kind VALUES ('capitulacao','Capitulação','🏳',3);
INSERT INTO chronicle_kind VALUES ('baixa','Baixa no comando','🎖',3);
INSERT INTO chronicle_kind VALUES ('capital','Capital tomada','🏛',3);
INSERT INTO chronicle_kind VALUES ('dominio','Domínio mundial','👑',3);
INSERT INTO chronicle_kind VALUES ('bomba','Bomba atómica','☢',3);
INSERT INTO chronicle_kind VALUES ('revolta','Revolta','✊',2);
INSERT INTO chronicle_kind VALUES ('alianca','Aliança','🤝',2);
INSERT INTO chronicle_kind VALUES ('honra','Honra de batalha','▮',2);
INSERT INTO chronicle_kind VALUES ('promocao','Promoção','🎖',2);
INSERT INTO chronicle_kind VALUES ('foco','Foco nacional','🎯',2);
INSERT INTO chronicle_kind VALUES ('estacao','Estação','🌦',1);
INSERT INTO rule (key,value,note) VALUES
 ('chronicle_min_weight',2,'peso mínimo para um acontecimento entrar na crónica'),
 ('chronicle_max',400,'entradas guardadas na crónica; as mais antigas caem');

-- Edifícios regionais (tabela building; ConstructionSystem/BuildBuildingCommand)
CREATE TABLE IF NOT EXISTS building (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, cost REAL NOT NULL, days REAL NOT NULL,
  stat_key TEXT NOT NULL, per_level REAL NOT NULL, max_level INTEGER NOT NULL,
  coastal INTEGER NOT NULL DEFAULT 0,        -- 1 = só em região de costa
  supply_range REAL NOT NULL DEFAULT 0,      -- km de abastecimento projectado por mar, por nível
  yard TEXT NOT NULL DEFAULT '');            -- fila de fábricas que abre (Industry): civil | militar | naval
INSERT INTO building (id,name,cost,days,stat_key,per_level,max_level,coastal,supply_range,yard) VALUES
 ('fabrica','Fábrica',40,25,'industry',0.05,5,0,0,'civil'),
 ('laboratorio','Laboratório',50,30,'research_speed',0.06,3,0,0,''),
 ('arsenal','Arsenal',45,25,'production_speed',0.05,4,0,0,'militar'),
 ('porto','Porto',35,20,'port_capacity',0,2,1,900,'naval');

-- Capacidade industrial (Industry): quantas obras e quantas linhas de montagem andam ao mesmo tempo.
INSERT INTO rule (key,value,note) VALUES
 ('factory_civil_base',2,'fábricas civis de partida: obras em paralelo (infra, edifícios, fortificações)'),
 ('factory_civil_per_region',0.25,'fábricas civis extra por região controlada'),
 ('factory_mil_base',2,'fábricas militares de partida: encomendas da fila que avançam por dia'),
 ('factory_mil_per_region',0.15,'fábricas militares extra por região controlada'),
 ('factory_per_building',1,'fábricas que cada nível de um edifício de fila (building.yard) acrescenta'),
 ('yard_divisions',3,'divisões abastecidas por mar que cada estaleiro serve');

-- Modos de mapa (tabela map_mode; MapModes): o mesmo território pintado pela conta que interessa.
CREATE TABLE IF NOT EXISTS map_mode (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL,
  metric TEXT NOT NULL,                      -- owner | supply | resistance | industry | population
  low TEXT NOT NULL, high TEXT NOT NULL,     -- as duas pontas da legenda
  sort INTEGER NOT NULL);
INSERT INTO map_mode (id,name,icon,metric,low,high,sort) VALUES
 ('politico','Político','🌍','owner','','',0),
 ('abastecimento','Abastecimento','📦','supply','a seco','cheio',1),
 ('resistencia','Resistência','✊','resistance','calma','revolta',2),
 ('industria','Indústria','🏭','industry','terra rasa','fábricas',3),
 ('populacao','População','♟','population','deserto','multidão',4);

-- Missões aéreas (tabela air_mission; AirMissionSystem): o que um esquadrão vai fazer ao céu de uma região.
CREATE TABLE IF NOT EXISTS air_mission (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL,
  effect TEXT NOT NULL,                      -- superiority | support | bombing
  value REAL NOT NULL,                       -- o que cada asa vale nesse papel
  note TEXT NOT NULL, sort INTEGER NOT NULL);
INSERT INTO air_mission (id,name,icon,effect,value,note,sort) VALUES
 ('superioridade','Superioridade aérea','🛩','superiority',1,'Varre o céu da região: cada asa pesa na balança aérea do combate que lá se der.',0),
 ('apoio','Apoio próximo','💥','support',0.03,'Bate no chão ao lado da nossa tropa: cada asa soma força a quem ali combate.',1),
 ('bombardeamento','Bombardeamento','🎯','bombing',0.015,'Deita abaixo a infraestrutura de quem manda na região, dia após dia.',2);

-- Missões navais (tabela naval_mission; NavalMissionSystem): o que uma esquadra vai fazer ao mar de uma costa.
CREATE TABLE IF NOT EXISTS naval_mission (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL,
  effect TEXT NOT NULL,                      -- blockade | escort | patrol
  value REAL NOT NULL,                       -- o que cada navio vale nesse papel
  note TEXT NOT NULL, sort INTEGER NOT NULL);
INSERT INTO naval_mission (id,name,icon,effect,value,note,sort) VALUES
 ('bloqueio','Bloqueio naval','⚓','blockade',1,'Fecha o mar em frente àquela costa: enquanto lá estiver a esquadra, o cais não carrega nada e o abastecimento por mar não passa.',0),
 ('escolta','Escolta de comboios','🛡','escort',1,'Acompanha os nossos comboios: enquanto houver mais navios nossos do que os do bloqueio, o mar continua aberto.',1),
 ('patrulha','Patrulha','🔭','patrol',1,'Vigia aquele mar: a costa deixa de estar no nevoeiro e vê-se o que lá está.',2);

-- Doutrinas militares (grupo doctrine): defensiva / armas combinadas (default) / ofensiva.
INSERT INTO law VALUES
 ('doc_defensiva','doctrine','Doutrina defensiva','Prioridade à defesa: mais defesa e recuperação, menos ataque.',0,0),
 ('doc_combinada','doctrine','Armas combinadas','Equilíbrio ofensivo-defensivo.',1,1),
 ('doc_ofensiva','doctrine','Doutrina ofensiva','Tudo no ataque: mais ataque, menos defesa.',2,0);
INSERT INTO law_effect VALUES
 ('doc_defensiva','defense',1.12),('doc_defensiva','attack',0.95),('doc_defensiva','org_regain',1.05),
 ('doc_ofensiva','attack',1.10),('doc_ofensiva','defense',0.95);

-- Intel dá vantagem em combate; operação de fomentar deserção (cara, lenta).
INSERT INTO rule (key,value,note) VALUES
 ('intel_combat_bonus',1.05,'multiplicador de força de quem tem intel sobre o outro lado');
INSERT INTO spy_op (id,name,description,cost,days,effect,magnitude) VALUES ('fomentar_desercao','Fomentar deserção','Uma fracção das divisões inimigas com pior moral dissolve-se.',90,40,'desertion',0.1);

-- Sabotagem na retaguarda (scope 'region'): a operação escolhe uma região que o inimigo controla e
-- estraga o que lá está. É a resposta a uma frente parada — não se ganha terreno, tira-se-lhe o cais,
-- as vias ou as casamatas antes do assalto.
INSERT INTO spy_op (id,name,description,cost,days,effect,magnitude,scope) VALUES
 ('sabotagem_porto','Sabotagem do porto','Cargas nos guindastes e nos molhes: o cais desta região perde um nível e deixa de carregar o que carregava.',55,20,'sabotage_port',1,'region'),
 ('sabotagem_via','Sabotagem das vias','Pontes e caminhos-de-ferro pelos ares: a infraestrutura da região cai e leva tempo a repor-se.',45,15,'sabotage_infra',0.3,'region'),
 ('sabotagem_forte','Sabotagem das defesas','Minas nas casamatas: as fortificações da região perdem um nível.',50,18,'sabotage_fort',1,'region');
INSERT INTO chronicle_kind VALUES ('sabotagem','Sabotagem','💥',2);

-- Defesa da retaguarda (CounterIntelSystem): cada dia que uma equipa de sabotagem passa em terreno
-- inimigo é um dia em que pode ser apanhada. A guarnição do dono da região é o que mais pesa.
INSERT INTO rule (key,value,note) VALUES
 ('catch_base',0.03,'hipótese diária de a guarnição apanhar uma equipa de sabotagem'),
 ('catch_guard',0.02,'acrescento a essa hipótese por divisão de guarnição na região'),
 ('catch_max',0.35,'tecto da hipótese diária de apanhar a equipa');

-- Troca negociada de prisioneiros (PrisonerExchange, ExchangePrisonersCommand): homem por homem, com a
-- guerra a decorrer. Quem guarda muito mais gente do que o outro não troca a vantagem de mão-de-obra,
-- mas um pool de homens vazio compra qualquer troca.
INSERT INTO rule (key,value,note) VALUES
 ('exchange_return',0.85,'fracção dos prisioneiros trocados que chega viva a casa'),
 ('exchange_ai_edge',1.4,'quanto mais gente o outro lado tem de guardar para recusar a troca'),
 ('exchange_need_men',150000,'pool de homens abaixo do qual se aceita qualquer troca');

-- Propostas do outro lado (OfferSystem): a IA também bate à porta com uma troca de prisioneiros.
INSERT INTO rule (key,value,note) VALUES
 ('offer_period_days',10,'de quantos em quantos dias a IA volta a olhar para a mesa de propostas'),
 ('offer_days',20,'dias que uma proposta fica em cima da mesa antes de cair'),
 ('cede_ratio',0.6,'divisões da IA em fracção das nossas abaixo da qual ela paga a paz com uma região');

-- Nevoeiro de guerra (Vision): 1 = só se vêem as guarnições que temos como ver; 0 = mapa aberto.
INSERT INTO rule (key,value,note) VALUES
 ('fog_of_war',1,'nevoeiro de guerra ligado: guarnições alheias só à vista de fronteira, aliado ou espionagem');

-- Poder aéreo abstrato: esquadrões por país, pesam no combate terrestre.
INSERT INTO rule (key,value,note) VALUES
 ('air_wing_cost',60,'custo de um esquadrão aéreo'),
 ('air_combat_weight',0.15,'peso máximo da superioridade aérea na força (±15%)'),
 ('air_mission_upkeep',0.6,'custo por asa e por dia de uma missão aérea destacada'),
 ('air_dogfight_loss',0.04,'asas abatidas por dia no céu disputado, por asa do lado mais fraco'),
 ('air_bomb_infra_min',0.25,'chão da infraestrutura de uma região bombardeada'),
 ('air_support_max',0.35,'tecto do bónus de apoio próximo na força de quem combate'),
 ('air_mission_min_wings',1,'asas mínimas para destacar uma missão aérea'),
 ('air_ai_reserve',1,'asas que a IA guarda em casa antes de destacar missões'),
 ('ai_air_reserve',250,'reserva da IA antes de comprar esquadrões'),
 ('naval_ship_cost',90,'custo de um navio de guerra'),
 ('naval_mission_upkeep',0.8,'custo por navio e por dia de uma esquadra no mar'),
 ('naval_battle_loss',0.05,'navios ao fundo por dia em mar disputado, por navio do lado mais fraco'),
 ('naval_range_km',1500,'distância máxima, por rota marítima, entre a nossa costa e o mar da missão'),
 ('naval_mission_min_ships',1,'navios mínimos para destacar uma esquadra'),
 ('naval_ai_reserve',1,'navios que a IA guarda em casa antes de destacar esquadras'),
 ('convoy_base',20,'marinha mercante de partida de cada país'),
 ('convoy_cost',25,'custo de um comboio mercante'),
 ('convoy_per_sea_division',1,'mercantes presos por cada divisão abastecida por mar'),
 ('convoy_per_trade_unit',2,'mercantes presos por cada unidade importada num tratado'),
 ('convoy_raid_sink',0.25,'mercantes afundados por dia, por navio de um bloqueio que a escolta não desfaz');

-- Integração de território ocupado (IntegrationSystem)
INSERT INTO rule VALUES ('integration_days', 150, 'dias de ocupação calma até a região mudar de dono');
INSERT INTO rule VALUES ('integration_max_resist', 0.1, 'resistência máxima para a integração avançar');
INSERT INTO rule VALUES ('integration_decay', 2, 'recuo diário do progresso com resistência alta');
INSERT INTO law_effect VALUES ('occ_gentle', 'integration_speed', 1.5);
INSERT INTO law_effect VALUES ('occ_harsh', 'integration_speed', 0.6);

-- Armas nucleares: tech nuc_2 desbloqueia; ver BuildNukeCommand/NuclearStrikeCommand.
INSERT INTO rule (key,value,note) VALUES
 ('nuke_cost',400,'pontos de produção por ogiva nuclear'),
 ('nuke_div_hp_mult',0.3,'HP restante das divisões na região atingida'),
 ('nuke_div_org_mult',0.2,'organização restante das divisões atingidas'),
 ('nuke_infra_mult',0.5,'infra-estrutura restante após o ataque'),
 ('nuke_fort_damage',2,'níveis de forte destruídos pelo ataque'),
 ('nuke_stability_hit',10,'estabilidade que o país atingido perde'),
 ('nuke_exhaustion',5,'exaustão de guerra que o atingido ganha'),
 ('nuke_self_stability_hit',4,'estabilidade que o atacante perde (opinião mundial)'),
 ('ai_nuke_reserve',600,'reserva da IA antes de construir ogivas'),
 ('ai_nuke_min_divs',3,'divisões inimigas mínimas para a IA gastar uma ogiva');

-- Decisões nacionais (tabela decision; ActivateDecisionCommand/DecisionSystem)
CREATE TABLE IF NOT EXISTS decision (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, cost REAL NOT NULL, days INTEGER NOT NULL,
  cooldown INTEGER NOT NULL, stat_key TEXT NOT NULL, mult REAL NOT NULL);
INSERT INTO decision VALUES ('mobilizacao_industrial','Mobilização industrial',30,30,60,'industry',1.15);
INSERT INTO decision VALUES ('esforco_guerra','Esforço de guerra',35,30,60,'production_speed',1.2);
INSERT INTO decision VALUES ('fundos_ciencia','Fundos para a ciência',40,45,90,'research_speed',1.25);

-- Peso económico do terreno (EconomySystem.TerrainMult; 1 = neutro)
INSERT INTO rule VALUES ('terrain_income_urban', 1.35, 'cidades rendem mais');
INSERT INTO rule VALUES ('terrain_income_plain', 1.0, 'planície: linha de base');
INSERT INTO rule VALUES ('terrain_income_forest', 0.9, 'floresta rende menos');
INSERT INTO rule VALUES ('terrain_income_mountain', 0.8, 'montanha rende menos');
INSERT INTO rule VALUES ('terrain_income_desert', 0.7, 'deserto rende pouco');
INSERT INTO rule VALUES ('terrain_income_tundra', 0.65, 'tundra rende pouco');
INSERT INTO rule VALUES ('coastal_income_bonus', 1.1, 'porto/costa: comércio marítimo');

-- Reparação natural da infraestrutura (InfrastructureRepairSystem)
INSERT INTO rule VALUES ('infra_repair_per_day', 0.002, 'infraestrutura reposta por dia numa região calma');
INSERT INTO rule VALUES ('infra_repair_max_resist', 0.3, 'resistência acima da qual a ocupação não repara');

-- Comandantes contratáveis (tabela general; HireGeneralCommand/general_slots)
CREATE TABLE IF NOT EXISTS general (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, stat_key TEXT NOT NULL, mult REAL NOT NULL, cost REAL NOT NULL);
INSERT INTO general VALUES ('gen_ofensiva','Mestre da ofensiva','attack',1.10,120);
INSERT INTO general VALUES ('gen_defesa','Muralha','defense',1.10,120);
INSERT INTO general VALUES ('gen_logistica','Logístico','org_regain',1.10,100);
INSERT INTO general VALUES ('gen_manobra','Manobrador','move_speed',1.15,110);
INSERT INTO general VALUES ('gen_industria','Organizador industrial','industry',1.08,140);
INSERT INTO rule VALUES ('general_slots', 3, 'comandantes ao serviço por país');
INSERT INTO rule VALUES ('general_command_bonus', 2, 'quanto vale o bónus de um comandante quando é destacado para um grupo de exércitos em vez de servir o país todo');
INSERT INTO rule VALUES ('ai_general_reserve', 200, 'reserva que a IA guarda antes de contratar comandantes');
INSERT INTO rule (key,value,note) VALUES
 ('army_group_reserve_depth',3,'saltos de distância à frente a que um grupo em reserva se recolhe'),
 ('reserve_org_bonus',1.6,'multiplicador da recuperação de organização de um grupo em reserva'),
 ('reserve_hp_bonus',1.5,'multiplicador dos reforços de um grupo em reserva'),
 ('ai_group_rest_org',40,'organização média abaixo da qual a IA recolhe o grupo à reserva'),
 ('ai_group_ready_org',75,'organização média a partir da qual a IA devolve o grupo à frente');

-- Postos de comandante (tabela general_rank; GeneralXpSystem). O comandante destacado num grupo de
-- exércitos ganha experiência com as batalhas do grupo e sobe de posto; bonus soma-se ao
-- general_command_bonus, por isso um marechal veterano vale muito mais do que o mesmo homem no dia
-- em que foi contratado.
CREATE TABLE IF NOT EXISTS general_rank (
  level INTEGER PRIMARY KEY, name TEXT NOT NULL, xp REAL NOT NULL, bonus REAL NOT NULL);
INSERT INTO general_rank VALUES (1,'Brigadeiro',0,0);
INSERT INTO general_rank VALUES (2,'General de Divisão',40,0.5);
INSERT INTO general_rank VALUES (3,'General de Exército',110,1.0);
INSERT INTO general_rank VALUES (4,'Marechal',220,1.75);
INSERT INTO general_rank VALUES (5,'Marechal do Reino',360,2.5);
INSERT INTO rule (key,value,note) VALUES
 ('general_xp_battle',2,'experiência do comandante por batalha travada pelo grupo'),
 ('general_xp_win',3,'experiência extra do comandante por batalha ganha pelo grupo'),
 ('general_xp_capture',4,'experiência do comandante por região tomada por divisões do grupo'),
 ('general_xp_max',400,'tecto da experiência de campanha de um comandante');

-- Baixas no comando (tabela wound_kind; CommandCasualtySystem). Cada batalha travada por um exército
-- com comandante destacado é uma hipótese de o perder: days = dias fora de serviço (a zero quando é
-- fatal), weight = peso no sorteio, fatal = fica lá. Enquanto está ferido não soma nada ao país nem
-- amplifica nada no exército, e o comando passa a um substituto do estado-maior.
CREATE TABLE IF NOT EXISTS wound_kind (
  id TEXT PRIMARY KEY, name TEXT NOT NULL, icon TEXT NOT NULL, days INTEGER NOT NULL,
  weight REAL NOT NULL, fatal INTEGER NOT NULL DEFAULT 0);
INSERT INTO wound_kind VALUES ('arranhao','Ferimento ligeiro','🩹',6,50,0);
INSERT INTO wound_kind VALUES ('ferido','Ferido em combate','🩸',21,28,0);
INSERT INTO wound_kind VALUES ('grave','Ferido com gravidade','🏥',60,15,0);
INSERT INTO wound_kind VALUES ('morto','Morto em combate','⚰',0,7,1);
INSERT INTO rule (key,value,note) VALUES
 ('wound_chance',0.035,'probabilidade de o comandante de um exército cair por batalha travada'),
 ('wound_loss_mult',2.2,'quanto a derrota multiplica essa probabilidade');

-- Prisioneiros de guerra (PrisonerSystem). Uma divisão desfeita em terreno inimigo entrega
-- prisoner_share do seu efectivo a quem manda na região; enquanto lá estão trabalham (até
-- prisoner_work_max de indústria, tecto alcançado com prisoner_work_men homens), todos os dias
-- prisoner_escape foge de volta a casa, e a paz devolve prisoner_return do que resta.
INSERT INTO rule (key,value,note) VALUES
 ('prisoner_share',0.30,'fracção de uma divisão desfeita que se rende em vez de morrer'),
 ('prisoner_work_men',400000,'prisioneiros necessários para o bónus máximo de indústria'),
 ('prisoner_work_max',0.20,'bónus máximo de indústria dado pelo trabalho dos prisioneiros'),
 ('prisoner_escape',0.001,'fracção de prisioneiros que foge por dia e volta ao pool de casa'),
 ('prisoner_return',0.60,'fracção dos prisioneiros que volta a casa quando se assina a paz'),
 ('prisoner_news_men',20000,'leva de prisioneiros a partir da qual a captura dá notícia');
INSERT INTO chronicle_kind VALUES ('prisioneiros','Prisioneiros','⛓',2);

-- Grupos de exércitos com frente atribuída (ArmyGroupSystem)
INSERT INTO rule VALUES ('army_group_max', 6, 'grupos de exércitos por país');
INSERT INTO rule VALUES ('army_group_min_org', 35, 'organização mínima para uma divisão do grupo marchar');
INSERT INTO rule VALUES ('army_group_march_range', 25, 'saltos máximos que um grupo procura a frente atribuída');
INSERT INTO rule VALUES ('army_group_order_days', 2, 'dias entre ordens de marcha de um grupo');
INSERT INTO rule VALUES ('ai_group_min_divisions', 6, 'divisões mínimas para a IA levantar um grupo de exércitos');
INSERT INTO rule VALUES ('ai_group_share', 0.6, 'fatia do exército da IA que vai para o grupo; o resto fica de guarnição');
INSERT INTO rule VALUES ('ai_group_advance_ratio', 1.2, 'vantagem em divisões a partir da qual o grupo da IA avança em vez de defender');

-- Pontuação e título da campanha (CampaignReport, ecrã de fim de jogo)
INSERT INTO rule VALUES ('score_per_region', 4, 'pontos por região controlada no fim');
INSERT INTO rule VALUES ('score_per_million', 0.5, 'pontos por milhão de habitantes controlado');
INSERT INTO rule VALUES ('score_per_war_won', 120, 'pontos por guerra ganha');
INSERT INTO rule VALUES ('score_per_war_lost', 90, 'pontos perdidos por guerra perdida');
INSERT INTO rule VALUES ('score_per_battle', 3, 'pontos por batalha ganha');
INSERT INTO rule VALUES ('score_per_division_lost', 2, 'pontos perdidos por divisão perdida');
INSERT INTO rule VALUES ('score_per_advance', 8, 'pontos por tecnologia ou foco concluído');
INSERT INTO rule VALUES ('score_domination_bonus', 2, 'multiplicador da pontuação quando se domina o mundo');
INSERT INTO rule VALUES ('score_defeat_penalty', 0.4, 'multiplicador da pontuação de quem capitulou');
INSERT INTO rule VALUES ('rank_power_score', 1500, 'pontuação a partir da qual a campanha é "Potência regional"');

-- Tabela mundial de potências (PowerIndex/PowerRankingSystem). A nota de cada país é a soma pesada de
-- quatro parcelas normalizadas: população controlada, capacidade industrial, exército em campo e avanço
-- tecnológico. Os patamares (power_tier) são só o nome que se dá à quota de potência mundial.
CREATE TABLE IF NOT EXISTS power_tier (
  level INTEGER PRIMARY KEY, name TEXT NOT NULL, min_share REAL NOT NULL);
INSERT INTO power_tier VALUES (5,'Superpotência',0.20);
INSERT INTO power_tier VALUES (4,'Grande potência',0.10);
INSERT INTO power_tier VALUES (3,'Potência',0.04);
INSERT INTO power_tier VALUES (2,'Potência regional',0.015);
INSERT INTO power_tier VALUES (1,'Estado menor',0);
INSERT INTO rule (key,value,note) VALUES
 ('power_weight_pop',0.3,'peso da população controlada na nota de potência'),
 ('power_weight_industry',0.3,'peso da capacidade industrial na nota de potência'),
 ('power_weight_army',0.3,'peso do exército em campo na nota de potência'),
 ('power_weight_tech',0.1,'peso do avanço tecnológico na nota de potência'),
 ('power_air_weight',4,'quanto vale um esquadrão aéreo em força de exército para a nota de potência'),
 ('power_nuke_weight',40,'quanto vale uma ogiva nuclear em força de exército para a nota de potência'),
 ('power_rank_days',5,'de quantos em quantos dias se refaz a tabela mundial de potências');
INSERT INTO rule VALUES ('rank_legend_score', 4000, 'pontuação a partir da qual a campanha é "Grande potência"');
INSERT INTO rule VALUES ('auto_advance_min_org', 40, 'organização mínima para o avanço automático atacar');

-- Níveis de dificuldade (World.ApplyDifficulty; menu de jogo)
CREATE TABLE IF NOT EXISTS difficulty (id TEXT PRIMARY KEY, name TEXT NOT NULL, sort INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS difficulty_effect (difficulty_id TEXT NOT NULL, rule_key TEXT NOT NULL, value REAL NOT NULL,
  PRIMARY KEY (difficulty_id, rule_key));
INSERT INTO difficulty VALUES ('muito_facil','Muito fácil',0),('facil','Fácil',1),('normal','Normal',2),('dificil','Difícil',3);
INSERT INTO difficulty_effect VALUES
 ('muito_facil','build_min_days',3),('muito_facil','new_division_org',70),('muito_facil','points_per_million',0.15),
 ('muito_facil','ai_general_reserve',600),('muito_facil','manpower_per_million_daily',90),
 ('facil','build_min_days',5),('facil','new_division_org',60),('facil','points_per_million',0.12),
 ('facil','ai_general_reserve',400),('facil','manpower_per_million_daily',75),
 ('normal','build_min_days',10),('normal','new_division_org',40),('normal','points_per_million',0.1),
 ('normal','ai_general_reserve',200),('normal','manpower_per_million_daily',60),
 ('dificil','build_min_days',15),('dificil','new_division_org',30),('dificil','points_per_million',0.08),
 ('dificil','ai_general_reserve',80),('dificil','manpower_per_million_daily',45);

-- Paz negociada (PeaceTerms): a pressão sobre o derrotado (fatia do país ocupada, diferença de
-- exércitos, desgaste de guerra, capital perdida) tem de pagar o preço do que se lhe exige. Uma
-- região já ocupada custa menos do que uma que ainda está nas mãos dele.
INSERT INTO rule (key,value,note) VALUES
 ('peace_weight_occupied',1.0,'peso da fatia do país que está ocupada'),
 ('peace_weight_strength',0.3,'peso da superioridade em divisões'),
 ('peace_weight_exhaustion',0.3,'peso do desgaste de guerra do derrotado'),
 ('peace_weight_capital',0.3,'peso de ter a capital dele ocupada'),
 ('peace_price_held',0.6,'preço de exigir uma região que já ocupas'),
 ('peace_price_free',1.4,'preço de exigir uma região que ainda é dele de facto'),
 ('peace_demand_greed',1.0,'multiplicador global do preço das exigências'),
 ('ai_peace_demand_min_share',0.25,'fatia do inimigo que a IA tem de ocupar para exigir território');

-- Desembarques: atravessar o mar desorganiza (naval_invasion_org_cost) e assaltar uma costa inimiga
-- exige organização (naval_invasion_min_org); quem bate da praia perde força (naval_invasion_penalty)
-- e só cabem naval_invasion_max_divs divisões por praia ao mesmo tempo.
INSERT INTO rule (key,value,note) VALUES
 ('naval_invasion_org_cost',25,'organização perdida ao desembarcar'),
 ('naval_invasion_min_org',45,'organização mínima para assaltar uma costa inimiga'),
 ('naval_invasion_penalty',0.45,'força do atacante que vem do mar'),
 ('naval_invasion_max_divs',3,'divisões a assaltar a mesma praia ao mesmo tempo');

-- Produção em série: a encomenda marcada volta ao fim da fila quando é entregue, até ao tecto da fila.
INSERT INTO rule (key,value,note) VALUES ('production_queue_max',30,'encomendas em fila por país');

-- Desembarques da IA: bater da praia é caro, por isso exige mais vantagem do que um ataque por terra
-- e reserva organização para a travessia.
INSERT INTO rule (key,value,note) VALUES
 ('ai_naval_ratio',3,'vantagem em divisões que a IA exige para assaltar uma praia defendida'),
 ('ai_naval_org_margin',20,'organização acima do mínimo legal que a IA guarda para a travessia');

-- Faixa de avisos (Alerts): limiares a partir dos quais o jogo levanta a mão sozinho.
INSERT INTO rule (key,value,note) VALUES
 ('alert_money_days',15,'dias de reserva no cofre abaixo dos quais se avisa que ele seca'),
 ('alert_supply',0.6,'abastecimento de uma divisão abaixo do qual ela conta como a beber areia'),
 ('alert_resistance',0.5,'resistência numa região ocupada a partir da qual se avisa que ferve'),
 ('alert_idle_money',150,'dinheiro no cofre a partir do qual a fila de produção vazia é desperdício');

-- Ranhuras de investigação (ResearchSystem): quantas linhas um país aguenta ao mesmo tempo.
INSERT INTO rule (key,value,note) VALUES
 ('research_slots',2,'linhas de investigação em paralelo por país, antes do stat research_slots');
