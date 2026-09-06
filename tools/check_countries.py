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
STAT_KEYS = {'industry', 'production_speed', 'org_regain', 'start_army_mult'}
MOD_STATS = {'str', 'str_attacker', 'str_defender', 'command'}
UNIT_STATS = {'soft_atk', 'hard_atk', 'defense', 'breakthrough', 'armor', 'piercing', 'hardness', 'hp'}
COND_KEYS = {'terrain', 'river', 'country'}


def norm(s):
    import unicodedata
    return ''.join(ch for ch in unicodedata.normalize('NFD', s or '') if unicodedata.category(ch) != 'Mn').lower().strip()


def check(path, static):
    tag = path.stem
    errs, warns = [], []
    db = sqlite3.connect(':memory:')
    db.executescript((HERE / 'data' / 'schema.sql').read_text(encoding='utf-8'))
    db.executescript((HERE / 'data' / 'seed_units.sql').read_text(encoding='utf-8'))
    base_units = {r[0] for r in db.execute('SELECT id FROM unit_type')}
    base_mods = {r[0] for r in db.execute('SELECT id FROM modifier')}
    base_tags = {r[0] for r in db.execute('SELECT DISTINCT tag FROM unit_tag')}
    terrains = {r[0] for r in db.execute('SELECT id FROM terrain')}
    if static:
        db.execute('ATTACH DATABASE ? AS st', (str(static),))
        if not db.execute('SELECT 1 FROM st.country WHERE tag=?', (tag,)).fetchone():
            errs.append(f'tag {tag} não existe na tabela country')
    try:
        db.executescript(path.read_text(encoding='utf-8'))
    except sqlite3.Error as e:
        return [f'SQL inválido: {e}'], warns

    # só fala do próprio país
    for table, col in (('country_stat', 'country_tag'), ('country_info', 'country_tag'), ('national_spirit', 'country_tag'),
                       ('country_template', 'country_tag'), ('country_unit', 'country_tag'), ('modifier', 'country_tag')):
        for (t,) in db.execute(f'SELECT DISTINCT {col} FROM {table} WHERE {col} IS NOT NULL'):
            if t != tag: errs.append(f'{table}: country_tag {t} num ficheiro de {tag}')
    for m in re.finditer(r"UPDATE\s+country\s+SET[^;]*WHERE\s+tag\s*=\s*'(\w+)'", path.read_text(encoding='utf-8'), re.I):
        if m.group(1) != tag: errs.append(f'UPDATE country de {m.group(1)} num ficheiro de {tag}')

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
    summary = (f'{tag}: {len(spirits)} espíritos, {db.execute("SELECT COUNT(*) FROM modifier WHERE country_tag=?", (tag,)).fetchone()[0]} efeitos, '
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
        try:
            for f in files: db.executescript(f.read_text(encoding='utf-8'))
        except sqlite3.IntegrityError as e:
            print(f'  ERRO: ids repetidos entre ficheiros — {e}'); bad += 1
    print('OK' if not bad else f'{bad} ficheiro(s) com erros')
    sys.exit(1 if bad else 0)


if __name__ == '__main__':
    main()
