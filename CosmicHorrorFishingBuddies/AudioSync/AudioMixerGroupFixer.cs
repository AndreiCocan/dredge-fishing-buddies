using CosmicHorrorFishingBuddies.Core;
using System.Linq;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.AudioSync
{
	/// <summary>
	/// Routes the mod's audio sources through the game's world SFX mixer group, so they follow the player's volume
	/// settings. The game loads its mixer asynchronously, so routing waits for it.
	/// </summary>
	internal static class AudioMixerGroupFixer
	{
		public static void RouteToWorldSFX(AudioSource audioSource)
		{
			Delay.RunWhen(
				() => audioSource == null || (GameManager.Instance != null && GameManager.Instance.AudioPlayer != null && GameManager.Instance.AudioPlayer.IsMixerLoaded),
				() =>
				{
					if (audioSource == null) return;

					var group = GameManager.Instance.AudioPlayer.AudioMixer.FindMatchingGroups("WorldSFX").FirstOrDefault();
					if (group == null)
					{
						CFBCore.LogError("The game's mixer has no WorldSFX group; other players' sounds ignore the volume settings");
						return;
					}
					audioSource.outputAudioMixerGroup = group;
				});
		}
	}
}
