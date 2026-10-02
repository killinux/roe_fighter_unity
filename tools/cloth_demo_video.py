# Side-by-side video of the bone cloth demo (RoeClothDemo): left without, right with RoeBoneCloth,
# one fighter after the other, the current move written underneath.
#   python cloth_demo_video.py [demo dir] [out.mp4] [--slow 2]
import argparse
import os
import subprocess

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
FFMPEG = r'D:\Program Files\ffmpeg\bin\ffmpeg.exe'
FONT = r'C:\Windows\Fonts\msyh.ttc'
MOVES = {'guard': '站架', 'walk': '前进', 'back': '后退', 'side step': '侧步', 'jab': '刺拳', 'straight': '直拳',
         'kick': '回旋踢', 'slash': '挥砍'}
NAMES = {'g04': 'g04 Luf', 'a08': 'a08 Inase'}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('dir', nargs='?', default=os.path.join(PROJECT, '_work', 'cloth_demo'))
    ap.add_argument('out', nargs='?', default=os.path.join(PROJECT, 'out', 'bone_cloth_demo.mp4'))
    ap.add_argument('--chars', default='g04,a08')
    ap.add_argument('--fps', type=int, default=30)
    ap.add_argument('--slow', type=float, default=1.0, help='play this many times slower')
    a = ap.parse_args()
    script = []
    for line in open(os.path.join(a.dir, 'script.txt'), encoding='utf-8'):
        t, what = line.strip().split(' ', 1)
        script.append((float(t), what))
    big = ImageFont.truetype(FONT, 34)
    small = ImageFont.truetype(FONT, 28)
    ff = None
    count = 0
    for cid in a.chars.split(','):
        off = sorted(os.listdir(os.path.join(a.dir, cid + '_off')))
        on = sorted(os.listdir(os.path.join(a.dir, cid + '_on')))
        for k, (f0, f1) in enumerate(zip(off, on)):
            im0 = Image.open(os.path.join(a.dir, cid + '_off', f0))
            im1 = Image.open(os.path.join(a.dir, cid + '_on', f1))
            w, h = im0.size
            frame = Image.new('RGB', (w * 2, h + 56), (18, 18, 18))
            frame.paste(im0, (0, 0))
            frame.paste(im1, (w, 0))
            d = ImageDraw.Draw(frame)
            d.text((16, 10), f'{NAMES.get(cid, cid)}　布料关', font=big, fill=(255, 255, 255), stroke_width=3, stroke_fill=(0, 0, 0))
            d.text((w + 16, 10), f'{NAMES.get(cid, cid)}　布料开（RoeBoneCloth）', font=big, fill=(255, 225, 110), stroke_width=3, stroke_fill=(0, 0, 0))
            t = k * 2 / 60.0
            move = ''
            for start, what in script:
                if t >= start - 0.05:
                    move = MOVES.get(what, what)
            d.text((16, h + 10), f'{move}　　{t:4.1f} s', font=small, fill=(220, 220, 220))
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
