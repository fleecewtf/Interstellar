using Interstellar.Menu;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;

namespace Interstellar.Utilities
{
    internal static class FeatureInput
    {
        internal static bool GunFireHeld => Main.shouldBePC ? Mouse.current?.leftButton.isPressed == true : Main.rightTrigger > 0.5f;
        internal static bool CanUseGameInput =>
            (XRSettings.isDeviceActive || Application.isFocused) && Main.menu == null &&
            !Main.inTextInput && Main.CurrentPrompt == null && !UI.HasKeyboardFocus;
    }
}
