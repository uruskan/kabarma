import json, re, sys
src = open(sys.argv[1], encoding='utf-8').read().splitlines()
start = next(i for i, l in enumerate(src) if l.startswith('## 19.'))
end = next(i for i, l in enumerate(src) if l.startswith('## 20.'))
rows = [l for l in src[start:end] if l.startswith('| **')]
assert len(rows) == 8, len(rows)
tr = str.maketrans('çğıöşüÇĞİÖŞÜâîû', 'cgiosuCGIOSUaiu')
def slug(s):
    s = s.translate(tr).lower()
    return re.sub(r'[^a-z0-9]+', '_', s).strip('_')
branches = [('kazi', 'Kazı', 'dig_speed'), ('hasat', 'Hasat', 'gather_yield'), ('ciftcilik', 'Çiftçilik', 'crop_yield'),
            ('hayvancilik', 'Hayvancılık', 'tame_rate'), ('muhendislik', 'Mühendislik', 'craft_speed'),
            ('ascilik', 'Aşçılık', 'food_duration'), ('savas', 'Savaş', 'damage'), ('surus', 'Sürüş', 'vehicle_speed')]
# T1-T2 (10. ve 20. seviye) sayisal etkiler: (stat, op, deger). Olmayanlar: yalniz secim (Has).
E = {
 'Çift Damla': [('ore_yield','add',0.15)], 'Hızlı Matkap': [('dig_speed','add',0.20)],
 'Damar Kokusu': [('ore_compass','add',1)], 'Sağlam Sırt': [('ore_stack','set',100)],
 'Oduncu': [('wood_yield','add',0.20)], 'Toplayıcı': [('wild_plant_yield','add',1.0)],
 'Tohum Avcısı': [('wild_seed_gene','add',1)], 'Geniş Biçim': [('scythe_area','add',0.30)],
 'Bol Hasat': [('crop_yield','add',0.10)], 'Çabuk Filiz': [('crop_growth','add',0.10)],
 'Su Bilgisi': [('moisture_use','add',-0.30)], 'Gübreci': [('compost_effect','add',0.5)],
 'Tatlı Dil': [('tame_rate','add',0.5)], 'Bol Yem': [('feed_use','add',-0.25)],
 'Sağlam Eyer': [('mount_speed','add',0.15)], 'Dolu Heybe': [('mount_carry','add',0.5)],
 'Çabuk El': [('craft_speed','add',0.15)], 'Tutumlu': [('craft_refund','add',0.10)],
 'Uzun Kablo': [('cable_len','set',16)], 'Yalıtım': [('battery_cap','add',0.20)],
 'Lezzet': [('food_duration','add',0.20)], 'Bol Porsiyon': [('food_double','add',0.20)],
 'Baharatçı': [('food_special','add',0.25)], 'Erzakçı': [('food_stack','set',20)],
 'Keskin Nişan': [('crit','add',0.05)], 'Kalın Deri': [('max_hp','add',15)],
 'Hızlı Dolum': [('head_swap_instant','set',1)], 'Soğukkanlı': [('gosteri_hold','set',1)],
 'Hafif Ayak': [('fuel_use','add',-0.20)], 'Sağlam Kasa': [('vehicle_hp','add',0.25)],
 'Yol Tutuş': [('vehicle_stable','set',1)], 'Çekici': [('trailer_cap','add',0.5)],
}
perks = []
used = set()
for (bid, bname, _), row in zip(branches, rows):
    cells = [c.strip() for c in row.strip('|').split('|')]
    assert len(cells) == 6, row
    assert bname in cells[0], (bname, cells[0])
    for tier, cell in zip((10, 20, 30, 40, 50), cells[1:]):
        parts = cell.split(' / ')
        assert len(parts) == 2, (bname, tier, cell)
        for slot, part in zip('AB', parts):
            if ':' in part:
                name, text = [x.strip() for x in part.split(':', 1)]
            else:
                name, text = part.strip(), ''
            pid = f'{bid}_{tier}{slot.lower()}_{slug(name)}'
            assert pid not in used
            used.add(pid)
            eff = E.get(name) if tier <= 20 else None
            p = {'id': pid, 'branch': bid, 'tier': tier, 'slot': slot, 'name': name, 'text': text, 'live': eff is not None}
            p['effects'] = [{'stat': s, 'op': o, 'value': v} for s, o, v in (eff or [])]
            perks.append(p)
assert len(perks) == 80
missing = [k for k in E if not any(p['name'] == k for p in perks)]
assert not missing, missing
xp = {
 'gather:stone': ('kazi', 2), 'gather:clay': ('kazi', 2), 'gather:copper_ore': ('kazi', 4),
 'gather:wood': ('hasat', 2), 'gather:fiber': ('hasat', 2), 'gather:wheat': ('hasat', 2),
 'plant': ('ciftcilik', 2), 'harvest': ('ciftcilik', 3), 'swell': ('ciftcilik', 6),
 'pen': ('hayvancilik', 25), 'lay': ('hayvancilik', 4), 'tame': ('hayvancilik', 40),
 'produce': ('muhendislik', 4), 'station': ('muhendislik', 10), 'build': ('muhendislik', 2),
 'cook': ('ascilik', 6), 'ate': ('ascilik', 1),
 'kill': ('savas', 2), 'kill_swollen': ('savas', 6),
 'drive_100m': ('surus', 2), 'ride_100m': ('surus', 2),
}
out = {
 '_not': 'Ustalik: GDD 10, ICERIK 19 (80 ozellik). n. seviye esigi = thresholdBase x n^thresholdExponent (toplam XP). Her seviye levelStat etkisine +%1. 10/20/30/40/50. seviyede A/B secilir. live=true: 10. ve 20. seviye (T1-T2) ozelliklerinin sayisal etkisi M2\'de; effects stat kimlikleri docs/ICERIK.md 19 "Ustalik etki kimlikleri". xp: kaynak kimligi -> dal ve XP (denge M7 ayari). Ozellik listesi tools/gen_mastery.py ile ICERIK 19 tablosundan uretildi.',
 'maxLevel': 50, 'thresholdBase': 100, 'thresholdExponent': 1.6, 'perLevel': 0.01,
 'perkLevels': [10, 20, 30, 40, 50], 'respecCreditsPer10Levels': 1000,
 'branches': [{'id': b, 'name': n, 'levelStat': s} for b, n, s in branches],
 'xp': {k: {'branch': b, 'xp': x} for k, (b, x) in xp.items()},
 'perks': perks,
}
json.dump(out, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('ok', len(perks), sum(p['live'] for p in perks))

