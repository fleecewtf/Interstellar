using Interstellar.Extensions;
using System.Collections.Generic;
using Interstellar.Classes.Menu;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Interstellar.Menu.Main;
using Object = UnityEngine.Object;

namespace Interstellar.Mods
{
    public class Visuals
    {
        public static void InstantiateWatch()
        {
            VRRig rig = VRRig.LocalRig;
            if (rig == null || rig.huntComputer == null) return;
            Transform hand = rig.transform.Find(rightHand ? "rig/hand.R" : "rig/hand.L");
            if (hand == null) return;

            GameObject watch = Object.Instantiate(rig.huntComputer, hand, false);
            GorillaHuntComputer computer = watch.GetComponent<GorillaHuntComputer>();
            Transform screen = watch.transform.Find("HuntWatch_Screen_DefaultAnchor/HuntWatch_ScreenLocal")
                ?? watch.transform.Find("HuntWatch_ScreenLocal");
            if (computer == null || computer.text == null || computer.leftHand == null || screen == null)
            {
                Object.Destroy(watch);
                return;
            }

            GameObject textObject = computer.text.gameObject;
            Image indicatorImage = computer.leftHand;
            foreach (Image image in new[] { computer.hat, computer.face, computer.badge,
                computer.material, computer.rightHand, computer.leftHand })
                if (image != null) image.gameObject.SetActive(false);

            Object.DestroyImmediate(computer);

            GameObject shell = screen.gameObject;

            if (rightHand)
            {
                shell.transform.localRotation = Quaternion.Euler(0f, 140f, 0f);
                shell.transform.parent.localPosition += new Vector3(0.025f, 0f, 0f);
                shell.transform.localPosition += new Vector3(0.025f, 0f, -0.035f);
            }

            Object.DestroyImmediate(textObject.GetComponent<Text>());

            TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
            MenuTypography.Configure(tmp);
            tmp.SafeSetFontSize(10);
            tmp.SafeSetWrappingMode(TextWrappingModes.Normal);
            tmp.SafeSetOverflowMode(TextOverflowModes.Truncate);

            Watches.Add(new WatchInfo
            {
                text = tmp,
                shell = shell,
                indicator = indicatorImage
            });
        }

        public static readonly List<VRRig> convertedRigs = new List<VRRig>();
        public static void FixRigMaterialESPColors(VRRig rig)
        {
            if (!convertedRigs.Contains(rig))
            {
                convertedRigs.Add(rig);

                rig.mainSkin.sharedMesh.colors32 = Enumerable.Repeat((Color32)Color.white, rig.mainSkin.sharedMesh.colors32.Length).ToArray();
                rig.mainSkin.sharedMesh.colors = Enumerable.Repeat(Color.white, rig.mainSkin.sharedMesh.colors.Length).ToArray();
            }
        }
    }
}
