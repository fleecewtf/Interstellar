using GorillaNetworking;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using static Interstellar.Menu.Main;

namespace Interstellar.Mods
{
    public static class Important
    {
        private static bool clickWasDown;
        private static float clickReadyAt;
        private static Camera desktopCamera;
        private static readonly Dictionary<Type, MethodInfo> triggerMethods = new Dictionary<Type, MethodInfo>();

        public static void PCButtonClick()
        {
            Mouse mouse = Mouse.current;
            bool pressed = mouse?.leftButton.isPressed == true;
            bool newPress = pressed && !clickWasDown;


            clickWasDown = pressed;
            if (!newPress || Time.unscaledTime < clickReadyAt || !Application.isFocused ||
                menu != null || inTextInput || CurrentPrompt != null || Interstellar.Menu.UI.HasKeyboardFocus || CameraTools.FreeCameraActive ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;

            GorillaTagger tagger = GorillaTagger.Instance;
            if (tagger == null || mouse == null) return;
            Camera camera = DesktopCamera(tagger);
            if (camera == null) return;
            GameObject triggerObject = tagger.rightHandTriggerCollider;
            Collider trigger = triggerObject == null ? null : triggerObject.GetComponent<Collider>();
            if (trigger == null) return;

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, float.MaxValue, ClickableMask(), QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || !TryClick(hit.collider, trigger, hit.point)) continue;
                clickReadyAt = Time.unscaledTime + 0.1f;
                return;
            }
        }

        private static bool TryClick(Collider hitCollider, Collider trigger, Vector3 hitPoint)
        {
            Transform current = hitCollider.transform;
            for (int depth = 0; current != null && depth < 6; depth++, current = current.parent)
                foreach (Component component in current.GetComponents<Component>())
                {
                    if (component == null || !component.gameObject.activeInHierarchy ||
                        (component is Behaviour behaviour && !behaviour.enabled)) continue;
                    if (component is GorillaKeyboardButton keyboard)
                    {
                        GameEvents.OnGorrillaKeyboardButtonPressedEvent.Invoke(keyboard.Binding);
                        return true;
                    }

                    Type type = component.GetType();
                    if (!(component is GorillaPressableButton) && !(component is GorillaPlayerLineButton) &&
                        type.Name != "CustomKeyboardKey") continue;
                    if (!triggerMethods.TryGetValue(type, out MethodInfo enter))
                    {
                        enter = AccessTools.Method(type, "OnTriggerEnter", new[] { typeof(Collider) });
                        triggerMethods[type] = enter;
                    }
                    if (enter == null) continue;
                    InvokeAtPoint(component, enter, trigger, hitPoint);
                    return true;
                }
            return false;
        }

        private static void InvokeAtPoint(Component component, MethodInfo enter, Collider trigger, Vector3 hitPoint)
        {
            Transform triggerTransform = trigger.transform;
            Vector3 localPosition = triggerTransform.localPosition;
            TransformFollow follow = trigger.GetComponent<TransformFollow>();
            bool followEnabled = follow != null && follow.enabled;
            try
            {
                if (follow != null) follow.enabled = false;
                triggerTransform.position = hitPoint;
                enter.Invoke(component, new object[] { trigger });
            }
            finally
            {
                if (triggerTransform != null) triggerTransform.localPosition = localPosition;
                if (follow != null) follow.enabled = followEnabled;
            }
        }

        private static Camera DesktopCamera(GorillaTagger tagger)
        {
            if (TPC != null && TPC.isActiveAndEnabled) return TPC;
            if (desktopCamera != null && desktopCamera.isActiveAndEnabled) return desktopCamera;
            GameObject shoulder = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera") ??
                GameObject.Find("Shoulder Camera");
            desktopCamera = shoulder == null ? null : shoulder.GetComponent<Camera>();
            if ((desktopCamera == null || !desktopCamera.isActiveAndEnabled) && tagger.mainCamera != null)
                desktopCamera = tagger.mainCamera.GetComponent<Camera>();
            if (desktopCamera == null || !desktopCamera.isActiveAndEnabled) desktopCamera = Camera.main;
            return desktopCamera != null && desktopCamera.isActiveAndEnabled ? desktopCamera : null;
        }

        private static int ClickableMask()
        {
            int mask = ~0;
            foreach (string name in new[]
            {
                "TransparentFX", "Ignore Raycast", "Zone", "Gorilla Trigger",
                "Gorilla Boundary", "GorillaCosmetics", "GorillaParticle"
            })
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0) mask &= ~(1 << layer);
            }
            return mask;
        }

        public static void DisablePCButtonClick()
        {
            clickWasDown = Mouse.current?.leftButton.isPressed == true;
            clickReadyAt = 0f;
            desktopCamera = null;
            triggerMethods.Clear();
        }
    }
}
