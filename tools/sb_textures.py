"""The Unity maps of Stellar Blade's Eve exported by tools/sb_fbx.py, from her game's own material instances:

    python tools/sb_textures.py Assets/SB/<id> [--size 2048]

The archived .blend (ripper_tpose validate_eve.py) wires only colour maps; the game's material instances say which maps
each material really uses.  Each one is read from the game (CUE4Parse CLI, -f json, into _work/sb/research/json; the
textures as PNG into _work/sb/tex - both caches, read again on the next run) and turned into what URP Lit takes:
  - outfit layers (MA_CH_P_EVE_*: Basecolor, ORM, Baked NormalMap): colour; URP's mask from ORM (R metallic = ORM blue,
    G occlusion = ORM red, A smoothness = 1 - ORM green); the normal map with green flipped (Unreal's are DirectX);
    glow: the Emissive map's channel ML_GlowEmissive_CH picks, in ML_GlowEm_EyeCoreHDRTint's colour (its brightness
    capped - Unreal's bloom is not URP's), or the emissive map times EmissiveColor x EmissivePower;
  - skin (MA_Skin_EVE): BaseColor, BaseNormal; ORSS: R occlusion, G roughness spread over MinRoughness..MaxRoughness;
  - the wing plates (MA_Plastic_2, a see-through glowing plastic): its Colour, see-through, the Emissive map glowing in
    its Color;
  - the lace (MA_DitherAlpha): colour cut out by its alpha, its emissive map in its Emissive Color;
  - hair (MA_CH_Hair: root / tip colours x Brightness) and the ponytail (Mat_Hair_V1: the two gradient colours), cut out by
    their alpha map; brows (BaseColour cut out by the mask's red);
  - the eyes: the .blend's own colour (sb_fbx.py bakes the iris into it); teeth: the .blend's maps;
  - the eye's wet film, shadow shell, the teeth's occlusion card and the head's unused "NewMaterial": hidden (alpha 0).
unity.json is what VdfFighter's builder reads (SbFighter.Build), plus emission (map, colour).
"""
import argparse
import glob
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from vdf_textures import channel, linear_to_srgb, load, save_rgba, srgb_to_linear  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
CLI = r"E:\tools\cue4parse_cli_ff7\cue4parse.exe"
PAKS = r"D:\Program Files (x86)\Steam\steamapps\common\StellarBlade\SB\Content\Paks"
USMAP = r"E:\game_export\StellarBlade\_meta\mappings\StellarBlade_1.1.0.usmap"
JSON_DIR = os.path.join(PROJECT, "_work", "sb", "research", "json")
TEX_DIR = os.path.join(PROJECT, "_work", "sb", "tex")
# where the materials of her meshes live (searched in this order for a material of that name)
MI_DIRS = ["Art/Character/PC/CH_P_EVE_09/Materials", "Art/Character/PC/CH_P_EVE_Head/Materials",
           "Art/Character/PC/00_HR/EVE_HR_01/Materials", "Art/HairMaterial", "Art/Character/Generic/GlobalMasterMaterials/Eye",
           "Art/Character/Generic/GlobalMasterMaterials/Skin"]
HIDDEN = {"eye-wet", "eye-occlusion"}
GLOW_MAX = 2.5          # brightest emission colour component (URP has no bloom here: the game's 6.9 would only clip)


def cli(*patterns, fmt=None, out=None):
    args = [CLI, "-i", PAKS, "-g", "GAME_StellarBlade", "-m", USMAP, "-o", out, "-y"]
    if fmt:
        args += ["-f", fmt]
    for p in patterns:
        args += ["-p", p]
    r = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return r.stdout + r.stderr


def mi_json(name, opts):
    """The material instance's exported properties (CUE4Parse JSON) from the first folder that has it; the candidates are
    exported (one CLI run) when none is cached."""
    dirs = ([opts.mi_dir] if opts.mi_dir else []) + MI_DIRS
    paths = [os.path.join(JSON_DIR, "SB", "Content", d.replace("/", os.sep), name + ".json") for d in dirs]
    if not any(os.path.exists(p) for p in paths):
        cli(*[f"SB/Content/{d}/{name}.*" for d in dirs], fmt="json", out=JSON_DIR)
    for path in paths:
        if os.path.exists(path):
            data = json.load(open(path, encoding="utf-8-sig"))
            for e in data:
                if e.get("Type") in ("MaterialInstanceConstant", "Material"):
                    return e.get("Properties", {}), path
    return None, None


def params(props):
    tex = {t["ParameterInfo"]["Name"]: (t.get("ParameterValue") or {}).get("ObjectPath") for t in props.get("TextureParameterValues", [])}
    sca = {s["ParameterInfo"]["Name"]: float(s["ParameterValue"]) for s in props.get("ScalarParameterValues", [])}
    vec = {v["ParameterInfo"]["Name"]: [float(v["ParameterValue"][k]) for k in "RGBA"] for v in props.get("VectorParameterValues", [])}
    parent = ((props.get("Parent") or {}).get("ObjectName") or "").split("'")[1] if props.get("Parent") else ""
    return parent, tex, sca, vec


def texture(object_path):
    """A texture parameter's map as a local PNG (exported from the game when not cached)."""
    if not object_path:
        return None
    pkg = object_path.split(".")[0]                       # /Game/Art/.../Name
    rel = "SB/Content/" + pkg[len("/Game/"):]
    png = os.path.join(TEX_DIR, rel.replace("/", os.sep) + ".png")
    if not os.path.exists(png):
        cli(rel + ".*", out=TEX_DIR)
    return png if os.path.exists(png) else None


def normal_map(path, size, out):
    n = np.asarray(load(path, size).convert("RGB"), np.uint8).copy()
    n[..., 1] = 255 - n[..., 1]                           # DirectX (Unreal) -> OpenGL (Unity)
    Image.fromarray(n, "RGB").save(out)


def mask_from_orm(path, size, out, ao=0, rough=1, metal=2, rough_range=None):
    orm = np.asarray(load(path, size).convert("RGBA"), np.float32) / 255.0
    r = orm[..., rough]
    if rough_range:
        r = rough_range[0] + (rough_range[1] - rough_range[0]) * r
    m = orm[..., metal] if metal is not None else np.zeros_like(r)
    mask = np.dstack([m, orm[..., ao], np.zeros_like(r), 1.0 - np.clip(r, 0, 1)])
    Image.fromarray(np.round(np.clip(mask, 0, 1) * 255).astype(np.uint8), "RGBA").save(out)


def glow(rgb):
    """An HDR colour scaled so its brightest component is at most GLOW_MAX (sRGB for Unity's colour field)."""
    c = np.array(rgb[:3], np.float32)
    peak = float(c.max())
    if peak > GLOW_MAX:
        c = c * GLOW_MAX / peak
    return [round(float(x), 4) for x in c]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--size", type=int, default=2048)
    ap.add_argument("--mi-dir", default="", help="the outfit's material folder first (default: CH_P_EVE_09's)")
    opts = ap.parse_args()
    side = json.load(open(os.path.join(opts.folder, "materials.json"), encoding="utf-8"))
    tex_out = os.path.join(opts.folder, "textures")
    os.makedirs(tex_out, exist_ok=True)
    entries = []
    for name, d in side["materials"].items():
        kind = d.get("kind", "pbr")
        stem = name.replace(" ", "_")
        e = {"name": name, "kind": kind, "albedo": "", "normal": "", "mask": "", "emission": "", "color": [1, 1, 1, 1],
             "emissionColor": [0, 0, 0], "smoothness": 0.5, "metallic": 0.0, "normalScale": 1.0, "cutoff": 0.5,
             "surface": "opaque", "cull": "back", "source": ""}
        props, src = mi_json(name, opts)
        parent, tex, sca, vec = params(props) if props else ("", {}, {}, {})
        e["source"] = f"{parent} ({os.path.relpath(src, PROJECT) if src else 'no material instance found'})"
        alpha = None

        def put_albedo(path, use_alpha=False, tint=None):
            nonlocal alpha
            im = load(path, opts.size, "RGBA")
            arr = np.asarray(im, np.float32) / 255.0
            lin = srgb_to_linear(arr[..., :3])
            if tint is not None:
                lin = lin * np.array(tint[:3], np.float32)
            if use_alpha:
                alpha = arr[..., 3]
            out = os.path.join(tex_out, f"{stem}_albedo.png")
            save_rgba(linear_to_srgb(lin), alpha, out)
            e["albedo"] = os.path.basename(out)

        def put_normal(path):
            if path:
                out = os.path.join(tex_out, f"{stem}_normal.png")
                normal_map(path, opts.size, out)
                e["normal"] = os.path.basename(out)

        def put_emission(path, color, channel_mask=None):
            if not path:
                return
            arr = np.asarray(load(path, opts.size, "RGBA"), np.float32) / 255.0
            if channel_mask is not None:
                k = int(np.argmax(channel_mask[:4]))
                g = arr[..., k]
                rgb = np.dstack([g, g, g])
            else:
                rgb = arr[..., :3]
            if float(rgb.max()) <= 1e-3:
                return
            out = os.path.join(tex_out, f"{stem}_emission.png")
            Image.fromarray(np.round(np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").save(out)
            e["emission"] = os.path.basename(out)
            e["emissionColor"] = glow(color)

        if kind in HIDDEN or (name == "NewMaterial" and float(d.get("alpha", 1)) <= 0.0):
            e["surface"] = "transparent"
            e["color"] = [0, 0, 0, 0]
        elif "Basecolor" in tex and "ORM" in tex:
            put_albedo(texture(tex["Basecolor"]))
            put_normal(texture(tex.get("Baked NormalMap")))
            orm = texture(tex["ORM"])
            if orm:
                out = os.path.join(tex_out, f"{stem}_mask.png")
                mask_from_orm(orm, opts.size, out)
                e["mask"] = os.path.basename(out)
                e["smoothness"] = e["metallic"] = 1.0
            if "ML_GlowEmissive_CH" in vec and "ML_GlowEm_EyeCoreHDRTint" in vec and tex.get("Emissive"):
                put_emission(texture(tex["Emissive"]), vec["ML_GlowEm_EyeCoreHDRTint"], vec["ML_GlowEmissive_CH"])
            elif tex.get("Emissive") and sca.get("EmissivePower", 0.0) > 0.0 and "EmissiveColor" in vec:
                put_emission(texture(tex["Emissive"]), [c * sca["EmissivePower"] for c in vec["EmissiveColor"]])
            e["cull"] = "off"
        elif parent == "MA_Skin_EVE":
            put_albedo(texture(tex.get("BaseColor")) or d.get("albedo"))
            put_normal(texture(tex.get("BaseNormal")))
            orss = texture(tex.get("ORSS Mask"))
            if orss:
                out = os.path.join(tex_out, f"{stem}_mask.png")
                mask_from_orm(orss, opts.size, out, ao=0, rough=1, metal=None,
                              rough_range=(sca.get("MinRoughness", 0.2), sca.get("MaxRoughness", 1.0)))
                e["mask"] = os.path.basename(out)
                e["smoothness"] = 1.0
                e["metallic"] = 0.0
        elif parent == "MA_Plastic_2":
            col = linear_to_srgb(np.array(vec.get("Colour", [0.06, 0.1, 0.09, 1])[:3], np.float32))
            e["color"] = [round(float(x), 4) for x in col] + [0.55]
            e["surface"] = "transparent"
            e["smoothness"] = 0.9
            put_normal(texture(tex.get("Normal")))
            put_emission(texture(tex.get("Emissive")), [c * min(1.0, sca.get("Power", 1.0)) * 2.0 for c in vec.get("Color", [0, 1, 0.25, 0])])
            e["cull"] = "off"
        elif parent == "MA_DitherAlpha":
            put_albedo(texture(tex.get("BaseColor")), use_alpha=True)
            put_normal(texture(tex.get("Baked NormalMap")))
            e["surface"] = "cutout"
            e["cutoff"] = 0.4
            e["smoothness"] = 1.0 - 0.5 * (sca.get("Rougness Min ", 0.43) + sca.get("Rougness Max", 0.35))
            e["metallic"] = 0.6
            if tex.get("Emissive Texture"):
                put_emission(texture(tex["Emissive Texture"]), [c * sca.get("Emissive Power", 1.0) for c in vec.get("Emissive Color", [0, 0, 0, 0])])
            e["cull"] = "off"
        elif parent in ("MA_CH_Hair", "Mat_Hair_V1"):
            if parent == "MA_CH_Hair":
                lin = 0.5 * (np.array(vec.get("RootColor", [0.1, 0.07, 0.05, 1])[:3]) + np.array(vec.get("TipColor", [0.1, 0.04, 0.02, 1])[:3]))
                lin = lin * sca.get("Brightness", 1.0) * 2.0
            else:
                lin = 0.5 * (np.array(vec.get("Color Gradient A", [0.02, 0.01, 0.01, 1])[:3]) + np.array(vec.get("Color Gradient B", [0.09, 0.07, 0.07, 1])[:3]))
            a_path = texture(tex.get("Alpha")) or d.get("alpha_map")
            m = load(a_path, opts.size, "RGBA")
            arr = np.asarray(m, np.float32) / 255.0
            # the alpha map's coverage: its alpha when it has one, else its red
            alpha = arr[..., 3] if float(arr[..., 3].min()) < 0.99 else arr[..., 0]
            rgb = np.broadcast_to(linear_to_srgb(np.array(lin, np.float32)), alpha.shape + (3,)).copy()
            out = os.path.join(tex_out, f"{stem}_albedo.png")
            save_rgba(rgb, alpha, out)
            e["albedo"] = os.path.basename(out)
            e["surface"] = "cutout"
            e["cutoff"] = 0.35
            e["smoothness"] = 1.0 - sca.get("Roughness", 0.5)
            e["cull"] = "off"
            e["kind"] = "hair"
        elif parent == "testmaterial_eyebrow":
            m = np.asarray(load(texture(tex.get("Mask")), opts.size, "RGBA"), np.float32) / 255.0
            alpha = m[..., 0]
            rgb = np.broadcast_to(linear_to_srgb(np.array(vec.get("BaseColour", [0.01, 0.005, 0.001, 0])[:3], np.float32)), alpha.shape + (3,)).copy()
            out = os.path.join(tex_out, f"{stem}_albedo.png")
            save_rgba(rgb, alpha, out)
            e["albedo"] = os.path.basename(out)
            e["surface"] = "cutout"
            e["cutoff"] = 0.3
            e["cull"] = "off"
        elif kind == "eye":
            if d.get("albedo") and os.path.exists(d["albedo"]):
                put_albedo(d["albedo"])
            e["smoothness"] = 0.9
        elif d.get("albedo") and os.path.exists(d["albedo"]):
            put_albedo(d["albedo"])
            if d.get("normal") and os.path.exists(d["normal"]):
                put_normal(d["normal"])
            e["smoothness"] = 1.0 - float(d.get("roughness", 0.5))
        elif d.get("color"):
            c = linear_to_srgb(np.array(d["color"][:3], np.float32))
            e["color"] = [round(float(x), 4) for x in c] + [float(d.get("alpha", 1.0))]
            e["smoothness"] = 1.0 - float(d.get("roughness", 0.5))
        entries.append(e)
        print(f"{name}: {e['kind']} {e['surface']} - {e['source']}; albedo {e['albedo'] or e['color']} normal {e['normal'] or '-'} "
              f"mask {e['mask'] or '-'} emission {e['emission'] or '-'} {e['emissionColor'] if e['emission'] else ''}")
    with open(os.path.join(opts.folder, "unity.json"), "w", encoding="utf-8") as f:
        json.dump({"id": side["id"], "fbx": side["fbx"], "materials": entries}, f, indent=1)
    print(f"{len(entries)} materials -> {os.path.join(opts.folder, 'unity.json')}")


if __name__ == "__main__":
    main()
