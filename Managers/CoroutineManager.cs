using UnityEngine;

namespace Interstellar.Managers
{
    public class CoroutineManager : MonoBehaviour
    {
        public static CoroutineManager instance;

        private void Awake() =>
            instance = this;
    }
}
