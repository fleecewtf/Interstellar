using System.Collections.Generic;
using UnityEngine;
using static Interstellar.Menu.Main;

namespace Interstellar.Extensions
{
    public static class MiscellaneousExtensions
    {
        public static IEnumerable<GameObject> Children(this Transform t)
        {
            var list = new List<GameObject>();
            for (int i = 0; i < t.childCount; i++)
                list.Add(t.GetChild(i).gameObject);
            return list;
        }

        public static void Play(this AudioClip clip, float volume = 1f) =>
            Play2DAudio(clip, volume);
    }
}
