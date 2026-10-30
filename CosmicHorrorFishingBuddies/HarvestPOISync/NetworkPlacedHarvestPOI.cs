using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.Extensions;
using CosmicHorrorFishingBuddies.HarvestPOISync.Data;
using CosmicHorrorFishingBuddies.PlayerSync;
using Mirror;
using System;
using System.Linq;
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

		public bool IsOwnedLocally => NetworkPlayer.LocalPlayer != null && _activeOwnerID == NetworkPlayer.LocalPlayer.netId;

		// Whoever last changed the pot (the owner after a catch, anyone after taking fish) publishes its contents
		[SyncVar(hook = nameof(OnContentsChanged))]
		private PotContents _contents;

		private bool _contentsDirty;
		private bool _applyingContents;

		private SerializedCrabPotPOIData PotData => (SerializedCrabPotPOIData)Target.Harvestable;

		private PotContents LocalContents() => new()
		{
			items = PotData.grid.spatialItems.Select(PotItem.From).ToArray(),
			durability = PotData.durability,
		};

		protected override void OnSetTarget()
		{
			PotData.grid.OnContentsUpdated += MarkContentsDirty;

			if (IsOwnedLocally) MarkContentsDirty();
			else ApplyContents();
		}

		public void MarkContentsDirty()
		{
			if (!_applyingContents) _contentsDirty = true;
		}

		public void LateUpdate()
		{
			if (!_contentsDirty || Target == null) return;
			_contentsDirty = false;

			var contents = LocalContents();
			if (!contents.SameAs(_contents))
			{
				CmdSetContents(contents);
			}
		}

		[Command(requiresAuthority = false)]
		private void CmdSetContents(PotContents contents) => _contents = contents;

		private void OnContentsChanged(PotContents previous, PotContents current) => ApplyContents();

		private void ApplyContents()
		{
			if (Target == null || _contents.items == null || _contents.SameAs(LocalContents())) return;

			_applyingContents = true;
			try
			{
				PotData.grid.Clear(reInit: false);
				foreach (var item in _contents.items)
				{
					PotData.grid.AddObjectToGridData(item.ToInstance(), item.Position, dispatchEvent: true);
				}
				PotData.durability = _contents.durability;
				Target.OnStockUpdated();
			}
			finally
			{
				_applyingContents = false;
			}
		}

		public override void OnStopClient()
		{
			if (Target != null) PotData.grid.OnContentsUpdated -= MarkContentsDirty;
			base.OnStopClient();
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
