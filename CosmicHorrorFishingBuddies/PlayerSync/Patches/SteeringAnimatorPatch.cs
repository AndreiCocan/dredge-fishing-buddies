using HarmonyLib;

namespace CosmicHorrorFishingBuddies.PlayerSync.Patches
{
	[HarmonyPatch(typeof(SteeringAnimator))]
	internal class SteeringAnimatorPatch
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(SteeringAnimator.Update))]
		public static void SteeringAnimatorPatch_Update(SteeringAnimator __instance)
		{
			if (NetworkPlayer.LocalPlayer != null) NetworkPlayer.LocalPlayer.remoteSteeringAnimator.UpdateSteering(__instance.moveValX, __instance.moveValY);
		}
	}
}
