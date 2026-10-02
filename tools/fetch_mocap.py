# Download the motion-capture clips used for the fighters' basic moves.
# Source: Bandai Namco Research Motion Dataset 1 (CC BY-NC 4.0, Bandai Namco Research Inc.),
# https://github.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset - BVH, 30 fps.
# Kept in _work/mocap/bandai1 (not in the repository).
#   python tools/fetch_mocap.py [--styles feminine,active,...]
import argparse
import concurrent.futures
import io
import os
import sys
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
BASE = ('https://raw.githubusercontent.com/BandaiNamcoResearchInc/Bandai-Namco-Research-Motiondataset/'
        'master/dataset/Bandai-Namco-Research-Motiondataset-1/data/')
LOCOMOTION = ['walk', 'walk-back', 'walk-left', 'walk-right', 'dash', 'run']
ATTACKS = ['punch_normal_001', 'punch_normal_002', 'kick_normal_001', 'slash_normal_001', 'slash_normal_002']


def fetch(name, out_dir):
    path = os.path.join(out_dir, name)
    if os.path.exists(path) and os.path.getsize(path) > 0:
        return name, 'have', os.path.getsize(path)
    try:
        with urllib.request.urlopen(BASE + name, timeout=120) as r:
            data = r.read()
    except urllib.error.HTTPError as e:
        return name, f'HTTP {e.code}', 0
    except Exception as e:
        return name, f'{type(e).__name__}', 0
    with open(path, 'wb') as f:
        f.write(data)
    return name, 'ok', len(data)


def main():
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('--styles', default='feminine,active,normal,chimpira,masculinity')
    ap.add_argument('--out', default=os.path.join(PROJECT, '_work', 'mocap', 'bandai1'))
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    names = [f'dataset-1_{m}_{s}_001.bvh' for m in LOCOMOTION for s in a.styles.split(',')]
    names += [f'dataset-1_{n}.bvh' for n in ATTACKS]
    names += [n.replace('.bvh', '.json') for n in names]
    with concurrent.futures.ThreadPoolExecutor(8) as pool:
        for name, status, size in pool.map(lambda n: fetch(n, a.out), names):
            print(f'{status:8s} {size:8d}  {name}')


if __name__ == '__main__':
    main()
