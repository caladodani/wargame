-- Números calibrados em combat_sim.py
INSERT INTO terrain VALUES ('plain','Planície','#c8d8a0'),('forest','Floresta','#4f7942'),('urban','Urbano','#888888'),('mountain','Montanha','#a08060'),('desert','Deserto','#e0c080'),('tundra','Tundra','#dfe8ee');

INSERT INTO unit_type (id,name,category,cost,build_days,supply,mobility) VALUES
 (1,'Infantaria','ground',1.0,30,1.0,25),
 (2,'Mecanizada','ground',2.5,45,1.6,45),
 (3,'Blindados','ground',4.0,60,2.2,40),
 (4,'Artilharia','support',1.8,40,1.4,25),
 (5,'AA/Anti-drone','support',1.5,40,1.1,25),
 (6,'Anti-tanque','support',1.6,40,1.2,25);

INSERT INTO unit_stat VALUES
 (1,'soft_atk',6),(1,'hard_atk',1),(1,'defense',24),(1,'breakthrough',8),(1,'armor',0),(1,'piercing',5),(1,'hardness',0.1),(1,'hp',25),
 (2,'soft_atk',10),(2,'hard_atk',4),(2,'defense',26),(2,'breakthrough',16),(2,'armor',15),(2,'piercing',20),(2,'hardness',0.5),(2,'hp',30),
 (3,'soft_atk',12),(3,'hard_atk',14),(3,'defense',12),(3,'breakthrough',28),(3,'armor',60),(3,'piercing',55),(3,'hardness',0.9),(3,'hp',20),
 (4,'soft_atk',20),(4,'hard_atk',2),(4,'defense',6),(4,'breakthrough',6),(4,'armor',0),(4,'piercing',8),(4,'hardness',0.2),(4,'hp',6),
 (5,'soft_atk',2),(5,'hard_atk',1),(5,'defense',10),(5,'breakthrough',2),(5,'armor',5),(5,'piercing',10),(5,'hardness',0.3),(5,'hp',8),(5,'air_deny',0.1),
 (6,'soft_atk',2),(6,'hard_atk',20),(6,'defense',8),(6,'breakthrough',2),(6,'armor',0),(6,'piercing',70),(6,'hardness',0.2),(6,'hp',6);

INSERT INTO unit_tag VALUES
 (1,'infantry'),(1,'ground'),(2,'infantry'),(2,'armored'),(2,'ground'),(3,'armored'),(3,'ground'),
 (4,'support'),(4,'ground'),(5,'support'),(5,'ground'),(6,'support'),(6,'ground');

INSERT INTO modifier (source_kind,condition_key,condition_value,stat_key,required_tag,op,value) VALUES
 ('terrain','terrain','forest',  'str_attacker',NULL,     'mul',0.8),
 ('terrain','terrain','urban',   'str_attacker',NULL,     'mul',0.6),
 ('terrain','terrain','mountain','str_attacker',NULL,     'mul',0.5),
 ('terrain','terrain','tundra',  'str_attacker',NULL,     'mul',0.7),
 ('terrain','terrain','desert',  'str_attacker','infantry','mul',0.9),
 ('terrain','terrain','urban',   'str_attacker','armored','mul',0.6),
 ('terrain','terrain','mountain','str_attacker','armored','mul',0.6),
 ('terrain','river',  'true',    'str_attacker',NULL,     'add',-0.3),
 ('terrain','terrain','urban',   'str_defender',NULL,     'mul',1.2),
 ('terrain','terrain','mountain','str_defender',NULL,     'mul',1.3),
 ('air',    'air_sup','own',     'str',         NULL,     'add',0.25),
 ('air',    'air_sup','enemy',   'str',         NULL,     'add',-0.25),
 ('tech',   'tech:drones_1','true','str',       NULL,     'add',0.10),
 ('cyber',  'cyber_hit','true',  'command',     NULL,     'mul',0.9);
