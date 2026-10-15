using UnityEngine.InputSystem;

namespace Interstellar.Managers
{
    internal static class UnityInput
    {
        internal static bool GetKey(Key key) => Keyboard.current[key].isPressed;
    }
}
