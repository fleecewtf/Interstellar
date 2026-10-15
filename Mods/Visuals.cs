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
            GameObject mainwatch = VRRig.LocalRig.transform.Find("rig/hand.L/huntcomputer (1)").gameObject;
            GameObject watch = Object.Instantiate(mainwatch,
                rightHand ? VRRig.LocalRig.transform.Find("rig/hand.R").transform
                          : VRRig.LocalRig.transform.Find("rig/hand.L").transform, false);

            Object.DestroyImmediate(watch.GetComponent<GorillaHuntComputer>());

            Transform anchor = watch.transform.Find("HuntWatch_ScreenLocal/Canvas/Anchor");
            anchor.Find("Hat").gameObject.SetActive(false);
            anchor.Find("Face").gameObject.SetActive(false);
            anchor.Find("Badge").gameObject.SetActive(false);
            anchor.Find("Material").gameObject.SetActive(false);
            anchor.Find("Right Hand").gameObject.SetActive(false);
            anchor.Find("Left Hand").gameObject.SetActive(false);

            GameObject shell = watch.transform.Find("HuntWatch_ScreenLocal").gameObject;

            if (rightHand)
            {
                shell.transform.localRotation = Quaternion.Euler(0f, 140f, 0f);
                shell.transform.parent.localPosition += new Vector3(0.025f, 0f, 0f);
                shell.transform.localPosition += new Vector3(0.025f, 0f, -0.035f);
            }

            GameObject textObject = anchor.Find("Text").gameObject;
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
                indicator = anchor.Find("Left Hand").gameObject.GetComponent<Image>()
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
