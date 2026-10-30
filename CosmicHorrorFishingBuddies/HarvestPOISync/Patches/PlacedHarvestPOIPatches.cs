using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.PlayerSync;
using HarmonyLib;

namespace CosmicHorrorFishingBuddies.HarvestPOISync.Patches
{
	[HarmonyPatch(typeof(PlacedHarvestPOI))]
	internal class PlacedHarvestPOIPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch(nameof(PlacedHarvestPOI.Awake))]
		public static void PlacedHarvestPOI_Awake(PlacedHarvestPOI __instance)
		{
			// Pots loaded with the save wake up before this player has joined the network, so wait for it
			Delay.RunWhen(
				() => __instance == null || (CFBSpawnManager.Instance != null && NetworkPlayer.LocalPlayer != null),
				() =>
				{
					if (__instance != null) CFBSpawnManager.Instance.TrySpawnNetworkCrabPot(__instance);
				});
		}
	}
}
