using UnityEngine;

namespace Interstellar.Utilities
{
    internal static class PaintbrawlMath
    {
        internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);



        internal static bool TryIntercept(Vector3 origin, Vector3 target, Vector3 targetVelocity,
            Vector3 gravity, float speed, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!Finite(origin) || !Finite(target) || !Finite(targetVelocity) || !Finite(gravity)
                || !Finite(speed) || speed < 0.1f || speed > 150f) return false;
            Vector3 displacement = target - origin;
            if (displacement.sqrMagnitude < 0.001f || displacement.sqrMagnitude > 10000f) return false;
            float previousTime = 0.015f;
            float previousError = Error(displacement, targetVelocity, gravity, previousTime, speed);
            float bestTime = previousTime;
            float bestError = previousError;
            const int samples = 160;
            for (int sample = 1; sample <= samples; sample++)
            {
                float time = 0.015f + sample * (4f / samples);
                float error = Error(displacement, targetVelocity, gravity, time, speed);
                if (error < bestError) { bestError = error; bestTime = time; }
                if (previousError > 0f && error <= 0f)
                {
                    float lower = previousTime;
                    float upper = time;
                    for (int iteration = 0; iteration < 20; iteration++)
                    {
                        float middle = (lower + upper) * 0.5f;
                        if (Error(displacement, targetVelocity, gravity, middle, speed) > 0f) lower = middle;
                        else upper = middle;
                    }
                    float flight = (lower + upper) * 0.5f;
                    velocity = displacement / flight + targetVelocity - gravity * (flight * 0.5f);
                    return Finite(velocity) && Mathf.Abs(velocity.magnitude - speed) < Mathf.Max(0.01f, speed * 0.001f);
                }
                previousTime = time;
                previousError = error;
            }


            float left = Mathf.Max(0.015f, bestTime - 4f / samples);
            float right = Mathf.Min(4.015f, bestTime + 4f / samples);
            for (int iteration = 0; iteration < 28; iteration++)
            {
                float oneThird = left + (right - left) / 3f;
                float twoThirds = right - (right - left) / 3f;
                if (Error(displacement, targetVelocity, gravity, oneThird, speed)
                    < Error(displacement, targetVelocity, gravity, twoThirds, speed)) right = twoThirds;
                else left = oneThird;
            }
            float tangentTime = (left + right) * 0.5f;
            float tangentError = Error(displacement, targetVelocity, gravity, tangentTime, speed);
            if (Mathf.Abs(tangentError) <= Mathf.Max(0.0001f, speed * speed * 0.00001f))
            {
                velocity = displacement / tangentTime + targetVelocity - gravity * (tangentTime * 0.5f);
                float length = velocity.magnitude;
                if (Finite(velocity) && length > 0.001f)
                {
                    velocity *= speed / length;
                    return true;
                }
            }
            return false;
        }

        private static float Error(Vector3 displacement, Vector3 targetVelocity, Vector3 gravity, float time, float speed)
        {
            Vector3 required = displacement / time + targetVelocity - gravity * (time * 0.5f);
            return required.sqrMagnitude - speed * speed;
        }
    }
}
