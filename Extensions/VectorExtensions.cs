using UnityEngine;

namespace Interstellar.Extensions
{
    public static class VectorExtensions
    {
        public static float Distance(this Vector3 point, Vector3 to) =>
            Vector3.Distance(point, to);
    }
}
