using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Interstellar.Patches.Safety
{
    public class RPCProtection
    {
        [HarmonyPatch(typeof(LoadBalancingClient), nameof(LoadBalancingClient.OpRaiseEvent))]
        public class OpRaiseEventPatch
        {
            public static bool enabled = true;
            const int MaxRPCs = 500;
            internal static float startTime = Time.unscaledTime;
            internal static int rpcCount = 0;
            private static bool Prefix(byte eventCode, object customEventContent, RaiseEventOptions raiseEventOptions, SendOptions sendOptions)
            {
                if (enabled)
                {
                    float currentTime = Time.unscaledTime;
                    if (currentTime - startTime > 1f)
                    {
                        startTime = currentTime;
                        rpcCount = 0;
                    }
                    rpcCount++;
                    if (rpcCount > MaxRPCs)
                    {
                        if ((eventCode == PunEvent.RPC || eventCode == 201) && customEventContent != null && customEventContent is Hashtable rpcData)
                        {
                            foreach (var key in rpcData.Keys)
                            {
                                if (key is byte keyByte && keyByte == 0)
                                {
                                    if (rpcData[key] is string rpcName)
                                    {
                                        Debug.LogWarning($"Blocked RPC {rpcName} as we are sending too much traffic over the network!");
                                    }
                                    break;
                                }
                            }
                        }
                        else
                            Debug.LogWarning($"Blocked event {eventCode} as we are sending too much traffic over the network!");
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
