"""Bitki hattinin kayitlari (Blender yok): build_bitki.py raporlarindan game/data/assets.json'a yazar.

  python pipeline/bitki_sync.py

- Her manifest varligi icin assets.json satiri (yoksa eklenir): model, LOD'lar, kabuk, ikon, ucgen, boy, kaynak, lisans.
- Olcut 3 (kalinlik) ve 4 (butunluk) yaprak karti icin olculmez (karti ince levha olarak tasarlanmistir): pass null.
  Olcut 6-8 gorselden elle puanlanir; puan yoksa null. Bu yuzden durum yer_tutucu ve not "sahibin onayini bekliyor".
- Degisen modellerin eski PBR kayitlari (game/data/materials.json, pipeline/pbr_manifest.json: Tripo malzeme adlari)
  ve agac'in altin hat satiri (pipeline/golden_manifest.json: run_golden.py eski Tripo modelini geri getirmesin) silinir.
Tekrar kosturulabilir.
"""
import json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAN = json.load(open(os.path.join(ROOT, 'pipeline', 'bitki_manifest.json'), encoding='utf-8-sig'))
ASSETS = os.path.join(ROOT, 'game', 'data', 'assets.json')
CRIT = {1: 'doku', 2: 'poligon', 3: 'kalinlik', 4: 'butunluk', 5: 'olcek_eksen', 6: 'siluet', 7: 'dil_uyumu', 8: 'okunaklilik'}
IN_GAME = {
    'agac': 'World.Decor.cs: Cayir decor "agac" (tac + govde, ruzgar; LOD parcalari World.DecorLod.cs); Bicme Odun',
    'turuncu_agac': 'World.Decor.cs: Cayir decor "turuncu_agac"; Bicme Odun',
    'cali': 'World.Decor.cs + World.Resources.cs: Cayir decor "cali" (Bicme Lif tutami)',
    'egrelti': 'World.Decor.cs: Cayir ve Sis Korusu decor "egrelti"',
    'ot': 'World.Decor.cs + World.Resources.cs: Cayir "ot" ve Koru "koru_ot" (Bicme Lif tutami)',
    'bugday': 'World.Decor.cs + World.Resources.cs: Cayir "bugday" (Bicme Bugday tutami)',
    'cicek': 'World.Decor.cs: Cayir decor "cicek"',
}
BUDGET = {'tree': 'agac'}


def compact(o, ind=0, width=150):
    one = json.dumps(o, ensure_ascii=False)
    if len(one) + ind <= width or not isinstance(o, (dict, list)):
        return one
    pad = ' ' * (ind + 2)
    if isinstance(o, dict):
        items = [f'{pad}{json.dumps(k, ensure_ascii=False)}: {compact(v, ind + 2, width)}' for k, v in o.items()]
        return '{\n' + ',\n'.join(items) + '\n' + ' ' * ind + '}'
    items = [pad + compact(v, ind + 2, width) for v in o]
    return '[\n' + ',\n'.join(items) + '\n' + ' ' * ind + ']'


def write(path, data, fmt='compact'):
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write((compact(data) if fmt == 'compact' else json.dumps(data, ensure_ascii=False, indent=2)) + '\n')


def main():
    data = json.load(open(ASSETS, encoding='utf-8'))
    by_id = {e['id']: e for e in data['assets']}
    budgets = data['budgets']
    for name, it in MAN['items'].items():
        if it.get('disabled'):
            continue
        d = it.get('dir', name)
        rp = os.path.join(ROOT, 'game', 'assets', 'models', d, f'{name}.report.json')
        if not os.path.exists(rp):
            print(f'{name}: rapor yok (once build_bitki.py)'); continue
        r = json.load(open(rp, encoding='utf-8'))
        e = by_id.get(name)
        if e is None:
            e = {'id': name}
            data['assets'].append(e); by_id[name] = e
        e['name'] = e.get('name') if name in IN_GAME and e.get('name') else it.get('label', name)
        e.setdefault('goldenClass', '')
        cls = BUDGET.get(it['kind'], 'agac' if it['height'] >= 3 else 'esya_kaya')
        e['budgetClass'] = cls
        e['stream'] = 'varlik'
        e['inGame'] = name in IN_GAME
        e['usedBy'] = IN_GAME.get(name, 'henuz yok: Sis Korusu icin hazir (dunya2 satir ekleyerek kullanir; progress/bitki/NOT.md)')
        e['ownerApproved'] = False
        e['source'] = {'pool': 'kutuphane', 'service': f"F:\\Assets; pipeline/build_bitki.py ({it['kind']})", 'credits': 0, 'retries': 0,
                       'taskId': '', 'date': '2026-11-18'}
        e['license'] = it.get('license', '') + ' (pipeline/licenses.md, Bitkiler)'
        lim = budgets[cls]
        tex = r['lods'][0]['tex']
        old = {c['id']: c for c in e.get('criteria', [])}
        card = it['kind'] == 'tree' or any(m.get('clip') or m.get('alpha') or m.get('albedo_alpha') for m in it.get('materials', {}).values())
        crit = [
            {'id': 1, 'name': CRIT[1], 'pass': tex <= lim['maxTex'], 'value': f"albedo+alfa, normal{', puruzluluk' if it['kind'] != 'tree' else ''}; en buyuk {tex} px (sinir {lim['maxTex']})", 'note': ''},
            {'id': 2, 'name': CRIT[2], 'pass': r['tris'] <= lim['maxTris'], 'value': f"{r['tris']} / {lim['maxTris']} ucgen" + (' (sinif alt sinirinin altinda)' if r['tris'] < lim['minTris'] else ''), 'note': ''},
            {'id': 3, 'name': CRIT[3], 'pass': None, 'value': '', 'note': 'olculmedi: yaprak karti / ince yaprak tasarim geregi levha (alfa makasli)' if card else 'olculmedi (bitki hatti)'},
            {'id': 4, 'name': CRIT[4], 'pass': None, 'value': '', 'note': 'olculmedi: kart dortgenleri acik levha (manifold degil) tasarim geregi' if card else 'olculmedi (bitki hatti)'},
            {'id': 5, 'name': CRIT[5], 'pass': abs(r['size_m'][2] - it['height']) < 0.02, 'value': f"boy {r['size_m'][2]} m (hedef {it['height']}), taban z 0, orta [0, 0]", 'note': ''},
        ]
        for i in (6, 7, 8):
            crit.append({'id': i, 'name': CRIT[i], 'pass': None, 'value': '', 'score': None,
                         'note': 'sahibin onayini bekliyor (progress/bitki/once ve sonra goruntuleri)'})
        e['criteria'] = crit
        e['status'] = 'yer_tutucu'
        e['statusNote'] = 'dokulu PBR bitki (bitki-pbr); olcut 3-4 kart icin olculmez, 6-8 sahibin onayini bekliyor'
        e['notes'] = f"kaynak: {it.get('label', name)}"
        e['model'] = r['lods'][0]['path']
        e['forward'] = ''
        e['tris'] = r['tris']
        e['sizeM'] = r['size_m']
        e['lods'] = [{'tris': l['tris'], 'tex': l['tex'], 'path': l['path']} for l in r['lods']]
        e['hull'] = f'res://assets/models/{d}/{name}_hull.tres'
        e['icon'] = f'res://assets/models/{d}/{name}_icon.png'
        e['card'] = f'progress/bitki/render/{name}.png'
        e.setdefault('parts', []); e.setdefault('sockets', [])
    write(ASSETS, data)

    # eski Tripo PBR kayitlari
    names = set(MAN['items'])
    mpath = os.path.join(ROOT, 'game', 'data', 'materials.json')
    m = json.load(open(mpath, encoding='utf-8'))
    gone = [k for k, v in m['models'].items() if v.get('id') in names]
    for k in gone:
        del m['models'][k]
    write(mpath, m, 'indent')
    ppath = os.path.join(ROOT, 'pipeline', 'pbr_manifest.json')
    p = json.load(open(ppath, encoding='utf-8'))
    before = len(p['assets'])
    p['assets'] = [a for a in p['assets'] if a.get('id') not in names]
    write(ppath, p, 'indent')
    gpath = os.path.join(ROOT, 'pipeline', 'golden_manifest.json')
    g = json.load(open(gpath, encoding='utf-8'))
    gg = g['assets'].pop('agac', None)
    write(gpath, g, 'indent')
    print('assets.json guncellendi;', 'materials.json -', len(gone), '; pbr_manifest -', before - len(p['assets']), '; golden agac', 'silindi' if gg else 'yoktu')


main()
