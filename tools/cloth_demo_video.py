# Side-by-side video of a RoeClothDemo run: the takes of each fighter next to each other (bone cloth
# off | on, or one take per motion pack), one fighter after the other, the current move written
# under each take.
#   python cloth_demo_video.py [demo dir] [out.mp4] [--variants off,on] [--slow 2]
#   python cloth_demo_video.py _work\pack_demo out\motion_packs_demo.mp4
import argparse
import os
import subprocess

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
FFMPEG = r'D:\Program Files\ffmpeg\bin\ffmpeg.exe'
FONT = r'C:\Windows\Fonts\msyh.ttc'
STEPS = {'guard': '站架', 'walk': '前进', 'back': '后退', 'side step': '侧步'}
MOVES = {'jab': '刺拳', 'cross': '直拳', 'straight': '直拳', 'kick': '回旋踢', 'slash': '挥砍', 'hook': '勾拳',
         'uppercut': '上勾拳', 'roundhouse_lead': '前腿回旋踢', 'front_kick': '前踢', 'side_kick': '侧踢', 'roundhouse': '后腿回旋踢', 'low_kick': '低踢',
         'high_kick': '高踢', 'knee': '膝撞', 'elbow': '肘击', 'lunge_punch': '冲拳', 'reverse_punch': '逆冲拳',
         'back_fist': '里拳', 'chop': '手刀'}
NAMES = {'g04': 'g04 Luf', 'a08': 'a08 Inase'}
TITLES = {'off': '布料关', 'on': '布料开（RoeBoneCloth）', 'legacy': '旧版布料（10-02）', 'magica_style': '新版：照 Magica 的做法'}


def read_takes(folder):
    """{(id, variant): (about, {button: move})} from takes.txt (older runs: cloth off/on, Bandai moves)."""
    path = os.path.join(folder, 'takes.txt')
    takes = {}
    if not os.path.exists(path):
        bandai = {'A': 'jab', 'B': 'slash', 'C': 'cross', 'D': 'kick'}
        for cid in NAMES:
            for v in ('off', 'on'):
                takes[(cid, v)] = ('', bandai)
        return takes
    for line in open(path, encoding='utf-8'):
        cols = line.rstrip('\n').split('\t')
        if len(cols) < 4:
            continue
        moves = dict(kv.split('=', 1) for kv in cols[3].split('|') if '=' in kv)
        takes[(cols[0], cols[1])] = (cols[2], moves)
    return takes


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('dir', nargs='?', default=os.path.join(PROJECT, '_work', 'cloth_demo'))
    ap.add_argument('out', nargs='?', default=os.path.join(PROJECT, 'out', 'bone_cloth_demo.mp4'))
    ap.add_argument('--chars', default='g04,a08')
    ap.add_argument('--variants', default='', help='takes to put side by side, left to right (default: as filmed)')
    ap.add_argument('--fps', type=int, default=30)
    ap.add_argument('--slow', type=float, default=1.0, help='play this many times slower')
    ap.add_argument('--suffix', default='', help='added to every title, e.g. the motion pack')
    a = ap.parse_args()
    script = []
    for line in open(os.path.join(a.dir, 'script.txt'), encoding='utf-8'):
        t, what = line.strip().split(' ', 1)
        script.append((float(t), what))
    takes = read_takes(a.dir)
    big = ImageFont.truetype(FONT, 34)
    small = ImageFont.truetype(FONT, 28)
    colours = [(255, 255, 255), (255, 225, 110), (140, 220, 255), (200, 255, 160)]
    ff = None
    count = 0
    for cid in a.chars.split(','):
        variants = a.variants.split(',') if a.variants else [v for (c, v) in takes if c == cid]
        frames = [sorted(os.listdir(os.path.join(a.dir, f'{cid}_{v}'))) for v in variants]
        for k in range(min(len(f) for f in frames)):
            ims = [Image.open(os.path.join(a.dir, f'{cid}_{v}', frames[n][k])) for n, v in enumerate(variants)]
            w, h = ims[0].size
            frame = Image.new('RGB', (w * len(ims), h + 56), (18, 18, 18))
            d = ImageDraw.Draw(frame)
            t = k * 2 / 60.0
            what = ''
            for start, step in script:
                if t >= start - 0.05:
                    what = step
            for n, (v, im) in enumerate(zip(variants, ims)):
                frame.paste(im, (w * n, 0))
                about, moves = takes.get((cid, v), ('', {}))
                title = (TITLES.get(v) or (about.split(';')[0] if about else v)) + a.suffix
                d.text((w * n + 16, 10), f'{NAMES.get(cid, cid)}　{title}', font=big, fill=colours[n % len(colours)],
                       stroke_width=3, stroke_fill=(0, 0, 0))
                if what in moves:
                    move = moves[what]
                    label = f'{what} 键：{MOVES.get(move, move)}' + (f'（{move}）' if move in MOVES else '')
                else:
                    label = STEPS.get(what) or MOVES.get(what, what)
                d.text((w * n + 16, h + 10), f'{label}　　{t:4.1f} s', font=small, fill=(220, 220, 220))
            if ff is None:
                ff = subprocess.Popen([FFMPEG, '-y', '-loglevel', 'error', '-f', 'rawvideo', '-pix_fmt', 'rgb24',
                                       '-s', f'{frame.width}x{frame.height}', '-r', str(a.fps / a.slow), '-i', '-',
                                       '-c:v', 'libx264', '-crf', '18', '-preset', 'slow', '-pix_fmt', 'yuv420p', a.out],
                                      stdin=subprocess.PIPE)
            ff.stdin.write(frame.tobytes())
            count += 1
    ff.stdin.close()
    ff.wait()
    print(f'{a.out}: {count} frames, {count / (a.fps / a.slow):.1f} s, {os.path.getsize(a.out) / 1e6:.1f} MB')


if __name__ == '__main__':
    main()
