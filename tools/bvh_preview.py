# Look inside a BVH clip: stick figures over time plus the speed of hands and feet, to find
# where single punches / kicks start and end.
#   python tools/bvh_preview.py <clip.bvh> [--out sheet.png] [--every 6] [--from 0] [--to -1]
# Prints the speed peaks of each hand and foot (frame, m/s) and draws a sheet: side view (x/y)
# of every n-th frame, numbered, with a speed plot underneath.
import argparse
import io
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont


def parse_bvh(path):
    lines = open(path, encoding='utf-8', errors='replace').read().split('\n')
    joints, stack, channels = [], [], []
    i = 0
    while i < len(lines):
        tok = lines[i].split()
        if not tok:
            i += 1
            continue
        if tok[0] in ('ROOT', 'JOINT'):
            joints.append({'name': tok[1], 'parent': stack[-1] if stack else -1, 'offset': None, 'channels': []})
            stack.append(len(joints) - 1)
        elif tok[0] == 'End':
            joints.append({'name': joints[stack[-1]]['name'] + '_end', 'parent': stack[-1], 'offset': None, 'channels': [], 'end': True})
            stack.append(len(joints) - 1)
        elif tok[0] == 'OFFSET':
            joints[stack[-1]]['offset'] = np.array([float(v) for v in tok[1:4]])
        elif tok[0] == 'CHANNELS':
            joints[stack[-1]]['channels'] = tok[2:]
            for c in tok[2:]:
                channels.append((stack[-1], c))
        elif tok[0] == '}':
            stack.pop()
        elif tok[0] == 'MOTION':
            break
        i += 1
    frames = int(lines[i + 1].split()[-1])
    dt = float(lines[i + 2].split()[-1])
    data = np.array([[float(v) for v in l.split()] for l in lines[i + 3:i + 3 + frames] if l.strip()])
    return joints, channels, data, dt


def rot(axis, deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    if axis == 'X':
        return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == 'Y':
        return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def forward_kinematics(joints, channels, row):
    """World positions (metres, BVH axes: y up) of every joint for one frame."""
    local_pos = [j['offset'].copy() if j['offset'] is not None else np.zeros(3) for j in joints]
    local_rot = [np.eye(3) for _ in joints]
    for k, (ji, c) in enumerate(channels):
        v = row[k]
        if c.endswith('position'):
            local_pos[ji]['XYZ'.index(c[0])] = v
        else:
            local_rot[ji] = local_rot[ji] @ rot(c[0], v)
    world_pos, world_rot = [None] * len(joints), [None] * len(joints)
    for i, j in enumerate(joints):
        if j['parent'] < 0:
            world_rot[i] = local_rot[i]
            world_pos[i] = local_pos[i]
        else:
            p = j['parent']
            world_rot[i] = world_rot[p] @ local_rot[i]
            world_pos[i] = world_pos[p] + world_rot[p] @ local_pos[i]
    return np.array(world_pos) * 0.01


def main():
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('bvh')
    ap.add_argument('--out', default='')
    ap.add_argument('--every', type=int, default=6)
    ap.add_argument('--from', dest='start', type=int, default=0)
    ap.add_argument('--to', type=int, default=-1)
    a = ap.parse_args()
    joints, channels, data, dt = parse_bvh(a.bvh)
    end = len(data) if a.to < 0 else min(a.to, len(data))
    pos = np.array([forward_kinematics(joints, channels, data[f]) for f in range(len(data))])
    names = [j['name'] for j in joints]
    track = {k: names.index(k) for k in ('Hand_L', 'Hand_R', 'Foot_L', 'Foot_R', 'Hips') if k in names}
    # speeds relative to the hips (strikes, not walking)
    speed = {}
    for k, ji in track.items():
        rel = pos[:, ji] - (pos[:, track['Hips']] if k != 'Hips' else 0)
        v = np.linalg.norm(np.diff(rel, axis=0), axis=1) / dt
        speed[k] = np.concatenate([[0], v])
    print(f'{os.path.basename(a.bvh)}: {len(data)} frames at {1 / dt:.0f} fps, {len(joints)} joints')
    for k in ('Hand_L', 'Hand_R', 'Foot_L', 'Foot_R'):
        if k not in speed:
            continue
        v = speed[k]
        peaks = [f for f in range(2, len(v) - 2) if v[f] == v[f - 2:f + 3].max() and v[f] > 3.0]
        print(f'  {k:7s} peaks (frame, m/s): ' + ' '.join(f'{f}:{v[f]:.1f}' for f in peaks))
    hip = pos[:, track['Hips']]
    print(f'  hips travel: x {hip[-1, 0] - hip[0, 0]:+.2f} z {hip[-1, 2] - hip[0, 2]:+.2f} m')

    if not a.out:
        return
    frames = list(range(a.start, end, a.every))
    cols = 10
    cw, chh = 150, 220
    rows = math.ceil(len(frames) / cols)
    img = Image.new('RGB', (cols * cw, rows * chh + 200), (25, 25, 30))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 14)
    colours = {'L': (90, 160, 255), 'R': (255, 120, 90)}
    for n, f in enumerate(frames):
        ox, oy = (n % cols) * cw + cw // 2, (n // cols) * chh + chh - 20
        p = pos[f]
        root = p[track['Hips']].copy()
        for i, j in enumerate(joints):
            if j['parent'] < 0 or j.get('end'):
                continue
            q = p[j['parent']]
            side = 'L' if j['name'].endswith('_L') else 'R' if j['name'].endswith('_R') else None
            col = colours.get(side, (220, 220, 220))
            # side view: x right (BVH x), y up; also a faint front view z
            x0, y0 = ox + (q[2] - root[2]) * 90, oy - q[1] * 90
            x1, y1 = ox + (p[i][2] - root[2]) * 90, oy - p[i][1] * 90
            d.line([x0, y0, x1, y1], fill=col, width=3)
        d.text((ox - cw // 2 + 4, oy - chh + 24), str(f), fill=(255, 255, 0), font=font)
    # speed plot
    top = rows * chh + 10
    W = cols * cw
    for k, col in (('Hand_L', (90, 160, 255)), ('Hand_R', (255, 120, 90)), ('Foot_L', (90, 255, 160)), ('Foot_R', (255, 220, 90))):
        if k not in speed:
            continue
        v = speed[k][a.start:end]
        pts = [(i * W / max(len(v) - 1, 1), top + 180 - min(v[i], 12) * 15) for i in range(len(v))]
        d.line(pts, fill=col, width=2)
    for f in range(a.start, end, 30):
        x = (f - a.start) * W / max(end - a.start - 1, 1)
        d.line([x, top, x, top + 180], fill=(70, 70, 70))
        d.text((x + 2, top), str(f), fill=(160, 160, 160), font=font)
    img.save(a.out)
    print('  sheet:', a.out)


if __name__ == '__main__':
    main()
