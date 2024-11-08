using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace LandFromOrbit.Patches;

[HarmonyPatch]
public class LandingPatch
{

    private static float _cachedSpeed = 1f;
    private static readonly int ShipOpen = Animator.StringToHash("ShipOpen");
    private static readonly int OpenShip = Animator.StringToHash("OpenShip");
    private static readonly int ShipLeave = Animator.StringToHash("ShipLeave");

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SceneManager_OnLoad))]
    private static class OnSceneLoad{
        private static void Prefix(StartOfRound __instance, ref bool __state)
        {
            __state = __instance.currentPlanetPrefab.activeSelf;
        }
        
        private static void Postfix(StartOfRound __instance, ref bool __state)
        {
            var animator = __instance.shipAnimatorObject.gameObject.GetComponent<Animator>();
            var audio = __instance.elevatorTransform.Find("ThrusterAmbientAudio")?.GetComponent<AudioSource>();
            _cachedSpeed = animator.speed;
            
            if (__state && !__instance.currentPlanetPrefab.activeSelf)
            {
                animator.speed = 0f;
                animator.Play(ShipOpen);

                if (audio)
                {
                    audio.mute = true;
                }
                
                LandFromOrbit.Log.LogInfo("Triggering animator");
            }
        }
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.openingDoorsSequence), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> PatchDoorSequence(IEnumerable<CodeInstruction> instructions,
        ILGenerator ilGenerator)
    {
        var codes = instructions.ToList();

        var doorButtonsMethod = AccessTools.Method(typeof(HangarShipDoor), nameof(HangarShipDoor.SetDoorButtonsEnabled));
        var target = AccessTools.Method(typeof(LandingPatch), nameof(OnLandingSequence));

        var matcher = new CodeMatcher(codes, ilGenerator);

        matcher.MatchForward(true, new CodeMatch(OpCodes.Callvirt, doorButtonsMethod));

        if (matcher.IsInvalid)
        {
            LandFromOrbit.Log.LogError($"Failed to patch\n{string.Join("\n", codes)}");
            return codes;
        }

        matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Call, target));
        
        LandFromOrbit.Log.LogDebug("Patched openingDoorsSequence");

        return matcher.Instructions();
    }
    
    private static void OnLandingSequence()
    {
        LandFromOrbit.Log.LogInfo("Resetting animator speed");
        var startOfRound = StartOfRound.Instance;
        var animator = startOfRound.shipAnimatorObject.gameObject.GetComponent<Animator>();
        var audio = startOfRound.elevatorTransform.Find("ThrusterAmbientAudio")?.GetComponent<AudioSource>();
        animator.ResetTrigger(OpenShip);
        animator.speed = _cachedSpeed;
        if (audio)
        {
            audio.mute = false;
            audio.Play();
        }
    }
    
}
