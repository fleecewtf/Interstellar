using UnityEngine;
using UnityEngine.UI;

namespace Interstellar.Classes.Menu
{
    public class UIColorChanger : MonoBehaviour
    {
        public void Start()
        {
            if (colors == null)
            {
                Destroy(this);
                return;
            }

            targetGraphic = gameObject.GetComponent<MaskableGraphic>();

            if (colors.IsFlat())
            {
                Update();
                Destroy(this);
                return;
            }

            Update();
        }

        public void Update() =>
            targetGraphic.color = colors.GetCurrentColor();

        public MaskableGraphic targetGraphic;
        public ExtGradient colors;
    }
}
