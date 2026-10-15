using GorillaLocomotion;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Interstellar.Utilities
{
    public class ControllerUtilities
    {
        public enum ControllerType
        {
            Unknown,
            Quest2,
            Quest3,
            ValveIndex,
            VIVE
        }

        private static readonly Dictionary<bool, ControllerInfo> controllerInfo = new Dictionary<bool, ControllerInfo>
        {
            { true, new ControllerInfo { type = ControllerType.Unknown, dataCacheTime = -1f } },
            { false, new ControllerInfo { type = ControllerType.Unknown, dataCacheTime = -1f } }
        };

        private static readonly Dictionary<string, ControllerType> controllerNames = new Dictionary<string, ControllerType>
        {
            { "quest2", ControllerType.Quest2 },
            { "quest3", ControllerType.Quest3 },
            { "knuckles", ControllerType.ValveIndex },
            { "vive", ControllerType.VIVE }
        };

        private struct ControllerInfo
        {
            public ControllerType type;
            public float dataCacheTime;
        }

        public static ControllerType GetControllerType(bool left)
        {
            try
            {
                if (!XRSettings.isDeviceActive)
                    return ControllerType.Unknown;

                var controller = left
                    ? ControllerInputPoller.instance.leftControllerDevice
                    : ControllerInputPoller.instance.rightControllerDevice;

                if (controllerInfo.TryGetValue(left, out var info) && !(Time.time > info.dataCacheTime + 1f))
                    return info.type;
                var controllerName = controller.name;
                var controllerType = ControllerType.Unknown;

                foreach (var kvp in controllerNames)
                {
                    if (controllerName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    controllerType = kvp.Value;
                    break;
                }

                info = new ControllerInfo
                {
                    type = controllerType,
                    dataCacheTime = Time.time
                };
                controllerInfo[left] = info;

                return info.type;
            }
            catch
            {
                return ControllerType.Unknown;
            }
        }

        public static ControllerType GetLeftControllerType() => GetControllerType(true);

        public static ControllerType GetRightControllerType() => GetControllerType(false);

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right) GetTrueHandPosition(bool left)
        {
            Transform controllerTransform = left ? GorillaTagger.Instance.leftHandTransform : GorillaTagger.Instance.rightHandTransform;
            GTPlayer.HandState handState = left ? GTPlayer.Instance.LeftHand : GTPlayer.Instance.RightHand;

            Quaternion rot = controllerTransform.rotation * handState.handRotOffset;
            return (controllerTransform.position + controllerTransform.rotation * (handState.handOffset * GTPlayer.Instance.scale), rot, rot * Vector3.up, rot * Vector3.forward, rot * Vector3.right);
        }

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right)
            GetTrueLeftHand() => GetTrueHandPosition(true);

        public static (Vector3 position, Quaternion rotation, Vector3 up, Vector3 forward, Vector3 right)
            GetTrueRightHand() => GetTrueHandPosition(false);
    }
}
