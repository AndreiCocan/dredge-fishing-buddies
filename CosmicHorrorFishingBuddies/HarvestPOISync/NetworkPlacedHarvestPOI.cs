using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.Extensions;
using CosmicHorrorFishingBuddies.HarvestPOISync.Data;
using CosmicHorrorFishingBuddies.PlayerSync;
using Mirror;
using System;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.HarvestPOISync
{
	internal class NetworkPlacedHarvestPOI : NetworkHarvestPOI
	{
		[SyncVar]
		private uint _activeOwnerID = uint.MaxValue;
		public uint OwnerID => _activeOwnerID;

		[SyncVar]
		private SerializedCrabPotPOIDataWrapper _crabPotData;

		/// <summary>
		/// Call before spawning, so the pot travels in the spawn state and players who join later get it too.
		/// </summary>
		public void SetCrabPotData(SerializedCrabPotPOIDataWrapper crabPotData, uint ownerID)
		{
			_crabPotData = crabPotData;
			_activeOwnerID = ownerID;
		}

		public override void OnStartClient()
		{
			base.OnStartClient();

			Delay.RunWhen(() => this == null || NetworkPlayer.LocalPlayer != null, () =>
			{
				if (this != null && _activeOwnerID != NetworkPlayer.LocalPlayer.netId)
				{
					InitializeCrabPotData(_crabPotData);
				}
			});
		}

		private void InitializeCrabPotData(SerializedCrabPotPOIDataWrapper crabPotData)
		{
			try
			{
				var yRotation = crabPotData.yRotation;
				var position = new Vector3(crabPotData.x, 0f, crabPotData.z);
				var gameObject = GameSceneInitializer.Instance.GetPlacedHarvestPOIPrefabFromPotItemData(crabPotData.deployableItemId).InstantiateInactive();
				gameObject.transform.parent = GameSceneInitializer.Instance.harvestPoiContainer.transform;
				gameObject.transform.position = position;
				gameObject.transform.eulerAngles = new Vector3(0f, yRotation, 0f);
				gameObject.name = "PlacedHarvestPOI";
				var harvestPOI = gameObject.GetComponent<HarvestPOI>();
				if (harvestPOI)
				{
					var harvestable = crabPotData.ToData();

					harvestPOI.Harvestable = harvestable;
					var cullable = harvestPOI.GetComponent<Cullable>();
					if (cullable)
					{
						GameManager.Instance.CullingBrain.AddCullable(cullable);
					}
					harvestable.Init();
				}

				Target = harvestPOI;

				NetworkHarvestPOIManager.Instance.RegisterPlacedHarvestPOI(this);

				gameObject.SetActive(true);

				CFBCore.LogInfo($"Created crab pot on {NetworkPlayer.LocalPlayer.netId}");
			}
			catch (Exception e)
			{
				CFBCore.LogError($"Couldn't initialize crab pot data {e}");
			}
		}
	}
}
