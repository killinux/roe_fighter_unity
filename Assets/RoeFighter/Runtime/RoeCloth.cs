using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoeFighter
{
    /// <summary>
    /// A cloth simulation the fighter can swap (F4 in the fight, like the motion packs on F3).  It runs on
    /// the finished pose of each step: Rest() before the animation is evaluated (bones no clip animates go
    /// back to rest), Step() after the pose (and the skirt's skinned pose) is done.
    /// </summary>
    public interface IRoeCloth
    {
        string Report { get; }
        /// <summary>How much of the simulation is written over the animation (0 = none: the game's own clips).</summary>
        float Weight { get; set; }
        void Rest();
        void Step(float dt, float floor);
        /// <summary>Start again from the animated pose (a teleport, a new round).</summary>
        void Reset();
    }

    /// <summary>
    /// The cloth backends, in the order F4 goes through them.
    ///   magica_style  RoeBoneCloth the way Magica Cloth 2 does it (10-03): the skirt's animation pose
    ///                 follows the legs (RoeSkirtRig), chains that are one piece of cloth are linked
    ///                 across, tether, world and local inertia (docs/magica-cloth-2.md)
    ///   legacy        RoeBoneCloth as it was on 10-02 (e4b0b28): independent chains, the skirt pinned
    ///                 to the pelvis from the guard
    ///   off           no simulation: skirts and hair keep the battle stance's keys
    /// Magica Cloth 2 itself goes in here once it is installed: a class implementing IRoeCloth on its
    /// MagicaCloth components (BoneCloth, mesh connection, the same colliders), registered below behind
    /// the MAGICACLOTH2 define the package adds.  The skirt's skinned pose works for it too: Magica takes
    /// the pose it finds on the bones as the animation pose.
    /// </summary>
    public static class RoeClothBackends
    {
        public class Backend
        {
            public string name, title;
            public bool skinnedSkirt;           // RoeSkirtRig turns the skirt to follow the legs first
            public Func<Animator, Transform, ICollection<Transform>, IRoeCloth> make;
        }

        public static readonly List<Backend> All = new List<Backend>
        {
            new Backend
            {
                name = "magica_style", title = "Magica-style bone cloth", skinnedSkirt = true,
                make = (animator, world, exclude) => new RoeBoneCloth(animator, world, exclude, legacy: false),
            },
            new Backend
            {
                name = "legacy", title = "Bone cloth (10-02)", skinnedSkirt = false,
                make = (animator, world, exclude) => new RoeBoneCloth(animator, world, exclude, legacy: true),
            },
            new Backend { name = "off", title = "No cloth", skinnedSkirt = false, make = null },
#if MAGICACLOTH2 && ROE_MAGICA
            new Backend
            {
                name = "magica", title = "Magica Cloth 2", skinnedSkirt = true,
                make = (animator, world, exclude) => new RoeMagicaCloth(animator, world, exclude),
            },
#endif
        };

        public static Backend Find(string name) => All.FirstOrDefault(b => b.name == name) ?? All[0];

        public static string Next(string name)
        {
            int i = All.FindIndex(b => b.name == name);
            return All[(i + 1) % All.Count].name;
        }
    }
}
