"""An outfit's game clips decoded onto its hd skeleton, as JSON (what tools/roe_grip.py reads): ripper_tpose's decoder
(scripts/riseoferos decode_roe_clip.py), reading the game's bundles only, writing nothing but the JSON (no VMD, nothing in
the export archive - export_roe_motions.py there does all of that for the outfits it exports).
    python tools/roe_clips.py <id> [<out dir>] [--clips idle_01,skill_01]
Out: _work/roe_clips/<id>/<clip>.json (30 frames a second).  ROE_RIPPER: the ripper_tpose checkout
(default E:/code/othercode/ripper_tpose); UnityPy needed.
"""
import argparse
import os
import sys

RIPPER = os.environ.get("ROE_RIPPER", r"E:\code\othercode\ripper_tpose")
sys.path.insert(0, os.path.join(RIPPER, "scripts", "riseoferos"))
import UnityPy  # noqa: E402

import decode_roe_clip as dec  # noqa: E402
from roe_motion_common import bundles  # noqa: E402

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("out", nargs="?")
    ap.add_argument("--clips", help="a,b: only these")
    a = ap.parse_args()
    out = a.out or os.path.join(PROJECT, "_work", "roe_clips", a.id)
    found = bundles(a.id)
    if not found.get("showcase"):
        sys.exit("no bundle chara_armor_pc_%s_hd & ld_hd.ab" % a.id)
    envs = {"showcase": UnityPy.load(found["showcase"])}
    if found.get("battle"):
        envs["battle"] = UnityPy.load(found["battle"])
    skeleton = dec.read_skeleton(envs["showcase"])     # the hd prefab's skeleton (the battle clips live with the low-detail one)
    os.makedirs(out, exist_ok=True)
    wanted = set(a.clips.split(",")) if a.clips else None
    for group, env in envs.items():
        clips = dec.clips_in(env)
        for name, seconds in dec.list_clips(env):
            if wanted and name not in wanted:
                continue
            print(group, dec.decode_clip(clips[name], skeleton, os.path.join(out, name + ".json")), flush=True)


if __name__ == "__main__":
    main()
