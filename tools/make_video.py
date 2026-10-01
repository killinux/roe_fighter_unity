# Turn the frames written by RoeShowcase.Video into an mp4, with the game's own skill sounds
# laid over the matching clips.
#   python make_video.py [video dir] [out.mp4]
# Sound for clip skill_0N of character <id>: the file in Assets/ROE/<id>/sfx_* whose name
# contains "skillN" / "skill0N" / "skill_N" and no "hit"; for die / hurt: "die".
import json
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
FFMPEG = r'D:\Program Files\ffmpeg\bin\ffmpeg.exe'


def sfx_for(manifest, cid, clip):
    files = manifest.get(cid, {}).get('sfx', [])
    m = re.match(r'skill_0?(\d)', clip)
    if m:
        n = m.group(1)
        pat = re.compile(r'skill_?0?%s(?!\d)' % n, re.I)
        cands = [f for f in files if pat.search(os.path.basename(f)) and 'hit' not in os.path.basename(f).lower()]
        return cands[0] if cands else None
    if clip == 'die':
        cands = [f for f in files if 'die' in os.path.basename(f).lower()]
        return cands[0] if cands else None
    return None


def main():
    vdir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(PROJECT, '_work', 'video')
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(vdir, 'showcase.mp4')
    timeline = json.load(open(os.path.join(vdir, 'timeline.json'), encoding='utf-8'))
    manifest = json.load(open(os.path.join(PROJECT, 'Assets', 'ROE', 'roe_manifest.json'), encoding='utf-8'))
    fps = timeline[0]['fps']

    cmd = [FFMPEG, '-y', '-loglevel', 'error', '-framerate', str(fps), '-i', os.path.join(vdir, 'frames', '%05d.jpg')]
    delays = []
    for seg in timeline:
        s = sfx_for(manifest, seg['id'], seg['clip'])
        if s:
            cmd += ['-i', os.path.join(PROJECT, s)]
            delays.append(int(seg['frame'] * 1000 / fps))
            print(f"  {seg['id']} {seg['clip']} at {seg['frame'] / fps:6.2f} s: {os.path.basename(s)}")
    total = sum(seg['frames'] for seg in timeline) / fps
    if delays:
        parts = []
        for i, d in enumerate(delays):
            parts.append(f'[{i + 1}:a]adelay={d}|{d},volume=0.9[a{i}]')
        mix = ''.join(f'[a{i}]' for i in range(len(delays)))
        parts.append(f'{mix}amix=inputs={len(delays)}:normalize=0:dropout_transition=0,apad[aout]')
        cmd += ['-filter_complex', ';'.join(parts), '-map', '0:v', '-map', '[aout]', '-c:a', 'aac', '-b:a', '192k']
    cmd += ['-c:v', 'libx264', '-crf', '17', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-t', f'{total:.3f}', out]
    subprocess.run(cmd, check=True)
    print(f'{out}: {total:.1f} s, {os.path.getsize(out) / 1e6:.1f} MB')


if __name__ == '__main__':
    main()
