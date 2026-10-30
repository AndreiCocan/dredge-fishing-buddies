using HarmonyLib;

namespace CosmicHorrorFishingBuddies.WeatherSync.Patches;

[HarmonyPatch]
internal static class WeatherPatches
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(WeatherController), nameof(WeatherController.ChangeWeather), typeof(WeatherData))]
	public static void WeatherController_ChangeWeather(WeatherData weatherData)
	{
		if (WeatherSyncManager.Instance != null) WeatherSyncManager.Instance.OnLocalWeatherChanged(weatherData);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(GameManager), "Update")]
	public static void GameManager_Update()
	{
		if (WeatherSyncManager.Instance != null) WeatherSyncManager.Instance.FollowHostWaveClock();
	}
}
