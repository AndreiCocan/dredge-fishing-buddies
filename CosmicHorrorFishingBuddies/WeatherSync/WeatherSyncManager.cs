using CosmicHorrorFishingBuddies.PlayerSync;
using Mirror;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.WeatherSync;

internal struct ZoneWeather
{
	public string weather;
	public float changeTime;
}

/// <summary>
/// Weather is shared per zone, because each weather is only allowed in some zones: players in the same zone see the
/// same weather, and whoever's game changes it (on a timer or on entering the zone) changes it for everyone there.
/// The wave clock follows the host so the water and boat bobbing line up.
/// </summary>
internal class WeatherSyncManager : NetworkBehaviour
{
	public static WeatherSyncManager Instance { get; private set; }

	private readonly SyncDictionary<ZoneEnum, ZoneWeather> _zoneWeather = new();

	// GameManager.gameTime drives the wave shader and wave height; each game starts its own at launch
	[SyncVar]
	private float _hostWaveClock;

	[SyncVar]
	private double _hostWaveClockStamp;

	private float _nextWaveClockSample;
	private float _nextWeatherCheck;
	private bool _applyingSharedWeather;

	public void Awake()
	{
		Instance = this;
	}

	private static ZoneEnum CurrentZone() => GameManager.Instance.Player.PlayerZoneDetector.GetCurrentZone();

	public void Update()
	{
		if (isServer && Time.unscaledTime >= _nextWaveClockSample)
		{
			_nextWaveClockSample = Time.unscaledTime + 0.5f;
			_hostWaveClock = GameManager.Instance.gameTime;
			_hostWaveClockStamp = NetworkTime.time;
		}

		if (Time.unscaledTime < _nextWeatherCheck) return;
		_nextWeatherCheck = Time.unscaledTime + 1f;

		var weatherController = GameManager.Instance.WeatherController;
		if (NetworkPlayer.LocalPlayer == null || GameManager.Instance.Player == null || weatherController == null) return;

		if (_zoneWeather.TryGetValue(CurrentZone(), out var shared))
		{
			if (shared.weather != weatherController.CurrentWeatherData.name) ApplySharedWeather(shared);
		}
		else
		{
			OnLocalWeatherChanged(weatherController.CurrentWeatherData);
		}
	}

	public void OnLocalWeatherChanged(WeatherData weather)
	{
		if (_applyingSharedWeather || NetworkPlayer.LocalPlayer == null || GameManager.Instance.Player == null) return;

		CmdSetZoneWeather(CurrentZone(), new ZoneWeather { weather = weather.name, changeTime = GameManager.Instance.SaveData.WeatherChangeTime });
	}

	[Command(requiresAuthority = false)]
	private void CmdSetZoneWeather(ZoneEnum zone, ZoneWeather weather) => _zoneWeather[zone] = weather;

	private void ApplySharedWeather(ZoneWeather shared)
	{
		var weather = GameManager.Instance.DataLoader.allWeather.Find(w => w.name == shared.weather);
		if (weather == null) return;

		_applyingSharedWeather = true;
		try
		{
			GameManager.Instance.WeatherController.ChangeWeather(weather);
		}
		finally
		{
			_applyingSharedWeather = false;
		}
		// Share the expiry too, so every game in the zone picks the next weather at the same moment
		GameManager.Instance.SaveData.WeatherChangeTime = shared.changeTime;
	}

	public void FollowHostWaveClock()
	{
		if (isServer || _hostWaveClockStamp == 0) return;

		var gameTime = Mathf.Repeat(_hostWaveClock + (float)(NetworkTime.time - _hostWaveClockStamp), 1000f);
		GameManager.Instance.gameTime = gameTime;
		Shader.SetGlobalFloat("_GameTime", gameTime);
	}
}
