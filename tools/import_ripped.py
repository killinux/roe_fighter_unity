# Copy the AssetRipper export of the staged ROE bundles into the Unity project (Assets/ROE),
# point the game's materials at this project's shaders, and write a manifest the editor
# scripts read.  Re-runnable: unchanged files are skipped.
#
#   python tools/import_ripped.py                       # _work/ripped -> Assets/ROE
#   python tools/import_ripped.py --src <ExportedProject/Assets> --dst <Assets/ROE>
#
# What is NOT copied: the game's shader stubs (AssetRipper cannot recover shader code; the
# materials are re-pointed to the shaders in Assets/RoeFighter/Shaders), script stubs,
# Cinemachine / audio-mixer packages.
import argparse
import hashlib
import io
import json
import os
import re
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)

SKIP_BUNDLES = {'heros_shader_collection', 'package_cinemachine_prelude', 'audio_mixer_runtime', 'heros_builtin_asset'}

# game shader name -> this project's shader file (Assets/RoeFighter/Shaders/<file>.shader)
SHADER_MAP = {
    'Pinkcore/Heros/ErosLit/Character': 'RoeCharacter',
    'Pinkcore/Heros/SimpleLit/Character': 'RoeCharacter',
    'Pinkcore/Heros/Skin': 'RoeSkin',
    'Pinkcore/Heros/KajiyaKayHair': 'RoeHair',
    'Pinkcore/Heros/SimpleLit/Hair': 'RoeHair',
    'Pinkcore/Heros/Eye': 'RoeEye',
    'Pinkcore/Heros/Eyebrow': 'RoeEyebrow',
    'Pinkcore/Heros/ErosLit/Environment': 'RoeEnvironment',
    'Pinkcore/Heros/SimpleLit/Environment': 'RoeEnvironment',
    'Pinkcore/Skybox/FogCubemap': 'RoeSkybox',
    # effects (rebuilt from the disassembly of the game's shaders, see tools/shader_asm.py)
    'Pinkcore/Particles/Default': 'RoeParticleDefault',
    'Pinkcore/Particles/Dissolve': 'RoeParticleDissolve',
    'Pinkcore/Particles/Unlit': 'RoeParticleUnlit',
    'Pinkcore/Particles/Distortion': 'RoeParticleDistortion',
    'Pinkcore/Particles/MutateDistortion': 'RoeParticleMutateDistortion',
    'Pinkcore/Particles/UnlitMaster': 'RoeParticleUnlitMaster',
}

CLIP_ALIASES = {  # the game's own spelling slips
    'skill01': 'skill_01', 'skill02': 'skill_02', 'skill03': 'skill_03',
    'skil_l01': 'skill_01', 'skil_l02': 'skill_02', 'skil_l03': 'skill_03',
    'idlel_01': 'idle_01', 'dle': 'die',
}


def shader_guid(file_stem):
    """Fixed GUID of one of this project's shaders (also written into its .meta)."""
    return hashlib.md5(('roe_fighter_unity/shader/' + file_stem).encode()).hexdigest()


def group_of(bundle):
    m = re.search(r'pc_([a-m]\d\d)(?![0-9])', bundle)
    if m:
        return m.group(1)
    m = re.search(r'pc_([a-m])_common', bundle)
    if m:
        return m.group(1) + '_common'
    return 'common'


def clean(name):
    return name.replace(' & ', '_').replace(' ', '_')


def read_guid(meta):
    t = open(meta, encoding='utf-8', errors='replace').read(400)
    m = re.search(r'guid: ([0-9a-f]{32})', t)
    return m.group(1) if m else None


def main():
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=os.path.join(PROJECT, '_work', 'ripped', 'ExportedProject', 'Assets'))
    ap.add_argument('--dst', default=os.path.join(PROJECT, 'Assets', 'ROE'))
    ap.add_argument('--keep-manifest', action='store_true',
                    help='leave roe_manifest*.json as they are (an extra rip, e.g. the nude bases, next to the fighters)')
    a = ap.parse_args()
    src_ab = os.path.join(a.src, 'AssetBundles')

    # game shader stub guid -> our shader guid
    remap = {}
    shader_dir = os.path.join(src_ab, 'heros_shader_collection')
    for d, _dirs, files in os.walk(shader_dir):
        for f in files:
            if f.endswith('.shader'):
                text = open(os.path.join(d, f), encoding='utf-8', errors='replace').read(300)
                m = re.search(r'Shader "([^"]+)"', text)
                g = read_guid(os.path.join(d, f + '.meta'))
                if m and g and m.group(1) in SHADER_MAP:
                    remap[g] = (m.group(1), shader_guid(SHADER_MAP[m.group(1)]))
    print(f'{len(remap)} game shaders mapped to project shaders')

    copied = skipped = remapped = 0
    unmapped = {}
    manifest = {}
    for bundle in sorted(os.listdir(src_ab)):
        bdir = os.path.join(src_ab, bundle)
        if not os.path.isdir(bdir) or bundle in SKIP_BUNDLES:
            continue
        group = group_of(bundle)
        out_dir = os.path.join(a.dst, group, clean(bundle))
        for d, _dirs, files in os.walk(bdir):
            rel = os.path.relpath(d, bdir)
            tdir = out_dir if rel == '.' else os.path.join(out_dir, rel)
            os.makedirs(tdir, exist_ok=True)
            for f in files:
                s = os.path.join(d, f)
                t = os.path.join(tdir, f)
                if f.endswith('.mat'):
                    text = open(s, encoding='utf-8').read()
                    m = re.search(r'm_Shader: \{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}', text)
                    if m and m.group(2) in remap:
                        text = text.replace(m.group(0), 'm_Shader: {fileID: 4800000, guid: %s, type: 3}' % remap[m.group(2)][1])
                        remapped += 1
                    elif m:
                        unmapped.setdefault(m.group(2), []).append(f)
                    if not (os.path.exists(t) and open(t, encoding='utf-8').read() == text):
                        open(t, 'w', encoding='utf-8', newline='\n').write(text)
                        copied += 1
                    else:
                        skipped += 1
                    continue
                if os.path.exists(t) and os.path.getsize(t) == os.path.getsize(s) and os.path.getmtime(t) >= os.path.getmtime(s):
                    skipped += 1
                    continue
                shutil.copy2(s, t)
                copied += 1
        # the folder itself needs a .meta only if AssetRipper wrote one; Unity creates the rest

        # manifest
        unity_dir = 'Assets/ROE/%s/%s' % (group, clean(bundle))
        names = [f for f in os.listdir(bdir) if not f.endswith('.meta')]
        entry = manifest.setdefault(group, {'clips': {}, 'sfx': [], 'voice': {}, 'weapons': []})
        cid = group
        if bundle.startswith('chara_armor_pc_') and re.search(r'_hd$', bundle) and '_outfit' not in bundle and '_common_' not in bundle:
            if ('pc_%s_hd.prefab' % cid) in names:
                entry['hd_prefab'] = '%s/pc_%s_hd.prefab' % (unity_dir, cid)
                entry['hd_avatar'] = '%s/pc_%s_hdAvatar.asset' % (unity_dir, cid)
            for n in names:
                if n.endswith('.anim'):
                    stem = CLIP_ALIASES.get(n[:-5], n[:-5])
                    entry['clips'][stem] = '%s/%s' % (unity_dir, n)
                if n.startswith('wp_') and n.endswith('.prefab'):
                    entry['weapons'].append('%s/%s' % (unity_dir, n))
        elif bundle.startswith('chara_armor_pc_') and re.search(r'_ld(_prelude|_tutorial)?$', bundle) and '_outfit' not in bundle and '_common_' not in bundle:
            if ('pc_%s_ld.prefab' % cid) in names:
                entry['ld_prefab'] = '%s/pc_%s_ld.prefab' % (unity_dir, cid)
            for n in names:
                if n.endswith('.anim'):
                    stem = CLIP_ALIASES.get(n[:-5], n[:-5])
                    entry['clips'][stem] = '%s/%s' % (unity_dir, n)
        elif bundle.startswith('meta_armor_pc_') and '_outfit' not in bundle and 'localize' not in bundle:
            if ('pc_%s_hd.prefab' % cid) in names:
                entry['meta_prefab'] = '%s/pc_%s_hd.prefab' % (unity_dir, cid)
        elif bundle.startswith('meta_armor_pc_') and '_outfit' in bundle and 'localize' not in bundle:
            for n in names:
                if n.endswith('.prefab'):
                    entry.setdefault('outfit_meta_prefabs', []).append('%s/%s' % (unity_dir, n))
        elif bundle.startswith('sfx_battlefield_'):
            entry['sfx'] += ['%s/%s' % (unity_dir, n) for n in sorted(names) if n.endswith(('.ogg', '.wav'))]
        elif bundle.startswith('voice_battlefield_'):
            lang = bundle.split('_')[2]
            entry['voice'].setdefault(lang, [])
            entry['voice'][lang] += ['%s/%s' % (unity_dir, n) for n in sorted(names) if n.endswith(('.ogg', '.wav'))]

    manifest = {k: v for k, v in manifest.items() if any(v.get(x) for x in ('hd_prefab', 'clips', 'sfx', 'voice'))}
    if a.keep_manifest:
        print(f'copied {copied}, unchanged {skipped}, materials re-pointed {remapped}; manifest left as it was')
        for g, mats in unmapped.items():
            print(f'  shader {g} has no project shader: {", ".join(sorted(mats))}')
        return
    os.makedirs(a.dst, exist_ok=True)
    json.dump(manifest, open(os.path.join(a.dst, 'roe_manifest.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    # the same data as lists: Unity's JsonUtility cannot read dictionaries
    unity = {'characters': []}
    for cid, e in sorted(manifest.items()):
        unity['characters'].append({
            'id': cid,
            'hdPrefab': e.get('hd_prefab', ''), 'hdAvatar': e.get('hd_avatar', ''),
            'metaPrefab': e.get('meta_prefab', ''), 'ldPrefab': e.get('ld_prefab', ''),
            'clips': [{'name': k, 'path': v} for k, v in sorted(e['clips'].items())],
            'weapons': e['weapons'], 'sfx': e['sfx'],
            'voices': [{'language': k, 'paths': v} for k, v in sorted(e['voice'].items())],
        })
    json.dump(unity, open(os.path.join(a.dst, 'roe_manifest_unity.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(f'copied {copied}, unchanged {skipped}, materials re-pointed {remapped}')
    for g, mats in unmapped.items():
        print(f'  shader {g} has no project shader: {", ".join(sorted(mats))}')
    for cid, e in sorted(manifest.items()):
        print(f'  {cid}: hd={bool(e.get("hd_prefab"))} meta={bool(e.get("meta_prefab"))} ld={bool(e.get("ld_prefab"))} '
              f'clips={sorted(e["clips"])} weapons={len(e["weapons"])} sfx={len(e["sfx"])} '
              f'voice={ {k: len(v) for k, v in e["voice"].items()} }')


if __name__ == '__main__':
    main()
