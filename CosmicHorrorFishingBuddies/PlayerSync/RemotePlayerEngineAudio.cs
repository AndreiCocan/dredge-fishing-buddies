using Mirror;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.PlayerSync
{
	internal class RemotePlayerEngineAudio : NetworkBehaviour
	{
		public float minPitch = 0.9f;
		public float maxPitch = 1.3f;

		public float minVolume = 0.1f;
		public float maxVolume = 1f;

		public AudioSource engineSource;

		[SyncVar]
		private float volume;

		[SyncVar]
		private float pitch;

		private Vector2 _sentSound = new(float.NaN, float.NaN);
		private float _nextSoundSend;

		/// <summary>
		/// Called every frame by the owner; sends only audible changes, at most 10 times a second, since a command per
		/// frame per client floods the host.
		/// </summary>
		public void UpdateEngineSound(float v, float p)
		{
			var sound = new Vector2(v, p);
			if (!float.IsNaN(_sentSound.x) && (sound - _sentSound).sqrMagnitude < 0.0001f) return;
			if (Time.unscaledTime < _nextSoundSend) return;

			_sentSound = sound;
			_nextSoundSend = Time.unscaledTime + 0.1f;
			SetEngineSound(v, p);
		}

		[Command]
		private void SetEngineSound(float v, float p)
		{
			volume = v;
			pitch = p;
		}

		public void Start()
		{
			if (!isOwned)
			{
				engineSource.clip = GameManager.Instance.Player.playerEngineAudio.audioSource.clip;
				engineSource.Play();
			}
		}

		public void Update()
		{
			if (!isOwned)
			{
				engineSource.volume = volume;
				engineSource.pitch = pitch;
			}
		}
	}
}
