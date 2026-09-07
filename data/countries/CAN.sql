-- CAN (k=22) — Forças Armadas Canadianas, ordem de batalha aproximada 2024-2026.
-- Fontes: 1ª, 2ª e 5ª Canadian Mechanized Brigade Group (Edmonton/Petawawa/Valcartier), Canadian
-- Rangers (patrulhas de soberania no Árctico), frota de Leopard 2A4/2A6M CAN, LAV 6 (Coyote/Kodiak).
-- Carácter: exército pequeno e inteiramente profissional (sem conscrição), pouco numeroso mas bem
-- equipado, especializado em guerra em clima ártico e forte integração NATO/NORAD.

-- ===== unit_type próprios (100+20*22 .. 119+20*22 = 540..559) =====
INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (540,'Rangers Canadianos','ground',0.8,25,0.6,45),
 (541,'LAV 6 (Coyote)','ground',2.8,45,1.5,50),
 (542,'Leopard 2A6M CAN','ground',5.5,70,2.0,42);

INSERT INTO unit_stat VALUES
 (540,'soft_atk',5), (540,'hard_atk',1),  (540,'defense',20),(540,'breakthrough',6), (540,'armor',0), (540,'piercing',5), (540,'hardness',0.1), (540,'hp',18),
 (541,'soft_atk',11),(541,'hard_atk',5),  (541,'defense',28),(541,'breakthrough',18),(541,'armor',18),(541,'piercing',22),(541,'hardness',0.55),(541,'hp',32),
 (542,'soft_atk',13),(542,'hard_atk',17), (542,'defense',14),(542,'breakthrough',31),(542,'armor',68),(542,'piercing',60),(542,'hardness',0.92),(542,'hp',23);

INSERT INTO unit_tag VALUES
 (540,'infantry'),(540,'ground'),(540,'artico'),
 (541,'infantry'),(541,'armored'),(541,'ground'),
 (542,'armored'),(542,'ground');

-- ===== espíritos nacionais + modificadores (ids 540..559) =====
INSERT INTO national_spirit (id,country_tag,name,description) VALUES
 ('CAN_guerreiros_articos','CAN','Guerreiros do Árctico',
   'Os Canadian Rangers patrulham o território mais gelado do planeta há gerações: as tropas canadianas lutam e defendem-se muito melhor em tundra do que qualquer outro exército.'),
 ('CAN_exercito_profissional','CAN','Forças Pequenas e Profissionais',
   'Sem conscrição, o Exército assenta em voluntários altamente treinados: poucas divisões, mas cada uma aguenta melhor o combate.'),
 ('CAN_norad_nato','CAN','Parceiro NORAD/NATO',
   'Décadas de comando integrado com os EUA (NORAD) e a NATO tornam o comando canadiano particularmente eficiente em operações conjuntas.');

INSERT INTO modifier (id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id) VALUES
 (540,'spirit','terrain','tundra','str_attacker',NULL,'mul',1.25,'CAN','CAN_guerreiros_articos'),
 (541,'spirit','terrain','tundra','str_defender',NULL,'mul',1.25,'CAN','CAN_guerreiros_articos'),
 (542,'spirit',NULL,NULL,'str_attacker','artico','mul',1.20,'CAN','CAN_guerreiros_articos'),
 (543,'spirit',NULL,NULL,'str_defender',NULL,      'mul',1.10,'CAN','CAN_exercito_profissional'),
 (544,'spirit',NULL,NULL,'command',     NULL,      'mul',1.10,'CAN','CAN_norad_nato');

-- ===== stats e info do país =====
INSERT OR REPLACE INTO country_stat (country_tag,key,value) VALUES
 ('CAN','production_speed',1.05),
 ('CAN','org_regain',1.15),
 ('CAN','start_army_mult',0.5);

INSERT INTO country_info (country_tag,government,leader,doctrine,alliance,description) VALUES
 ('CAN','Monarquia constitucional parlamentar','Primeiro-Ministro',
  'Defesa territorial ártica, forças mecanizadas ligeiras e interoperabilidade total com os EUA e a NATO.',
  'NATO',
  'O Canadá mantém um exército pequeno mas inteiramente profissional, assente em três grupos de combate mecanizados e nos Canadian Rangers, que garantem a soberania sobre o vasto e gelado território do Norte. A indústria de defesa é sólida mas a prioridade estratégica é a defesa continental, partilhada com os Estados Unidos através do NORAD e reforçada pela pertença à NATO.');

-- ===== templates próprios (ids 1+50*22 .. 50+50*22 = 1101..1150) =====
INSERT INTO country_template (id,country_tag,name) VALUES
 (1101,'CAN','Grupo de Combate Mecanizado'),
 (1102,'CAN','Patrulha Ártica de Rangers');

INSERT INTO country_template_unit (country_template_id,unit_type_id,qty) VALUES
 (1101,541,4),(1101,542,3),(1101,4,2),(1101,5,1),
 (1102,540,6),(1102,1,1);

-- ===== brigadas/regimentos reais nomeados (ids 1101..1150) =====
INSERT INTO country_unit (id,country_tag,name,template_name,region_name) VALUES
 (1103,'CAN','1st Canadian Mechanized Brigade Group','Grupo de Combate Mecanizado','Alberta'),
 (1104,'CAN','2nd Canadian Mechanized Brigade Group','Grupo de Combate Mecanizado','Ontario'),
 (1105,'CAN','5e Groupe-brigade mécanisé du Canada','Grupo de Combate Mecanizado','Québec'),
 (1106,'CAN','Patrulha Ártica de Rangers de Nunavut','Patrulha Ártica de Rangers','Nunavut'),
 (1107,'CAN','Patrulha Ártica de Rangers do Yukon','Patrulha Ártica de Rangers','Yukon'),
 (1108,'CAN','Regimento de Infantaria de Newfoundland','Infantaria','Newfoundland and Labrador'),
 (1109,'CAN','Regimento de Reconhecimento Costeiro','Blindada','British Columbia'),
 (1110,'CAN','Regimento de Infantaria da Nova Escócia','Infantaria','Nova Scotia');

-- ===== focos nacionais (FocusSystem) =====
INSERT INTO focus (id,country_tag,name,description,days,requires,sort) VALUES
 ('can_norad','CAN','Parceiro do NORAD','Vigilância aeroespacial partilhada com os EUA sobre o continente norte-americano.',35,NULL,1),
 ('can_artico','CAN','Soberania Ártica','Presença reforçada no Ártico: patrulhas, radares e infraestrutura no extremo norte.',42,NULL,2),
 ('can_rangers','CAN','Expansão dos Rangers Canadianos','Mais patrulhas de Rangers nas comunidades nórdicas e costeiras isoladas.',28,'can_artico',3),
 ('can_f35','CAN','Programa de Caças F-35','Substituição da frota de CF-18 por caças de quinta geração.',56,'can_norad',4),
 ('can_estaleiros','CAN','Estratégia Nacional de Construção Naval','Modernização dos estaleiros de Halifax e Vancouver para fragatas e quebra-gelos.',49,NULL,5),
 ('can_industria','CAN','Indústria de Defesa de Ontário e Québec','Contratos e investimento puxam pela cadeia industrial de defesa nacional.',42,'can_estaleiros',6),
 ('can_francofonia','CAN','Ponte com a Francofonia','Cooperação militar e diplomática reforçada com o Québec e parceiros francófonos.',35,'can_norad',7),
 ('can_reserva','CAN','Força de Reserva Nacional','Recrutamento alargado da Reserva das Forças Armadas Canadianas.',28,'can_rangers',8);
INSERT INTO focus_effect (focus_id,stat_key,value) VALUES
 ('can_norad','research_speed',1.08),
 ('can_artico','org_regain',1.08),
 ('can_rangers','conscription',1.10),
 ('can_f35','production_speed',1.10),
 ('can_f35','research_speed',1.06),
 ('can_estaleiros','industry',1.08),
 ('can_industria','industry',1.07),
 ('can_industria','production_speed',1.07),
 ('can_francofonia','research_speed',1.05),
 ('can_reserva','conscription',1.15);

-- Árvore de focos: ramos que se excluem e o topo que exige as duas raízes (focus_link/focus_rival).
INSERT INTO focus_rival (focus_id,rival_id) VALUES
 ('can_f35','can_francofonia');
INSERT INTO focus_link (focus_id,requires_id) VALUES
 ('can_reserva','can_norad');

-- ===== conselheiros próprios do gabinete civil (advisor.country_tag) =====
INSERT INTO advisor (id,country_tag,slot,name,icon,cost,note) VALUES
 ('CAN_adv_recursos','CAN','economia','Ministro dos Recursos','⛏',185,'Minas e florestas a render como fábricas.'),
 ('CAN_adv_artico','CAN','seguranca','Patrulheiro do Árctico','❄',170,'O gelo não o atrasa.');
INSERT INTO advisor_effect VALUES ('CAN_adv_recursos','industry',1.12);
INSERT INTO advisor_effect VALUES ('CAN_adv_recursos','export_price',1.08);
INSERT INTO advisor_effect VALUES ('CAN_adv_artico','move_speed',1.12);
INSERT INTO advisor_effect VALUES ('CAN_adv_artico','defense',1.05);

-- ===== escada de leis própria do país (law.country_tag / law_group.country_tag) =====
INSERT INTO law_group (id,name,icon,sort,country_tag) VALUES ('CAN_artico','Passagem do Ártico','❄',10,'CAN');
INSERT INTO law (id,grp,name,description,sort,is_default,country_tag) VALUES
 ('CAN_law_patrulha','CAN_artico','Patrulha simbólica','Uma bandeira no gelo e pouco mais.',0,1,'CAN'),
 ('CAN_law_quebragelos','CAN_artico','Rangers e quebra-gelos','O Norte ganha portos, pistas e gente fardada.',1,0,'CAN'),
 ('CAN_law_noroeste','CAN_artico','Domínio do Noroeste','A passagem é rota interna e cobra-se como tal.',2,0,'CAN');
INSERT INTO law_effect (law_id,stat_key,value) VALUES
 ('CAN_law_patrulha','research_speed',1.05),
 ('CAN_law_quebragelos','defense',1.08),
 ('CAN_law_quebragelos','industry',1.04),
 ('CAN_law_noroeste','industry',1.08),
 ('CAN_law_noroeste','export_share',1.1);

-- ===== comandantes de casa (general.country_tag) =====
INSERT INTO general (id,name,stat_key,mult,cost,country_tag,icon,note) VALUES
 ('CAN_gen_artico','Comandante do Ártico','defense',1.15,130,'CAN','❄','Guerra a quarenta abaixo de zero: sabe o que congela e o que dispara.'),
 ('CAN_gen_logistica_norte','Mestre da Rota do Norte','org_regain',1.14,125,'CAN','🛷','Abastece guarnições onde não há estrada, só gelo e pista curta.');

-- ===== escola nacional de guerra (army_doctrine_branch/army_doctrine.country_tag) =====
INSERT INTO army_doctrine_branch (id,name,icon,sort,country_tag) VALUES
 ('CAN_escola','Escola do Ártico','❄',10,'CAN');
INSERT INTO army_doctrine (id,branch,name,description,cost,requires,sort,country_tag) VALUES
 ('CAN_doc_frio','CAN_escola','Guerra a Quarenta Abaixo','A quarenta abaixo, o inimigo é o termómetro; quem o domina combate à vontade.',50,NULL,1,'CAN'),
 ('CAN_doc_rota_norte','CAN_escola','Rota do Norte','Abastecer guarnições sem estrada é a metade difícil da guerra polar.',110,'CAN_doc_frio',2,'CAN'),
 ('CAN_doc_aliado','CAN_escola','Contingente Aliado','Um exército pequeno vale muito quando encaixa no de toda a gente.',190,'CAN_doc_rota_norte',3,'CAN');
INSERT INTO army_doctrine_effect (doctrine_id,stat_key,value) VALUES
 ('CAN_doc_frio','defense',1.07),
 ('CAN_doc_rota_norte','org_regain',1.07),
 ('CAN_doc_rota_norte','move_speed',1.05),
 ('CAN_doc_aliado','attack',1.07),
 ('CAN_doc_aliado','org_regain',1.06);
