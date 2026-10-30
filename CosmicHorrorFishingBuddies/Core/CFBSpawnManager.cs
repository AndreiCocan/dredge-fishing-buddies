using CosmicHorrorFishingBuddies.Extensions;
using CosmicHorrorFishingBuddies.HarvestPOISync;
using CosmicHorrorFishingBuddies.HarvestPOISync.Data;
using CosmicHorrorFishingBuddies.PlayerSync;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.Core
{
	internal class CFBSpawnManager : NetworkBehaviour
	{
		public static CFBSpawnManager Instance;

		// Several spawns can be in flight at once (every pot in a save spawns in the same frame), so each request
		// carries an id the server echoes back to match the network object to its local HarvestPOI
		private readonly Dictionary<int, HarvestPOI> _pendingSpawns = new();
		private int _nextSpawnRequest;

		public void Awake()
		{
			Instance = this;
		}

		private int TrackSpawnRequest(HarvestPOI harvestPOI)
		{
			var requestID = _nextSpawnRequest++;
			_pendingSpawns[requestID] = harvestPOI;
			return requestID;
		}

		#region Bait
		public void TrySpawnNetworkBait(BaitHarvestPOI bait)
		{
			try
			{
				if (NetworkHarvestPOIManager.Instance.IsHarvestPOITracked(bait)) return;

				CFBCore.LogInfo($"Spawning network bait for {PlayerManager.LocalNetID}");

				var zone = GameManager.Instance.Player.PlayerZoneDetector.GetCurrentZone();
				var position = new Vector3(GameManager.Instance.Player.BoatModelProxy.DeployPosition.position.x, 0f, GameManager.Instance.Player.BoatModelProxy.DeployPosition.position.z);
				var yRot = GameManager.Instance.Player.BoatModelProxy.DeployPosition.eulerAngles.y;
				var numFish = Mathf.CeilToInt(bait.HarvestPOIData.startStock);

				CmdSpawnNetworkBait(zone, position, yRot, numFish, TrackSpawnRequest(bait), NetworkPlayer.LocalPlayer.netIdentity);
			}
			catch (Exception e)
			{
				CFBCore.LogError($"Couldn't spawn network bait {e}");
			}
		}

		[Command(requiresAuthority = false)]
		private void CmdSpawnNetworkBait(ZoneEnum zone, Vector3 position, float yRot, int numFish, int requestID, NetworkIdentity sender)
		{
			var networkBaitHarvestPOI = CFBNetworkManager.BaitHarvestPOIPrefab.SpawnWithServerAuthority().GetComponent<NetworkBaitHarvestPOI>();

			networkBaitHarvestPOI.SetBaitData(zone, position, yRot, numFish, sender.netId);

			// Target the original creator so it can register its bait
			SpawnNetworkPOICallback(sender.connectionToClient, networkBaitHarvestPOI, requestID);
		}
		#endregion

		#region Crab
		public void TrySpawnNetworkCrabPot(PlacedHarvestPOI crabPot)
		{
			try
			{
				if (NetworkHarvestPOIManager.Instance.IsHarvestPOITracked(crabPot)) return;

				CFBCore.LogInfo($"Spawning network crab pot for {PlayerManager.LocalNetID}");

				CmdSpawnNetworkCrabPot(new SerializedCrabPotPOIDataWrapper(crabPot.Harvestable as SerializedCrabPotPOIData), TrackSpawnRequest(crabPot), NetworkPlayer.LocalPlayer.netIdentity);
			}
			catch (Exception e)
			{
				CFBCore.LogError($"Couldn't spawn network crab pot {e}");
			}
		}

		[Command(requiresAuthority = false)]
		private void CmdSpawnNetworkCrabPot(SerializedCrabPotPOIDataWrapper crabPotData, int requestID, NetworkIdentity sender)
		{
			var crabPot = CFBNetworkManager.PlacedHarvestPOIPrefab.InstantiateInactive();
			var networkPlacedHarvestPOI = crabPot.GetComponent<NetworkPlacedHarvestPOI>();
			networkPlacedHarvestPOI.SetCrabPotData(crabPotData, sender.netId);
			crabPot.SetActive(true);
			NetworkServer.Spawn(crabPot, NetworkServer.localConnection);

			// Target the original creator so it can register its crab pot
			SpawnNetworkPOICallback(sender.connectionToClient, networkPlacedHarvestPOI, requestID);
		}
		#endregion

		[TargetRpc]
		private void SpawnNetworkPOICallback(NetworkConnectionToClient _, NetworkHarvestPOI networkPOI, int requestID)
		{
			if (!_pendingSpawns.TryGetValue(requestID, out var harvestPOI))
			{
				CFBCore.LogError($"No pending spawn request {requestID}");
				return;
			}
			_pendingSpawns.Remove(requestID);

			networkPOI.Target = harvestPOI;

			if (networkPOI is NetworkBaitHarvestPOI networkBait)
			{
				NetworkHarvestPOIManager.Instance.RegisterNetworkBaitHarvestPOI(networkBait);
			}
			else if (networkPOI is NetworkPlacedHarvestPOI networkCrabPot)
			{
				NetworkHarvestPOIManager.Instance.RegisterPlacedHarvestPOI(networkCrabPot);
			}
		}
	}
}
