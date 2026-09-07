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
GENERAL_STATS = {'attack', 'defense', 'org_regain', 'move_speed', 'industry'}
# o que cada arma pode melhorar: uma escola de caça não dá recrutamento e uma de infantaria não dá bloqueio
DOCTRINE_STATS = {'exercito': {'attack', 'conscription', 'defense', 'industry', 'move_speed', 'org_regain', 'production_speed'},
                  'ar': {'air_losses', 'air_bombing', 'air_upkeep'},
                  'mar': {'naval_losses', 'naval_upkeep', 'naval_blockade', 'naval_escort', 'naval_patrol'}}
# chaves em que o número é uma conta a pagar (perdas, sustento): melhorar é descer, e um valor acima de 1
# seria uma escola que piora quem a aprende — quase sempre um sinal trocado
LOWER_IS_BETTER = {'air_losses', 'air_upkeep', 'naval_losses', 'naval_upkeep'}
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
                  'advisor', 'law', 'law_group', 'general', 'army_doctrine', 'army_doctrine_branch')
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

    # comandantes de casa: id prefixado, stat que o motor conheça e força dentro da gama dos mercenários
    for gid, gname, gstat, gmult, gcost, gicon in db.execute(
            'SELECT id,name,stat_key,mult,cost,icon FROM general WHERE country_tag=?', (tag,)):
        if not gid.startswith(tag + '_'): errs.append(f'general {gid}: id deve começar por {tag}_')
        if gstat not in GENERAL_STATS: errs.append(f'general {gid}: stat_key {gstat} desconhecido (usa {sorted(GENERAL_STATS)})')
        if not (1.05 <= gmult <= 1.20): warns.append(f'general {gid}: mult {gmult} fora de 1.05..1.20')
        if not (100 <= gcost <= 160): warns.append(f'general {gid}: custo {gcost} fora de 100..160')
        if not gicon: warns.append(f'general {gid}: sem chapa (o retrato do estado-maior fica vazio)')

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
    summary = (f'{tag}: {len(own_groups)} escadas de leis ({n_laws} leis), {n_adv} conselheiros, {n_gen} comandantes, '
               f'{len(own_branches)} escolas de guerra [{arms}] ({n_doc} degraus), {len(spirits)} espíritos, {db.execute("SELECT COUNT(*) FROM modifier WHERE country_tag=?", (tag,)).fetchone()[0]} efeitos, '
               f'{len(new_units)} unidades próprias, {db.execute("SELECT COUNT(*) FROM country_template WHERE country_tag=?", (tag,)).fetchone()[0]} templates próprios, '
               f'{n_units} brigadas nomeadas, stats {dict(db.execute("SELECT key,value FROM country_stat WHERE country_tag=?", (tag,)).fetchall())}')
    print(summary)
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
    print('OK' if not bad else f'{bad} ficheiro(s) com erros')
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
