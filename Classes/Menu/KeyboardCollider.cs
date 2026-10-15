using System.Collections.Generic;
using UnityEngine;
using static Interstellar.Menu.Main;

namespace Interstellar.Classes.Menu
{
    public class KeyboardKey : MonoBehaviour
    {
        public static readonly Dictionary<string, KeyboardKey> keyLookupDictionary = new Dictionary<string, KeyboardKey>();
        public string key;
        public static float delay;

        private Collider leftContact;
        private Collider rightContact;
        private readonly HeldKeyRepeat backspaceRepeat = new HeldKeyRepeat();
        private bool backspaceStarted;

        public void Start() =>
            keyLookupDictionary[gameObject.name] = this;

        public void OnTriggerEnter(Collider collider)
        {
            if (!ValidInput || !IsPointer(collider)) return;
            if (key == "Backspace")
            {
                RecordContact(collider);
                UpdateBackspace();
                return;
            }
            if (!(Time.time > delay)) return;
            Press(collider);
        }

        public void OnTriggerStay(Collider collider)
        {
            if (key == "Backspace" && ValidInput && IsPointer(collider))
                RecordContact(collider);
        }

        public void OnTriggerExit(Collider collider)
        {
            if (collider == leftContact) leftContact = null;
            if (collider == rightContact) rightContact = null;
            if (leftContact == null && rightContact == null) ResetBackspace();
        }

        private void LateUpdate()
        {
            if (key == "Backspace") UpdateBackspace();
        }

        private bool ValidInput => isActiveAndEnabled && menu != null && menu.activeInHierarchy
            && inTextInput && !isKeyboardPc;

        private static bool IsPointer(Collider collider) => collider != null
            && (collider == lKeyCollider || collider == rKeyCollider)
            && collider.enabled && collider.gameObject.activeInHierarchy;

        private void RecordContact(Collider collider)
        {
            if (collider == lKeyCollider) leftContact = collider;
            else if (collider == rKeyCollider) rightContact = collider;
        }

        private void UpdateBackspace()
        {
            if (!ValidInput)
            {
                ResetBackspace();
                return;
            }

            if (leftContact != lKeyCollider || !IsPointer(leftContact)) leftContact = null;
            if (rightContact != rKeyCollider || !IsPointer(rightContact)) rightContact = null;
            if (leftContact == null && rightContact == null)
            {
                ResetBackspace();
                return;
            }



            if (!backspaceStarted && !(Time.time > delay)) return;
            bool hasText = !string.IsNullOrEmpty(keyboardInput);
            if (!backspaceRepeat.Poll(true, Time.unscaledTime, hasText)) return;
            backspaceStarted = true;
            if (!hasText) return;
            Press(leftContact != null ? leftContact : rightContact);
        }

        private void ResetBackspace()
        {
            leftContact = null;
            rightContact = null;
            backspaceStarted = false;
            backspaceRepeat.Reset();
        }

        public void OnDisable() => ResetBackspace();

        private void Press(Collider collider)
        {
            delay = Time.time + 0.1f;

            if (doButtonsVibrate)
                GorillaTagger.Instance.StartVibration(collider == lKeyCollider, GorillaTagger.Instance.tagHapticStrength / 2f, GorillaTagger.Instance.tagHapticDuration / 2f);

            VRRig.LocalRig.PlayHandTapLocal(66, collider == lKeyCollider, buttonClickVolume / 10f);
            PressKeyboardKey(key);
        }
    }
}
