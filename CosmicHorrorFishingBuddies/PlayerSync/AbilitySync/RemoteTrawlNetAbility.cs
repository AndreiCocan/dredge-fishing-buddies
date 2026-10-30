using CosmicHorrorFishingBuddies.AudioSync;
using CosmicHorrorFishingBuddies.PlayerSync.AbilitySync.Base;
using DG.Tweening;
using System;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.PlayerSync.AbilitySync
{
	internal class RemoteTrawlNetAbility : RemoteSyncVarAbility
	{
		// Have to adjust the rotation of the trawl net - will be unnessecary when rigidbodies work
		private Tween _rotationTween;

		private bool _wasActive;

		public override Type AbilityType => typeof(TrawlNetAbility);

		public override void Start()
		{
			base.Start();
			_networkPlayer.remotePlayerBoatGraphics.NetsRefreshed.AddListener(ApplyNetState);
		}

		public void OnDestroy()
		{
			_networkPlayer.remotePlayerBoatGraphics.NetsRefreshed.RemoveListener(ApplyNetState);
		}

		protected override void OnToggleRemote(bool active)
		{
			if (active != _wasActive)
			{
				_networkPlayer.RemotePlayOneShot(active ? AudioEnum.TRAWL_ACTIVATE : AudioEnum.TRAWL_END, 1f, 1f);
			}
			_wasActive = active;

			ApplyNetState();
		}

		/// <summary>
		/// The remote boat swaps its model and nets for a while after spawning, so the state is reapplied on every
		/// net refresh rather than once.
		/// </summary>
		private void ApplyNetState()
		{
			var boat = _networkPlayer.remotePlayerBoatGraphics.CurrentBoatModelProxy;
			var animator = boat == null ? null : boat.GetTrawlNetAnimator();
			if (animator == null) return;

			animator.SetBool("isDeployed", IsActive);

			var trawlNet = boat.transform.Find("TrawlNet/TrawlArmature/TrawlArm/Net");
			if (trawlNet == null) return;

			_rotationTween?.Kill();
			_rotationTween = trawlNet.DOLocalRotate(IsActive ? new Vector3(90, 0, 0) : Vector3.zero, 1f);
			if (!IsActive) _rotationTween.SetDelay(2f);
		}
	}
}
