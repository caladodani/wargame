"""Verifica os ficheiros de características únicas por país (data/countries/*.sql).

Uso: python3 tools/check_countries.py [data/countries/PRT.sql ...]   (sem args: todos)
Sai com 1 se houver erros. Avisos não travam.

Cada ficheiro chama-se <TAG>.sql e só pode falar desse país (country_tag = TAG). Carrega-se
schema.sql + seed_units.sql + o ficheiro num SQLite em memória e confere-se referências, gamas
de ids e valores. Nomes de regiões conferem-se contra data/static.db se existir.
"""
import re, sqlite3, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
STAT_KEYS = {'industry', 'production_speed', 'org_regain', 'start_army_mult', 'research_speed', 'move_speed', 'aggression'}
MOD_STATS = {'str', 'str_attacker', 'str_defender', 'command'}
UNIT_STATS = {'soft_atk', 'hard_atk', 'defense', 'breakthrough', 'armor', 'piercing', 'hardness', 'hp'}
COND_KEYS = {'terrain', 'river', 'country'}
# comandantes: cada arma comanda o que é do ofício dela — um almirante não melhora a marcha da infantaria
GENERAL_STATS = {'exercito': {'attack', 'defense', 'org_regain', 'move_speed', 'industry'},
                 'ar': {'air_losses', 'air_bombing', 'air_upkeep'},
                 'mar': {'naval_losses', 'naval_upkeep', 'naval_blockade', 'naval_escort', 'naval_patrol'}}
# o que cada arma pode melhorar: uma escola de caça não dá recrutamento e uma de infantaria não dá bloqueio
DOCTRINE_STATS = {'exercito': {'attack', 'conscription', 'defense', 'industry', 'move_speed', 'org_regain', 'production_speed'},
                  'ar': {'air_losses', 'air_bombing', 'air_upkeep'},
                  'mar': {'naval_losses', 'naval_upkeep', 'naval_blockade', 'naval_escort', 'naval_patrol'}}
# chaves em que o número é uma conta a pagar (perdas, sustento): melhorar é descer, e um valor acima de 1
# seria uma escola que piora quem a aprende — quase sempre um sinal trocado
LOWER_IS_BETTER = {'air_losses', 'air_upkeep', 'naval_losses', 'naval_upkeep'}
# ramos da árvore de investigação que pertencem a uma arma: é lá que vive o programa nacional de cada país
TECH_BRANCHES = {'Aviação': 'ar', 'Marinha': 'mar'}
LAW_STATS = {'attack', 'conscription', 'counter_intel', 'defense', 'export_price', 'export_share', 'industry',
             'integration_speed', 'occupied_yield', 'org_regain', 'production_speed', 'research_speed',
             'resistance_growth'}


def norm(s):
    import unicodedata
    return ''.join(ch for ch in unicodedata.normalize('NFD', s or '') if unicodedata.category(ch) != 'Mn').lower().strip()


def check(path, static):
    tag = path.stem
    errs, warns = [], []
    db = sqlite3.connect(':memory:')
    db.executescript((HERE / 'data' / 'schema.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_units.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_tech.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_world.sql').read_text(encoding='utf-8'))
    base_units = {r[0] for r in db.execute('SELECT id FROM unit_type')}
    base_mods = {r[0] for r in db.execute('SELECT id FROM modifier')}
    base_tags = {r[0] for r in db.execute('SELECT DISTINCT tag FROM unit_tag')}
    terrains = {r[0] for r in db.execute('SELECT id FROM terrain')}
    if static:
        db.execute('ATTACH DATABASE ? AS st', (str(static),))
        if not db.execute('SELECT 1 FROM st.country WHERE tag=?', (tag,)).fetchone():
            errs.append(f'tag {tag} não existe na tabela country')
    TAG_TABLES = ('country_stat', 'country_info', 'national_spirit', 'country_template', 'country_unit', 'modifier',
                  'advisor', 'law', 'law_group', 'general', 'army_doctrine', 'army_doctrine_branch', 'tech',
                  'general_rank')
    base_rows = {t: set(db.execute(f'SELECT * FROM {t}').fetchall()) for t in TAG_TABLES}
    try:
        db.executescript(path.read_text(encoding='utf-8'))
    except sqlite3.Error as e:
        return [f'SQL inválido: {e}'], warns

    # só fala do próprio país (as linhas que já vinham dos seeds não contam)
    for table in TAG_TABLES:
        cur = db.execute(f'SELECT * FROM {table}')
        ci = [d[0] for d in cur.description].index('country_tag')
        for row in set(cur.fetchall()) - base_rows[table]:
            if row[ci] is not None and row[ci] != tag: errs.append(f'{table}: country_tag {row[ci]} num ficheiro de {tag}')
    src = path.read_text(encoding='utf-8')
    for m in re.finditer(r"UPDATE\s+country\s+SET[^;]*WHERE\s+tag\s*=\s*'(\w+)'", src, re.I):
        if m.group(1) != tag: errs.append(f'UPDATE country de {m.group(1)} num ficheiro de {tag}')
    for m in re.finditer(r"UPDATE\s+region\s+SET[^;]*;", src, re.I):
        if f"tag='{tag}'" not in m.group(0).replace('"', "'").replace(' ', ''):
            errs.append("UPDATE region sem WHERE owner_id=(SELECT id FROM country WHERE tag='%s')" % tag)
    for m in re.finditer(r"\b(DELETE|DROP|ALTER|INSERT\s+INTO\s+(region|neighbour|country\b|rule|terrain))", src, re.I):
        errs.append(f'instrução proibida: {m.group(0)}')

    # unidades novas
    new_units = {r[0] for r in db.execute('SELECT id FROM unit_type')} - base_units
    if len({r[0] for r in db.execute('SELECT id FROM unit_type')} & base_units) != len(base_units):
        errs.append('unit_type base (1-6) alterado')
    for uid in new_units:
        if uid < 100: errs.append(f'unit_type {uid}: ids próprios ≥ 100')
        stats = {r[0]: r[1] for r in db.execute('SELECT stat_key,value FROM unit_stat WHERE unit_type_id=?', (uid,))}
        missing = UNIT_STATS - set(stats)
        if missing: errs.append(f'unit_type {uid}: faltam stats {sorted(missing)}')
        if not (0 <= stats.get('hardness', 0) <= 1): errs.append(f'unit_type {uid}: hardness fora de 0..1')
        if not db.execute('SELECT 1 FROM unit_tag WHERE unit_type_id=?', (uid,)).fetchone(): warns.append(f'unit_type {uid}: sem tags (ground/infantry/armored/support)')
        cost, mob = db.execute('SELECT cost,mobility FROM unit_type WHERE id=?', (uid,)).fetchone()
        if not (0.5 <= cost <= 8): warns.append(f'unit_type {uid}: cost {cost} fora do habitual 0.5..8')
        if not (15 <= mob <= 60): warns.append(f'unit_type {uid}: mobility {mob} fora de 15..60')

    # modificadores / espíritos
    spirits = {r[0]: r[1] for r in db.execute('SELECT id,country_tag FROM national_spirit')}
    for sid in spirits:
        if not sid.startswith(tag + '_'): errs.append(f'national_spirit {sid}: id deve começar por {tag}_')
        if not db.execute('SELECT 1 FROM modifier WHERE spirit_id=?', (sid,)).fetchone(): warns.append(f'espírito {sid} sem efeitos (modifier.spirit_id)')
    for mid, sk, ck, cv, stat, rtag, op, val, ctag, sid in db.execute(
            'SELECT id,source_kind,condition_key,condition_value,stat_key,required_tag,op,value,country_tag,spirit_id FROM modifier WHERE id NOT IN (%s)' % ','.join(map(str, base_mods))):
        if mid < 100: errs.append(f'modifier {mid}: ids próprios ≥ 100')
        if ctag != tag: errs.append(f'modifier {mid}: country_tag tem de ser {tag}')
        if stat not in MOD_STATS: errs.append(f'modifier {mid}: stat_key {stat} desconhecido (usa {sorted(MOD_STATS)})')
        if ck is not None and ck not in COND_KEYS and not ck.startswith('tech:'): errs.append(f'modifier {mid}: condition_key {ck} desconhecido')
        if ck == 'terrain' and cv not in terrains: errs.append(f'modifier {mid}: terreno {cv} desconhecido')
        if rtag is not None and rtag not in base_tags | {r[0] for r in db.execute('SELECT DISTINCT tag FROM unit_tag')}: errs.append(f'modifier {mid}: required_tag {rtag} desconhecida')
        if op == 'mul' and not (0.5 <= val <= 1.6): warns.append(f'modifier {mid}: mul {val} fora de 0.5..1.6')
        if op == 'add' and not (-0.5 <= val <= 0.5): warns.append(f'modifier {mid}: add {val} fora de -0.5..0.5')
        if sid is not None and sid not in spirits: errs.append(f'modifier {mid}: spirit_id {sid} não existe')

    # árvore de focos: as ligações só podem falar de focos deste país
    own = {r[0] for r in db.execute('SELECT id FROM focus WHERE country_tag=?', (tag,))}
    for table, cols in (('focus_link', ('focus_id', 'requires_id')), ('focus_rival', ('focus_id', 'rival_id'))):
        for a, b in db.execute(f'SELECT {cols[0]},{cols[1]} FROM {table}'):
            if a not in own and b not in own: continue        # linha de outro país (não veio deste ficheiro)
            if a not in own or b not in own: errs.append(f'{table}: {a} → {b} mistura focos de outro país')
            if a == b: errs.append(f'{table}: {a} aponta para si próprio')

    # leis próprias: escada com grupo do país, ids prefixados, um degrau de arranque e efeitos conhecidos
    own_groups = {r[0] for r in db.execute('SELECT id FROM law_group WHERE country_tag=?', (tag,))}
    for gid in own_groups:
        if not gid.startswith(tag + '_'): errs.append(f'law_group {gid}: id deve começar por {tag}_')
        steps = db.execute('SELECT id,is_default,country_tag FROM law WHERE grp=?', (gid,)).fetchall()
        if len(steps) < 2: errs.append(f'law_group {gid}: {len(steps)} degrau(s) — uma escada precisa de dois ou mais')
        if sum(d for _, d, _ in steps) != 1: errs.append(f'law_group {gid}: tem de haver exactamente um is_default')
        for lid, _, ctag in steps:
            if ctag != tag: errs.append(f'law {lid}: está no grupo {gid} de {tag} mas country_tag={ctag}')
    for lid, grp in db.execute('SELECT id,grp FROM law WHERE country_tag=?', (tag,)):
        if not lid.startswith(tag + '_'): errs.append(f'law {lid}: id deve começar por {tag}_')
        if grp not in own_groups: errs.append(f'law {lid}: grupo {grp} não é uma escada de {tag} (lei própria em grupo comum)')
        effs = db.execute('SELECT stat_key,value FROM law_effect WHERE law_id=?', (lid,)).fetchall()
        if not effs: warns.append(f'law {lid}: sem efeitos (law_effect)')
        for k, v in effs:
            if k not in LAW_STATS: errs.append(f'law {lid}: stat_key {k} desconhecido (usa {sorted(LAW_STATS)})')
            if not (0.5 <= v <= 1.6): warns.append(f'law {lid}: {k}={v} fora de 0.5..1.6')

    # a experiência que o comandante comum mais caro de cada arma pede: o de casa tem de valer mais do que
    # ele, senão o país nunca teria motivo para chamar o seu
    merc_xp = {d: x for d, x in db.execute(
        'SELECT domain, MAX(xp) FROM general WHERE country_tag IS NULL GROUP BY domain')}
    # comandantes de casa: id prefixado, stat que o motor conheça e força dentro da gama dos mercenários
    by_arm, icons = {}, {}
    for gid, gname, gstat, gmult, gcost, gicon, gdom, gxp in db.execute(
            'SELECT id,name,stat_key,mult,cost,icon,domain,xp FROM general WHERE country_tag=?', (tag,)):
        if not gid.startswith(tag + '_'): errs.append(f'general {gid}: id deve começar por {tag}_')
        by_arm.setdefault(gdom, []).append(gid)
        # duas chapas iguais no mesmo estado-maior e os dois retratos passam a ser o mesmo homem
        if gicon and gicon in icons:
            errs.append(f'general {gid}: chapa {gicon} já é a de {icons[gicon]} — o retrato tem de ser dele')
        icons[gicon] = gid
        if gdom != 'exercito' and gxp <= merc_xp.get(gdom, 0):
            errs.append(f'general {gid}: xp {gxp} não passa a do comandante comum de {gdom} '
                        f'({merc_xp.get(gdom, 0)}) — o de casa tem de ser mais exigente')
        if gdom not in GENERAL_STATS:
            errs.append(f'general {gid}: arma {gdom} desconhecida (usa {sorted(GENERAL_STATS)})')
        elif gstat not in GENERAL_STATS[gdom]:
            errs.append(f'general {gid}: stat_key {gstat} não é da arma {gdom} (usa {sorted(GENERAL_STATS[gdom])})')
        if gstat in LOWER_IS_BETTER:
            if gmult >= 1: errs.append(f'general {gid}: {gstat}={gmult} — aqui menos é melhor, o valor tem de ser < 1')
            if not (0.85 <= gmult): warns.append(f'general {gid}: mult {gmult} abaixo de 0.85')
        elif not (1.05 <= gmult <= 1.20): warns.append(f'general {gid}: mult {gmult} fora de 1.05..1.20')
        if gdom != 'exercito' and gxp <= 0:
            warns.append(f'general {gid}: comandante de {gdom} sem experiência a pagar (general.xp)')
        if not (100 <= gcost <= 160): warns.append(f"general {gid}: custo {gcost} fora de 100..160")
        if not gicon: warns.append(f'general {gid}: sem chapa (o retrato do estado-maior fica vazio)')

    # um comandante de casa por arma: dois na mesma arma disputavam a mesma cadeira e o segundo nunca era
    # chamado; nenhum numa arma é um país sem cara própria nessa arma
    for domain in GENERAL_STATS:
        men = by_arm.get(domain, [])
        if domain == 'exercito':
            if not men: warns.append('sem comandantes de casa do exército')
        elif len(men) > 1:
            errs.append(f'{len(men)} comandantes de casa de {domain} ({", ".join(sorted(men))}) — só cabe um')
        elif not men:
            warns.append(f'sem comandante de casa de {domain}')

    # escola nacional de guerra: ramo do país, degraus encadeados, preço a subir e efeitos conhecidos
    own_branches = {r[0]: r[1] for r in db.execute('SELECT id,domain FROM army_doctrine_branch WHERE country_tag=?', (tag,))}
    by_domain = {}
    for bid, domain in own_branches.items():
        if not bid.startswith(tag + '_'): errs.append(f'army_doctrine_branch {bid}: id deve começar por {tag}_')
        if domain not in DOCTRINE_STATS: errs.append(f'army_doctrine_branch {bid}: arma {domain} desconhecida (usa {sorted(DOCTRINE_STATS)})')
        by_domain.setdefault(domain, []).append(bid)
        steps = db.execute('SELECT id,requires,cost,sort,country_tag FROM army_doctrine WHERE branch=? ORDER BY sort',
                           (bid,)).fetchall()
        if len(steps) < 2: errs.append(f'army_doctrine_branch {bid}: {len(steps)} degrau(s) — uma escola precisa de dois ou mais')
        prev = None
        for did, req, cost, _, ctag in steps:
            if ctag != tag: errs.append(f'army_doctrine {did}: está na escola {bid} de {tag} mas country_tag={ctag}')
            if req != prev: errs.append(f'army_doctrine {did}: requires {req} quebra a corrente (esperado {prev})')
            prev = did
        costs = [c for _, _, c, _, _ in steps]
        if costs != sorted(costs) or len(set(costs)) != len(costs):
            errs.append(f'army_doctrine_branch {bid}: preços {costs} não sobem degrau a degrau')
    # uma escola de casa por arma: duas escolas próprias da mesma arma fechavam-se uma à outra e a segunda
    # nunca seria aprendida; nenhuma numa arma é só um país sem maneira própria de a fazer
    for domain, bids in by_domain.items():
        if len(bids) > 1: errs.append(f'{domain}: {len(bids)} escolas de casa ({", ".join(sorted(bids))}) — só pode haver uma por arma')
    for domain in DOCTRINE_STATS:
        if domain not in by_domain: warns.append(f'sem escola de casa da arma {domain}')
    for did, branch in db.execute('SELECT id,branch FROM army_doctrine WHERE country_tag=?', (tag,)):
        if not did.startswith(tag + '_'): errs.append(f'army_doctrine {did}: id deve começar por {tag}_')
        if branch not in own_branches: errs.append(f'army_doctrine {did}: escola {branch} não é de {tag} (degrau próprio em ramo comum)')
        effs = db.execute('SELECT stat_key,value FROM army_doctrine_effect WHERE doctrine_id=?', (did,)).fetchall()
        if not effs: warns.append(f'army_doctrine {did}: sem efeitos (army_doctrine_effect)')
        domain = own_branches.get(branch, 'exercito')
        allowed = DOCTRINE_STATS.get(domain, set())
        for k, v in effs:
            if k not in allowed: errs.append(f'army_doctrine {did}: stat_key {k} não é da arma {domain} (usa {sorted(allowed)})')
            lo = 0.85 if k in LOWER_IS_BETTER else 0.9   # cortar 15% de perdas é uma escola forte, não um erro
            if not (lo <= v <= 1.2): warns.append(f'army_doctrine {did}: {k}={v} fora de {lo}..1.2')
            if k in LOWER_IS_BETTER and v >= 1: errs.append(f'army_doctrine {did}: {k}={v} — aqui menos é melhor, o valor tem de ser < 1')
            if k not in LOWER_IS_BETTER and domain != 'exercito' and v <= 1:
                errs.append(f'army_doctrine {did}: {k}={v} — aqui mais é melhor, o valor tem de ser > 1')

    # conselheiros próprios: id prefixado e uma pasta que exista
    slots = {r[0] for r in db.execute('SELECT id FROM cabinet_slot')}
    for aid, slot in db.execute('SELECT id,slot FROM advisor WHERE country_tag=?', (tag,)):
        if not aid.startswith(tag + '_'): errs.append(f'advisor {aid}: id deve começar por {tag}_')
        if slot not in slots: errs.append(f'advisor {aid}: pasta {slot} não existe (cabinet_slot)')

    # stats de país
    for k, v in db.execute('SELECT key,value FROM country_stat WHERE country_tag=?', (tag,)):
        if k not in STAT_KEYS: errs.append(f'country_stat {k}: chave desconhecida (usa {sorted(STAT_KEYS)})')
        if not (0.2 <= v <= 3): warns.append(f'country_stat {k}={v} fora de 0.2..3')
    if not db.execute('SELECT 1 FROM country_info WHERE country_tag=?', (tag,)).fetchone(): warns.append('sem country_info')

    # templates e unidades nomeadas
    tnames = {norm(n) for n in ('Infantaria', 'Blindada', 'Infantaria AT', 'Mecanizada')}
    for ctid, name in db.execute('SELECT id,name FROM country_template WHERE country_tag=?', (tag,)):
        tnames.add(norm(name))
        units = db.execute('SELECT unit_type_id,qty FROM country_template_unit WHERE country_template_id=?', (ctid,)).fetchall()
        if not units: errs.append(f'country_template {name}: sem unidades')
        allu = base_units | new_units
        for u, q in units:
            if u not in allu: errs.append(f'country_template {name}: unit_type {u} não existe')
            if not (1 <= q <= 12): warns.append(f'country_template {name}: qty {q} estranha')
        if sum(q for _, q in units) > 12: warns.append(f'country_template {name}: {sum(q for _, q in units)} batalhões (>12, HoI4 ~ 8-10)')
    region_names = set()
    if static and not errs:
        cid = db.execute('SELECT id FROM st.country WHERE tag=?', (tag,)).fetchone()
        if cid: region_names = {norm(r[0]) for r in db.execute('SELECT name FROM st.region WHERE owner_id=?', (cid[0],))}
    # programas nacionais de aviação e de marinha (tech.country_tag): ramo da arma, apanhados a um degrau
    # comum, mais caros do que qualquer degrau comum do ramo e com um efeito que vale mais do que o comum
    # — um programa nacional que fosse mais barato ou mais fraco não seria investigado por ninguém
    own_techs = db.execute('SELECT id,branch,cost,requires,description FROM tech WHERE country_tag=?', (tag,)).fetchall()
    tech_by_branch = {}
    for tid, branch, cost, req, desc in own_techs:
        if not tid.startswith(tag + '_'): errs.append(f'tech {tid}: id deve começar por {tag}_')
        tech_by_branch.setdefault(branch, []).append(tid)
        arm = TECH_BRANCHES.get(branch)
        if arm is None:
            errs.append(f'tech {tid}: ramo {branch} não é de nenhuma arma (usa {sorted(TECH_BRANCHES)})')
        if req is None:
            errs.append(f'tech {tid}: sem requires — um programa nacional apanha um degrau comum da arma')
        else:
            owner = db.execute('SELECT country_tag FROM tech WHERE id=?', (req,)).fetchone()
            if owner is None: errs.append(f'tech {tid}: requires {req} não existe')
            elif owner[0] is not None: errs.append(f'tech {tid}: requires {req} é programa de {owner[0]} — ninguém o teria feito')
        top = db.execute('SELECT MAX(cost) FROM tech WHERE branch=? AND country_tag IS NULL', (branch,)).fetchone()[0]
        if top is not None and cost <= top:
            errs.append(f'tech {tid}: custa {cost} e o degrau comum mais caro de {branch} custa {top} — o programa de casa é o topo do ramo')
        if not desc: warns.append(f'tech {tid}: sem descrição (a ficha da investigação fica só com o nome)')
        effs = db.execute('SELECT stat_key,value FROM tech_effect WHERE tech_id=?', (tid,)).fetchall()
        if not effs: errs.append(f'tech {tid}: sem efeito (tech_effect) — um programa que não muda nada')
        allowed = GENERAL_STATS.get(arm, set())
        for k, v in effs:
            if arm and k not in allowed:
                errs.append(f'tech {tid}: stat_key {k} não é da arma {arm} (usa {sorted(allowed)})')
            if k in LOWER_IS_BETTER and v >= 1:
                errs.append(f'tech {tid}: {k}={v} — aqui menos é melhor, o valor tem de ser < 1')
            if k not in LOWER_IS_BETTER and v <= 1:
                errs.append(f'tech {tid}: {k}={v} — aqui mais é melhor, o valor tem de ser > 1')
            common = [r[0] for r in db.execute(
                'SELECT e.value FROM tech_effect e JOIN tech t ON t.id=e.tech_id'
                ' WHERE e.stat_key=? AND t.country_tag IS NULL', (k,))]
            if common:
                best = min(common) if k in LOWER_IS_BETTER else max(common)
                if (v >= best) if k in LOWER_IS_BETTER else (v <= best):
                    errs.append(f'tech {tid}: {k}={v} não vale mais do que o degrau comum ({best})')
    for branch, tids in tech_by_branch.items():
        if len(tids) > 1:
            errs.append(f'{branch}: {len(tids)} programas de casa ({", ".join(sorted(tids))}) — só cabe um por arma')
    for branch in TECH_BRANCHES:
        if branch not in tech_by_branch: warns.append(f'sem programa nacional de {branch}')

    # escada de postos nacional (general_rank.country_tag): três armas, os mesmos degraus da escada
    # comum e nomes próprios. Os limiares e os bónus TÊM de ser os comuns: uma escada nacional muda o
    # nome do posto, não o que ele vale — senão um país sobe mais depressa por ter melhores palavras.
    own_ranks = db.execute('SELECT domain,level,name,xp,bonus FROM general_rank WHERE country_tag=?'
                           ' ORDER BY domain,level', (tag,)).fetchall()
    ranks_by_arm = {}
    for domain, level, name, xp, bonus in own_ranks:
        ranks_by_arm.setdefault(domain, []).append((level, name, xp, bonus))
        if domain not in GENERAL_STATS:
            errs.append(f'posto {name}: arma {domain} não existe (só {", ".join(sorted(GENERAL_STATS))})')
    if own_ranks:
        for domain in GENERAL_STATS:
            steps = ranks_by_arm.get(domain)
            common = db.execute('SELECT level,name,xp,bonus FROM general_rank'
                                ' WHERE domain=? AND country_tag IS NULL ORDER BY level', (domain,)).fetchall()
            if not steps:
                errs.append(f'escada nacional sem a arma {domain} — quem trouxer uma escada tem de trazer as três')
                continue
            if len(steps) != len(common):
                errs.append(f'escada de {domain}: {len(steps)} degraus e a comum tem {len(common)}')
                continue
            names = [s[1] for s in steps]
            for name in sorted({n for n in names if names.count(n) > 1}):
                errs.append(f'escada de {domain}: posto {name} repetido')
            for (lvl, name, xp, bonus), (clvl, cname, cxp, cbonus) in zip(steps, common):
                if lvl != clvl:
                    errs.append(f'escada de {domain}: nível {lvl} onde a comum tem {clvl}')
                if (xp, bonus) != (cxp, cbonus):
                    errs.append(f'posto {name}: pede {xp}/vale {bonus} e o degrau comum ({cname}) pede {cxp}/vale {cbonus}'
                                ' — a escada nacional muda o nome, não o equilíbrio')
            if names == [c[1] for c in common]:
                errs.append(f'escada de {domain}: nomes iguais aos da comum — uma escada nacional sem nada de nacional')
    else:
        warns.append('sem escada de postos nacional (os comandantes sobem pela comum)')

    n_units = 0
    for name, tname, rname in db.execute('SELECT name,template_name,region_name FROM country_unit WHERE country_tag=?', (tag,)):
        n_units += 1
        if norm(tname) not in tnames: errs.append(f'country_unit "{name}": template "{tname}" não existe')
        if rname and region_names and norm(rname) not in region_names: warns.append(f'country_unit "{name}": região "{rname}" não existe (vai para a capital)')
    n_laws = db.execute('SELECT COUNT(*) FROM law WHERE country_tag=?', (tag,)).fetchone()[0]
    n_adv = db.execute('SELECT COUNT(*) FROM advisor WHERE country_tag=?', (tag,)).fetchone()[0]
    n_gen = db.execute('SELECT COUNT(*) FROM general WHERE country_tag=?', (tag,)).fetchone()[0]
    n_doc = db.execute('SELECT COUNT(*) FROM army_doctrine WHERE country_tag=?', (tag,)).fetchone()[0]
    arms = '+'.join(f'{d}:{len(by_domain.get(d, []))}' for d in ('exercito', 'ar', 'mar'))
    gen_arms = '+'.join(f'{d}:{len(by_arm.get(d, []))}' for d in ('exercito', 'ar', 'mar'))
    tech_arms = '+'.join(f'{TECH_BRANCHES[b]}:{len(tech_by_branch.get(b, []))}' for b in sorted(TECH_BRANCHES))
    rank_arms = '+'.join(f'{d}:{len(ranks_by_arm.get(d, []))}' for d in ('exercito', 'ar', 'mar'))
    summary = (f'{tag}: {len(own_ranks)} postos próprios [{rank_arms}], {len(own_techs)} programas nacionais [{tech_arms}], '
               f'{len(own_groups)} escadas de leis ({n_laws} leis), {n_adv} conselheiros, {n_gen} comandantes [{gen_arms}], '
               f'{len(own_branches)} escolas de guerra [{arms}] ({n_doc} degraus), {len(spirits)} espíritos, {db.execute("SELECT COUNT(*) FROM modifier WHERE country_tag=?", (tag,)).fetchone()[0]} efeitos, '
               f'{len(new_units)} unidades próprias, {db.execute("SELECT COUNT(*) FROM country_template WHERE country_tag=?", (tag,)).fetchone()[0]} templates próprios, '
               f'{n_units} brigadas nomeadas, stats {dict(db.execute("SELECT key,value FROM country_stat WHERE country_tag=?", (tag,)).fetchall())}')
    print(summary)
    return errs, warns


def check_ranks(db, static):
    """Escadas de postos (general_rank): uma por arma, e a mesma no espelho.

    Cada arma tem a sua carreira — um brigadeiro não é um contra-almirante — e o World.RankOf só olha
    para a escada da arma do comandante: uma arma sem escada é uma arma onde ninguém sobe de posto e a
    UI mostra o nome vazio. Os degraus têm de crescer nos dois números (o que pedem e o que valem),
    senão há um posto que se ganha sem ser preciso nada ou que vale menos do que o anterior.
    """
    errs, warns = [], []
    rows = db.execute('SELECT domain,level,name,xp,bonus FROM general_rank'
                      ' WHERE country_tag IS NULL ORDER BY domain,level').fetchall()
    by_arm = {}
    for domain, level, name, xp, bonus in rows:
        by_arm.setdefault(domain, []).append((level, name, xp, bonus))
        if domain not in GENERAL_STATS:
            errs.append(f'posto {name}: arma {domain} não existe (só {", ".join(sorted(GENERAL_STATS))})')
    names = [r[2] for r in rows]
    for name in sorted({n for n in names if names.count(n) > 1}):
        errs.append(f'posto {name} repetido: dois postos com o mesmo nome são o mesmo posto')
    for domain in GENERAL_STATS:
        steps = by_arm.get(domain)
        if not steps:
            errs.append(f'sem escada de postos de {domain} — os comandantes dessa arma nunca sobem')
            continue
        if [s[0] for s in steps] != list(range(1, len(steps) + 1)):
            errs.append(f'escada de {domain}: níveis {[s[0] for s in steps]} não são 1..{len(steps)} seguidos')
        if steps[0][2] != 0 or steps[0][3] != 0:
            errs.append(f'escada de {domain}: o primeiro posto ({steps[0][1]}) tem de ser 0 de experiência e 0 de bónus')
        for a, b in zip(steps, steps[1:]):
            if b[2] <= a[2] or b[3] <= a[3]:
                errs.append(f'escada de {domain}: {b[1]} não pede nem vale mais do que {a[1]}')
    if static:
        st = sqlite3.connect(static)
        mirror = st.execute('SELECT domain,level,name,xp,bonus FROM general_rank'
                            ' WHERE country_tag IS NULL ORDER BY domain,level').fetchall()
        if mirror != rows:
            errs.append('data/static.db tem outra escada comum de postos — falta o espelho manual do seed_world.sql')
        nat = st.execute('SELECT domain,level,name,xp,bonus,country_tag FROM general_rank'
                         ' WHERE country_tag IS NOT NULL ORDER BY country_tag,domain,level').fetchall()
        seed = db.execute('SELECT domain,level,name,xp,bonus,country_tag FROM general_rank'
                          ' WHERE country_tag IS NOT NULL ORDER BY country_tag,domain,level').fetchall()
        if nat != seed:
            errs.append(f'data/static.db tem {len(nat)} postos nacionais e os ficheiros dos países têm {len(seed)}'
                        ' — falta o espelho manual')
    arms = ' '.join(f'{d}:{len(by_arm.get(d, []))}' for d in ('exercito', 'ar', 'mar'))
    nations = db.execute('SELECT COUNT(DISTINCT country_tag) FROM general_rank WHERE country_tag IS NOT NULL').fetchone()[0]
    print(f'postos de comandante [{arms}] + escadas próprias de {nations} países')
    return errs, warns


def check_wounds(db, static):
    """Gravidades de baixa (wound_kind): as armas todas têm por onde cair, e o espelho está em dia.

    O CommandCasualtySystem sorteia a gravidade pelos pesos da tabela, mas só entre as que servem a arma
    do comandante (domain NULL = todas; com arma é só dessa). Uma arma sem gravidade nenhuma é uma arma
    onde o comando nunca cai — o sorteio devolve null e o combate não fere ninguém. E uma gravidade com
    arma que não existe é peso morto que nunca sai a ninguém.
    """
    errs, warns = [], []
    rows = db.execute('SELECT id,name,icon,days,weight,fatal,domain FROM wound_kind ORDER BY id').fetchall()
    if not rows:
        return ['sem gravidades de baixa: o comando nunca cai'], warns

    icons = [r[2] for r in rows]
    for wid, name, icon, days, weight, fatal, domain in rows:
        if domain is not None and domain not in GENERAL_STATS:
            errs.append(f'gravidade {wid}: arma {domain} não existe (só {", ".join(sorted(GENERAL_STATS))})')
        if weight <= 0:
            errs.append(f'gravidade {wid}: peso {weight} — nunca sai no sorteio')
        if fatal and days:
            errs.append(f'gravidade {wid}: é fatal e ainda assim marca {days} dias de hospital')
        if not fatal and days <= 0:
            errs.append(f'gravidade {wid}: não é fatal e não tira o homem de serviço nem um dia')
        if icons.count(icon) > 1:
            errs.append(f'gravidade {wid}: chapa {icon} repetida — na enfermaria não se distinguem')

    for domain in GENERAL_STATS:
        mine = [r for r in rows if r[6] is None or r[6] == domain]
        if not any(r[4] > 0 for r in mine):
            errs.append(f'sem gravidades de baixa para {domain} — o comando dessa arma nunca cai')
        if not any(r[5] for r in mine):
            warns.append(f'em {domain} não há gravidade fatal: o comando dessa arma nunca se perde de vez')
        rule = {'ar': 'wound_chance_air', 'mar': 'wound_chance_sea'}.get(domain, 'wound_chance')
        got = db.execute('SELECT value FROM rule WHERE key=?', (rule,)).fetchone()
        if not got:
            errs.append(f'falta a regra {rule} — World.WoundChanceRule({domain}) não encontra a probabilidade')
        elif got[0] <= 0:
            warns.append(f'{rule} está a {got[0]}: o comando de {domain} não corre risco nenhum')

    if static:
        st = sqlite3.connect(static)
        mirror = st.execute('SELECT id,name,icon,days,weight,fatal,domain FROM wound_kind ORDER BY id').fetchall()
        if mirror != rows:
            errs.append('data/static.db tem outras gravidades de baixa — falta o espelho manual do seed_world.sql')
        for rule in ('wound_chance', 'wound_chance_air', 'wound_chance_sea'):
            a = db.execute('SELECT value FROM rule WHERE key=?', (rule,)).fetchone()
            b = st.execute('SELECT value FROM rule WHERE key=?', (rule,)).fetchone()
            if a != b:
                errs.append(f'data/static.db tem outro {rule} ({b} contra {a}) — falta o espelho manual')

    arms = ' '.join(f'{d}:{sum(1 for r in rows if r[6] is None or r[6] == d)}' for d in ('exercito', 'ar', 'mar'))
    own = sum(1 for r in rows if r[6] is not None)
    print(f'gravidades de baixa [{arms}], {own} próprias de uma arma')
    return errs, warns


def main():
    files = [Path(a) for a in sys.argv[1:]] or sorted((HERE / 'data' / 'countries').glob('*.sql'))
    static = HERE / 'data' / 'static.db'
    static = static if static.exists() else None
    bad = 0
    for f in files:
        errs, warns = check(f, static)
        for w in warns: print(f'  aviso {f.name}: {w}')
        for e in errs: print(f'  ERRO {f.name}: {e}')
        bad += bool(errs)
    # ids únicos entre ficheiros (unit_type, modifier, country_template, country_unit)
    if len(files) > 1:
        seen = {}
        for f in files:
            txt = f.read_text(encoding='utf-8')
            for kind, pat in (('unit_type', r"INSERT\s+INTO\s+unit_type[^;]*?VALUES\s*(.*?);"),):
                pass
        db = sqlite3.connect(':memory:')
        db.executescript((HERE / 'data' / 'schema.sql').read_text(encoding='utf-8'))
        db.executescript((HERE / 'data' / 'seed_units.sql').read_text(encoding='utf-8'))
        db.executescript((HERE / 'data' / 'seed_tech.sql').read_text(encoding='utf-8'))
        db.executescript((HERE / 'data' / 'seed_world.sql').read_text(encoding='utf-8'))
        try:
            for f in files: db.executescript(f.read_text(encoding='utf-8'))
        except sqlite3.Error as e:
            print(f'  ERRO: ao carregar todos os ficheiros juntos (ids repetidos?) — {e}'); bad += 1
        errs, warns = check_ranks(db, static)
        for w in warns: print(f'  aviso general_rank: {w}')
        for e in errs: print(f'  ERRO general_rank: {e}')
        bad += bool(errs)
        errs, warns = check_wounds(db, static)
        for w in warns: print(f'  aviso wound_kind: {w}')
        for e in errs: print(f'  ERRO wound_kind: {e}')
        bad += bool(errs)
    print('OK' if not bad else f'{bad} ficheiro(s) com erros')
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
