using Interstellar.Managers;
using UnityEngine;
using static Interstellar.Menu.Main;

namespace Interstellar.Classes.Menu
{
    public class ButtonCollider : MonoBehaviour
    {
        public string relatedText;

        public bool incremental;
        public bool positive;

        public void OnTriggerEnter(Collider collider)
        {
            if (collider == null || !(Time.time > buttonCooldown) ||
                (collider != buttonCollider && collider != lKeyCollider && collider != rKeyCollider) || joystickMenu ||
                menu == null || !isActiveAndEnabled || !transform.IsChildOf(menu.transform)) return;
            buttonCooldown = Time.time + 0.2f;
            panelPressTime = Time.time;
            SoundManager.Play(SoundManager.DefaultSounds["Button"], buttonText: relatedText);

            if (annoyingMode)
            {
                if (Random.Range(1, 5) == 2)
                {
                    NotificationManager.SendNotification("Error");
                    return;
                }
            }

            if (incremental)
                ToggleIncremental(relatedText, positive);
            else
                Toggle(relatedText, true);
        }
    }
}
