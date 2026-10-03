using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoeFighter.EditorTools
{
    /// <summary>
    /// What tools/import_ripped.py copied into Assets/ROE, per character id (a08, g04, ...).
    /// </summary>
    [Serializable]
    public class RoeManifest
    {
        public const string Path = "Assets/ROE/roe_manifest_unity.json";

        [Serializable]
        public class Clip
        {
            public string name;
            public string path;
        }

        [Serializable]
        public class VoiceSet
        {
            public string language;
            public List<string> paths = new List<string>();
        }

        [Serializable]
        public class Character
        {
            public string id;
            public string hdPrefab;
            public string hdAvatar;
            public string metaPrefab;
            public string ldPrefab;
            public List<Clip> clips = new List<Clip>();
            public List<string> weapons = new List<string>();
            public List<string> sfx = new List<string>();
            public List<VoiceSet> voices = new List<VoiceSet>();

            public string Family => id.Substring(0, 1);
            public bool HasModel => !string.IsNullOrEmpty(hdPrefab);

            public AnimationClip LoadClip(string name)
            {
                var c = clips.FirstOrDefault(x => x.name == name);
                return c == null ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(c.path);
            }
        }

        public List<Character> characters = new List<Character>();

        public static RoeManifest Load()
        {
            if (!File.Exists(Path))
                throw new FileNotFoundException("Run tools/import_ripped.py first", Path);
            return JsonUtility.FromJson<RoeManifest>(File.ReadAllText(Path));
        }

        public Character Find(string id)
        {
            var c = characters.FirstOrDefault(x => x.id == id);
            if (c == null)
                throw new ArgumentException("No character '" + id + "' in " + Path);
            return c;
        }

        public IEnumerable<Character> WithModels => characters.Where(c => c.HasModel);

        /// <summary>The characters named by -roeChars b10,g05 (batch argument), else every one with a model.</summary>
        public IEnumerable<Character> Chosen()
        {
            var ids = RoeCapture.Arg("-roeChars", null);
            return string.IsNullOrEmpty(ids) ? WithModels : ids.Split(',').Select(id => Find(id.Trim())).Where(c => c.HasModel);
        }
    }
}
