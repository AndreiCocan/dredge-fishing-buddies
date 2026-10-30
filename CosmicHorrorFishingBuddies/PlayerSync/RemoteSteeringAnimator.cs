using Mirror;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.PlayerSync
{
	[RequireComponent(typeof(NetworkPlayer))]
	internal class RemoteSteeringAnimator : NetworkBehaviour
	{
		public Transform[] rudders;
		public Transform[] propellers;

		public float rudderMaxTurnDegrees = 50f;
		public Vector3 propellorMaxTurnSpeed = new(0f, 0f, 50f);

		private float _moveValX;
		private float _moveValY;
		private float _lerpedMoveValY;

		private NetworkPlayer _player;

		private Vector2 _sentSteering = new(float.NaN, float.NaN);
		private float _nextSteeringSend;

		/// <summary>
		/// Called every frame by the owner; sends only changes, at most 20 times a second, since a command per frame
		/// per client floods the host. A change skipped for the rate limit goes out on a later frame.
		/// </summary>
		public void UpdateSteering(float moveValX, float moveValY)
		{
			var steering = new Vector2(moveValX, moveValY);
			if (steering == _sentSteering || Time.unscaledTime < _nextSteeringSend) return;

			_sentSteering = steering;
			_nextSteeringSend = Time.unscaledTime + 0.05f;
			SetSteeringParameters(moveValX, moveValY);
		}

		[Command]
		public void SetSteeringParameters(float moveValX, float moveValY) => RpcSetSteeringParameters(moveValX, moveValY);

		[ClientRpc(includeOwner = false)]
		public void RpcSetSteeringParameters(float moveValX, float moveValY)
		{
			_moveValX = moveValX;
			_moveValY = moveValY;
		}

		public void Start()
		{
			_player = GetComponent<NetworkPlayer>();
			_player.remotePlayerBoatGraphics.RefreshBoatModel.AddListener(OnRefreshBoatModel);
		}

		public void OnDestroy()
		{
			_player.remotePlayerBoatGraphics.RefreshBoatModel.RemoveListener(OnRefreshBoatModel);
		}

		private void OnRefreshBoatModel()
		{
			var currentSteering = _player.remotePlayerBoatGraphics.CurrentBoatModelProxy.GetComponentInChildren<SteeringAnimator>(true);

			rudders = currentSteering.rudders;
			propellers = currentSteering.propellers;

			rudderMaxTurnDegrees = currentSteering.rudderMaxTurnDegrees;
			propellorMaxTurnSpeed = currentSteering.propellorMaxTurnSpeed;
		}

		public void Update()
		{
			_lerpedMoveValY = Mathf.Lerp(_lerpedMoveValY, _moveValY, Time.deltaTime * 4f);
			if (_moveValY < 0f)
			{
				_moveValX *= -1f;
			}
			for (int i = 0; i < rudders.Length; i++)
			{
				Vector3 euler = new(0f, rudderMaxTurnDegrees * -_moveValX, 0f);
				rudders[i].localRotation = Quaternion.Lerp(rudders[i].localRotation, Quaternion.Euler(euler), Time.deltaTime * 10f);
			}
			for (int j = 0; j < propellers.Length; j++)
			{
				propellers[j].transform.Rotate(propellorMaxTurnSpeed * _lerpedMoveValY * Time.deltaTime, Space.Self);
			}
		}
	}
}
