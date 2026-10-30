using CosmicHorrorFishingBuddies.AudioSync;
using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.PlayerSync.AbilitySync.Base;
using CosmicHorrorFishingBuddies.Util;
using Mirror;
using System;

namespace CosmicHorrorFishingBuddies.PlayerSync.AbilitySync
{
	internal class RemoteLightAbility : RemoteSyncVarAbility
	{
		public override Type AbilityType => typeof(LightAbility);

		public override void Start()
		{
			_networkPlayer.remotePlayerBoatGraphics.RefreshBoatModel.AddListener(RefreshLights);
			base.Start();
		}

		public void OnDestroy()
		{
			_networkPlayer.remotePlayerBoatGraphics.RefreshBoatModel.RemoveListener(RefreshLights);
		}

		protected override void OnToggleRemote(bool active)
		{
			_networkPlayer.RemotePlayOneShot(active ? AudioEnum.LIGHT_ON : AudioEnum.LIGHT_OFF, 0.3f, 1f);
			RefreshLights();
		}

		[Command]
		public void FlickerLights() => ClientRpcFlickerLights();


		// The flickering player's own game already ran the event
		[ClientRpc(includeOwner = false)]
		private void ClientRpcFlickerLights()
		{
			try
			{
				var flickerLightsWorldEvent = EventHelper.GetWorldEvent<FlickerLightsWorldEvent>();
				_networkPlayer.RemotePlayOneShot(AudioEnum.LIGHT_FLICKER, flickerLightsWorldEvent.flickerVolume, 1f);

				// The remote boat swaps models as upgrades sync, so take the effect from the current one
				var boat = _networkPlayer.remotePlayerBoatGraphics.CurrentBoatModelProxy;
				if (boat == null) return;
				boat.LightFlickerEffect.BeginFlicker(flickerLightsWorldEvent.flickerCurve, flickerLightsWorldEvent.worldEventData.durationSec, false);
			}
			catch (Exception e)
			{
				CFBCore.LogError(e);
			}
		}

		public void RefreshLights()
		{
			Delay.RunWhen(
				() => _networkPlayer.remotePlayerBoatGraphics.boatModelProxies != null && _networkPlayer.remotePlayerBoatGraphics.boatModelProxies.Length > 0, 
				() => {
					try
					{
						_networkPlayer.remotePlayerBoatGraphics.CurrentBoatModelProxy.SetLightStrength(IsActive ? 4f : 0f);

						foreach (var light in _networkPlayer.remotePlayerBoatGraphics.CurrentBoatModelProxy.Lights)
						{
							light.SetActive(IsActive);
						}
						foreach (var lightBeam in _networkPlayer.remotePlayerBoatGraphics.CurrentBoatModelProxy.LightBeams)
						{
							lightBeam.SetActive(IsActive);
						}
					}
					catch (Exception e)
					{
						CFBCore.LogError($"Failed to refresh lights {e}");
					}
				}
			);
		}
	}
}