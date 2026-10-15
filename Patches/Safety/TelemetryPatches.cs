using HarmonyLib;
using JetBrains.Annotations;
using Liv.Lck.Telemetry;
using PlayFab;
using PlayFab.EventsModels;
using System.Collections.Generic;
using static Interstellar.Patches.PatchHandler;

namespace Interstellar.Patches.Safety
{
    public class TelemetryPatches
    {
        [PatchOnAwake]
        [HarmonyPatch(typeof(GorillaTelemetry), nameof(GorillaTelemetry.EnqueueTelemetryEvent))]
        public class EnqueueTelemetryEvent
        {
            private static bool Prefix(string eventName, object content, [CanBeNull] string[] customTags = null) =>
                false;
        }

        [PatchOnAwake]
        [HarmonyPatch(typeof(GorillaTelemetry), nameof(GorillaTelemetry.FlushMothershipTelemetry))]
        public class FlushMothershipTelemetry
        {
            private static bool Prefix() =>
                false;
        }

        [PatchOnAwake]
        [HarmonyPatch(typeof(LckTelemetryClient), nameof(LckTelemetryClient.SendTelemetry))]
        public class SendTelemetry
        {
            private static bool Prefix(LckTelemetryEvent lckTelemetryEvent) =>
                false;
        }

        [PatchOnAwake]
        [HarmonyPatch(typeof(PlayFabEventsAPI), nameof(PlayFabEventsAPI.WriteTelemetryEvents))]
        public class WriteTelemetryEvents
        {
            private static bool Prefix(WriteEventsRequest request, System.Action<WriteEventsResponse> resultCallback, System.Action<PlayFabError> errorCallback, object customData = null, Dictionary<string, string> extraHeaders = null) =>
                false;
        }
    }
}
