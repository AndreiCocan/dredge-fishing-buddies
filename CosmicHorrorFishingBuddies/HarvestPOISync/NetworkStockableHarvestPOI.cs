using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.HarvestPOISync.Patches;
using Mirror;
using System;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.HarvestPOISync
{
	internal class NetworkStockableHarvestPOI : NetworkHarvestPOI
	{
		public bool IsCurrentlySpecial => _isCurrentlySpecial;

		[SyncVar] private float _stockCount = Mathf.NegativeInfinity;

		[SyncVar] private bool _isCurrentlySpecial;

		[Command(requiresAuthority = false)]
		public void SetStockCount(float count)
		{
			_stockCount = count;
			RpcSetStockCount(count);
		}

		[ClientRpc]
		private void RpcSetStockCount(float count) => ApplyStockCount(count);

		private void ApplyStockCount(float count)
		{
			// Do not allow syncing of spots which do not restock; infinity means the host has not reported a count yet
			if (Target == null || float.IsInfinity(count) || !Target.harvestable.GetDoesRestock()) return;

			try
			{
				HarvestPOIPatches.disabled = true;

				var dStock = count - Target.Stock;
				Target.harvestable.AddStock(dStock, false);
				Target.OnStockUpdated();
			}
			catch (Exception e)
			{
				CFBCore.LogError($"Couldn't apply stock to {Target.name}: {e}");
			}
			finally
			{
				HarvestPOIPatches.disabled = false;
			}
		}

		/// <summary>
		/// Only the host rolls special spots, and as the server it sets the value directly: as a Command this needed
		/// authority and the host's rolls never reached the synced value.
		/// </summary>
		[Server]
		public void SetIsCurrentlySpecial(bool isCurrentlySpecial)
		{
			_isCurrentlySpecial = isCurrentlySpecial;
			RpcSetSpecial(isCurrentlySpecial);
		}

		[ClientRpc]
		private void RpcSetSpecial(bool isSpecial) => ApplySpecial(isSpecial);

		private void ApplySpecial(bool isSpecial)
		{
			if (Target == null) return;

			try
			{
				HarvestPOIPatches.disabled = true;

				Target.SetIsCurrentlySpecial(isSpecial);
			}
			catch (Exception e)
			{
				CFBCore.LogError(e);
			}
			finally
			{
				HarvestPOIPatches.disabled = false;
			}
		}

		protected override void OnSetTarget()
		{
			// A joining client applies the synced state directly: calling the ClientRpc methods from a client
			// never runs their bodies, so it kept its own stock and special spots until the next change
			if (!NetworkClient.activeHost)
			{
				ApplyStockCount(_stockCount);
				ApplySpecial(_isCurrentlySpecial);
			}
			else
			{
				SetStockCount(Target.Stock);
				SetIsCurrentlySpecial(Target.IsCurrentlySpecial);
			}
		}
	}
}
