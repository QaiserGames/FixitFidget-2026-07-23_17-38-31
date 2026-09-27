"""Life-layer summary of a café life trace (pass 2).

usage: life.py <folder-with-trace.csv> [label]

Counts what the info column's pass-2 fields say: profiles and gaits seen per
person, who sat where (chair / bench), idle beats (episodes, not samples),
ambient chats, head-look episodes, and the director's own totals (life.txt).
"""
import sys, os, re, json
import pandas as pd, numpy as np

def field(info, key):
    m = re.search(r'(?:^|;)%s=([^;]*)' % re.escape(key), info)
    return m.group(1) if m else ''

def episodes(series):
    """Count runs of non-empty values per person, by value."""
    out = {}
    prev = None
    for v in series:
        if v and v != prev:
            out[v] = out.get(v, 0) + 1
        prev = v
    return out

def main(folder, label=''):
    df = pd.read_csv(os.path.join(folder, 'trace.csv'))
    # Seated bodies have their agent disabled (en=0), so no en filter here.
    p = df[df.kind.isin(['C', 'P'])].copy()
    p['info'] = p['info'].fillna('')
    for key in ('prof', 'gait', 'sit', 'beat', 'chair'):
        p[key] = p['info'].map(lambda s, k=key: field(s, k))
    p['chat'] = p['info'].str.contains(';chat', regex=False)
    people = p.groupby('id')
    res = {'label': label or os.path.basename(folder), 'seconds': round(float(df.t.max()), 1), 'people': int(people.ngroups)}
    # profiles and gaits
    prof = people['prof'].agg(lambda s: next((v for v in s if v and v != '-'), '-'))
    gait = people['gait'].agg(lambda s: next((v for v in s if v), ''))
    res['profiles'] = prof.value_counts().to_dict()
    res['gaits'] = gait.value_counts().to_dict()
    res['profiles_distinct'] = int(len([k for k in res['profiles'] if k != '-']))
    # walking styles actually seen walking (walk flag)
    walking = p[(p.walk == 1)]
    res['gaits_seen_walking'] = walking.groupby('id')['gait'].agg(lambda s: next((v for v in s if v), '')).value_counts().to_dict()
    # seats
    seated = p[p['sub'] == 'Seated']
    res['seated_people'] = int(seated.id.nunique())
    res['seated_by_chair_kind'] = seated.groupby('id')['chair'].agg(lambda s: next((v for v in s if v), '')).value_counts().to_dict()
    res['seated_person_seconds'] = round(len(seated) * 0.05, 1)
    # beats: episodes per type
    beats = {}
    for pid, g in people:
        e = episodes(g.sort_values('t')['beat'].tolist())
        for k, v in e.items(): beats[k] = beats.get(k, 0) + v
    res['beat_episodes'] = beats
    # seated beats only
    sb = {}
    for pid, g in p[p['sit'] == 'Seated'].groupby('id'):
        e = episodes(g.sort_values('t')['beat'].tolist())
        for k, v in e.items(): sb[k] = sb.get(k, 0) + v
    res['seated_beat_episodes'] = sb
    # chats
    chats = 0; chat_seconds = 0.0
    for pid, g in people:
        c = g.sort_values('t')['chat'].tolist()
        prev = False
        for v in c:
            if v and not prev: chats += 1
            prev = v
        chat_seconds += sum(c) * 0.05
    res['chat_episodes_per_person'] = chats
    res['chat_person_seconds'] = round(chat_seconds, 1)
    # look episodes (head)
    p['look'] = p['info'].str.contains(';look=', regex=False)
    looks = 0; look_seconds = 0.0
    for pid, g in people:
        c = g.sort_values('t')['look'].tolist()
        prev = False
        for v in c:
            if v and not prev: looks += 1
            prev = v
        look_seconds += sum(c) * 0.05
    res['look_episodes'] = looks
    res['look_person_seconds'] = round(look_seconds, 1)
    # queue: how much of queue time is spent looking vs not
    q = p[p['sit'] == 'Queue']
    res['queue_person_seconds'] = round(len(q) * 0.05, 1)
    res['queue_looking_share'] = round(float(q['look'].mean()), 2) if len(q) else None
    res['queue_beats'] = {}
    for pid, g in q.groupby('id'):
        e = episodes(g.sort_values('t')['beat'].tolist())
        for k, v in e.items(): res['queue_beats'][k] = res['queue_beats'].get(k, 0) + v
    life = os.path.join(folder, 'life.txt')
    res['director'] = open(life).read().strip() if os.path.exists(life) else ''
    print(json.dumps(res, indent=1))
    return res

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else '')
