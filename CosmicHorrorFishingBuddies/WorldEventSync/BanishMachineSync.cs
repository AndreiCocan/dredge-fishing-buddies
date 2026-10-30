using CosmicHorrorFishingBuddies.PlayerSync;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.WorldEventSync;

/// <summary>
/// The banish machine's whole state is its expiry in game time. Every player keeps the latest expiry any of them set,
/// so one player switching it on protects everyone.
/// </summary>
[HarmonyPatch]
internal class BanishMachineSync : NetworkBehaviour
{
	public static BanishMachine Machine { get; private set; }

	[SyncVar]
	private float _expiry;

	private float _nextCheck;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(BanishMachine), nameof(BanishMachine.Start))]
	public static void BanishMachine_Start(BanishMachine __instance) => Machine = __instance;

	public void Update()
	{
		if (Time.unscaledTime < _nextCheck || NetworkPlayer.LocalPlayer == null || GameManager.Instance.SaveData == null) return;
		_nextCheck = Time.unscaledTime + 1f;

		var localExpiry = GameManager.Instance.SaveData.BanishMachineExpiry;
		if (localExpiry > _expiry)
		{
			CmdExtendExpiry(localExpiry);
		}
		else if (_expiry > localExpiry)
		{
			GameManager.Instance.SaveData.BanishMachineExpiry = _expiry;
			if (Machine != null && !Machine.isBanishMachineActive && GameManager.Instance.Time.TimeAndDay < _expiry)
			{
				Machine.ToggleBanishMachine(enabled: true);
			}
		}
	}

	[Command(requiresAuthority = false)]
	private void CmdExtendExpiry(float expiry)
	{
		if (expiry > _expiry) _expiry = expiry;
	}
}
