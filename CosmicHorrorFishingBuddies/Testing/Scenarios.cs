using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.HarvestPOISync;
using CosmicHorrorFishingBuddies.PlayerSync;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.Testing;

/// <summary>
/// Every synced feature gets a scenario here. Both instances run them in this order.
/// </summary>
internal static class Scenarios
{
	public static readonly TestScenario[] All =
	{
		TestScenario.Both("every Harmony patch is applied to this game version", AllPatchesApplied),
		TestScenario.Both("both players see each other", BothPlayersPresent),
		TestScenario.Both("stationary boats are drawn where they are when a player joins", StationaryBoatsVisibleOnJoin),
		TestScenario.Both("time of day matches the host", TimeOfDayMatches),
		TestScenario.Both("each player sees the other's saved crab pots", SavedCrabPotsVisibleToOtherPlayer),
		TestScenario.Both("crab pot contents match the owner's", CrabPotContentsMatchOwner),
		new("fish added to a pot reach the other player, fish taken out leave the owner's pot", AddFishToOwnPot, TakeFishFromRemotePot),
		TestScenario.Both("the wave clock matches the host", WaveClockMatchesHost),
		new("the host's weather change reaches a player in the same zone",
			ctx => ChangeWeather(ctx, "weather.host"), ctx => FollowWeather(ctx, "weather.host")),
		new("a client's weather change reaches the host in the same zone",
			ctx => FollowWeather(ctx, "weather.client"), ctx => ChangeWeather(ctx, "weather.client")),
		TestScenario.Both("the mod logged no errors", NoModErrors),
	};

	private static readonly Type[] PatchMethodAttributes =
		{ typeof(HarmonyPrefix), typeof(HarmonyPostfix), typeof(HarmonyTranspiler), typeof(HarmonyFinalizer) };

	private static IEnumerator AllPatchesApplied(TestContext ctx)
	{
		var declared = typeof(CFBCore).Assembly.GetTypes()
			.SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
			.Where(m => PatchMethodAttributes.Any(a => m.IsDefined(a, false)))
			.ToList();

		var applied = new HashSet<MethodInfo>(Harmony.GetAllPatchedMethods()
			.Select(Harmony.GetPatchInfo)
			.SelectMany(info => info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers))
			.Where(patch => patch.owner == nameof(CFBCore))
			.Select(patch => patch.PatchMethod));

		var missing = declared.Where(m => !applied.Contains(m)).Select(m => $"{m.DeclaringType.Name}.{m.Name}").ToList();
		Expect.That(declared.Count > 0, "found no patch methods to check");
		Expect.That(missing.Count == 0, $"{missing.Count}/{declared.Count} patches not applied: {string.Join(", ", missing)}");
		yield break;
	}

	private static IEnumerator BothPlayersPresent(TestContext ctx)
	{
		yield return Expect.Until(() => PlayerManager.Players.Count == 2, 30, $"2 players (have {PlayerManager.Players.Count})");
	}

	private static NetworkPlayer RemotePlayer() => PlayerManager.Players.Values.FirstOrDefault(p => !p.isOwned);

	private static Vector2 XZ(Vector3 v) => new(v.x, v.z);

	/// <summary>
	/// Both sides keep publishing their live state and checking the other's latest report against what they see of
	/// it, until both agree or time runs out. Live state can change mid-check, so a single exchange is not enough.
	/// </summary>
	private static IEnumerator KeepComparing(TestContext ctx, string key, Func<string> mine, Func<string, bool> matches, Func<string, string> mismatch)
	{
		string reported = null;
		var matched = false;
		var otherMatched = false;
		var deadline = Time.realtimeSinceStartup + 15;
		while (!(matched && otherMatched))
		{
			ctx.Share(key, mine());
			if (ctx.TryRead(key, out var s)) reported = s;

			if (!matched && reported != null && matches(reported))
			{
				matched = true;
				ctx.Share(key + ".matched", "");
			}
			otherMatched = ctx.TryRead(key + ".matched", out _);

			Expect.That(Time.realtimeSinceStartup < deadline,
				matched ? $"{ctx.OtherRole} never saw this player's state" : mismatch(reported));
			yield return new WaitForSeconds(0.25f);
		}
	}

	private static Vector2 ParseXZ(string s)
	{
		var parts = s.Split(',');
		return new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
	}

	private static IEnumerator StationaryBoatsVisibleOnJoin(TestContext ctx)
	{
		float Distance(string reported) => RemotePlayer() is { } remote ? Vector2.Distance(XZ(remote.transform.position), ParseXZ(reported)) : float.PositiveInfinity;

		return KeepComparing(ctx, "position.stationary",
			() =>
			{
				var local = XZ(GameManager.Instance.Player.transform.position);
				return FormattableString.Invariant($"{local.x},{local.y}");
			},
			reported => Distance(reported) < 2f,
			reported => reported == null
				? $"{ctx.OtherRole} never reported its position"
				: $"remote boat drawn {Distance(reported):F1}m from where {ctx.OtherRole} reports it ({reported})");
	}

	private static int CrabPotsOwnedBy(Func<uint, bool> owner) =>
		NetworkHarvestPOIManager.Instance.CrabPots.Count(pot => pot.Target != null && owner(pot.OwnerID));

	private static IEnumerator SavedCrabPotsVisibleToOtherPlayer(TestContext ctx)
	{
		var self = PlayerManager.LocalNetID;
		var savedPots = GameManager.Instance.SaveData.serializedCrabPotPOIs.Count;
		Expect.That(savedPots > 0, "the test save has no deployed crab pots");

		yield return Expect.Until(() => CrabPotsOwnedBy(id => id == self) >= savedPots, 20,
			$"own crab pots on the network ({CrabPotsOwnedBy(id => id == self)}/{savedPots})");
		var own = CrabPotsOwnedBy(id => id == self);
		ctx.Share("pots", own);

		var theirs = 0;
		yield return ctx.Receive("pots", s => theirs = int.Parse(s));
		yield return Expect.Until(() => CrabPotsOwnedBy(id => id != self) == theirs, 20,
			$"{theirs} crab pots from {ctx.OtherRole} (see {CrabPotsOwnedBy(id => id != self)})");
	}

	private static SerializedCrabPotPOIData PotData(NetworkPlacedHarvestPOI pot) => (SerializedCrabPotPOIData)pot.Target.Harvestable;

	private static IEnumerable<NetworkPlacedHarvestPOI> PotsOwnedBy(Func<uint, bool> owner) =>
		NetworkHarvestPOIManager.Instance.CrabPots.Where(pot => pot.Target != null && owner(pot.OwnerID));

	/// <summary>
	/// One line per pot, keyed by where it sits, listing the item ids inside it.
	/// </summary>
	private static string PotContents(Func<uint, bool> owner) => string.Join("\n", PotsOwnedBy(owner)
		.Select(PotData)
		.Select(d => FormattableString.Invariant($"{d.x:F1},{d.z:F1}: {string.Join(",", d.grid.spatialItems.Select(i => i.id).OrderBy(id => id))}"))
		.OrderBy(line => line));

	private static IEnumerator CrabPotContentsMatchOwner(TestContext ctx)
	{
		var self = PlayerManager.LocalNetID;
		return KeepComparing(ctx, "pot.contents",
			() => PotContents(id => id == self),
			reported => PotContents(id => id != self) == reported,
			reported => $"{ctx.OtherRole}'s pots:\n{reported}\nseen here as:\n{PotContents(id => id != self)}");
	}

	private static NetworkPlacedHarvestPOI PotAt(Func<uint, bool> owner, Vector2 position) =>
		PotsOwnedBy(owner).FirstOrDefault(pot => Vector2.Distance(new Vector2(PotData(pot).x, PotData(pot).z), position) < 0.1f);

	private static IEnumerator AddFishToOwnPot(TestContext ctx)
	{
		var self = PlayerManager.LocalNetID;
		var pot = PotsOwnedBy(id => id == self).FirstOrDefault();
		Expect.That(pot != null, "host has no crab pot on the network");
		var data = PotData(pot);

		var fish = GameManager.Instance.ItemManager.GetSpatialItemDataBySubtype(ItemSubtype.FISH)
			.Select(item => GameManager.Instance.ItemManager.AddItemById(item.id, data.grid))
			.FirstOrDefault(added => added != null);
		Expect.That(fish != null, "no fish fits in the host's crab pot");
		pot.Target.OnStockUpdated();

		ctx.Share("pot", FormattableString.Invariant($"{data.x},{data.z}"));
		ctx.Share("fish", fish.id);

		yield return ctx.Receive("taken", _ => { });
		yield return Expect.Until(() => data.grid.spatialItems.Count == 0, 10,
			$"the host's pot to be emptied by the client (holds {data.grid.spatialItems.Count} items)");
		ctx.Share("emptied", "");
	}

	private static IEnumerator TakeFishFromRemotePot(TestContext ctx)
	{
		var self = PlayerManager.LocalNetID;
		var position = Vector2.zero;
		var fishID = "";
		yield return ctx.Receive("pot", s => position = ParseXZ(s));
		yield return ctx.Receive("fish", s => fishID = s);

		var pot = PotAt(id => id != self, position);
		Expect.That(pot != null, $"no copy of the host's pot at {position}");
		var data = PotData(pot);
		yield return Expect.Until(() => data.grid.spatialItems.Any(i => i.id == fishID), 10,
			$"the host's {fishID} to appear in the pot (holds {string.Join(",", data.grid.spatialItems.Select(i => i.id))})");

		foreach (var item in data.grid.spatialItems.ToList())
		{
			data.grid.RemoveObjectFromGridData(item, notify: true);
		}
		pot.Target.OnStockUpdated();
		ctx.Share("taken", "");

		yield return ctx.Receive("emptied", _ => { }, 15);
	}

	private static IEnumerator WaveClockMatchesHost(TestContext ctx)
	{
		// Mirror's NetworkTime is shared, so the other side's reading can be carried forward to now
		float Drift(string reported)
		{
			var parts = reported.Split(',');
			var expected = Mathf.Repeat(float.Parse(parts[0], CultureInfo.InvariantCulture)
				+ (float)(Mirror.NetworkTime.time - double.Parse(parts[1], CultureInfo.InvariantCulture)), 1000f);
			var difference = Mathf.Abs(GameManager.Instance.gameTime - expected);
			return Mathf.Min(difference, 1000f - difference);
		}

		return KeepComparing(ctx, "wave.clock",
			() => FormattableString.Invariant($"{GameManager.Instance.gameTime},{Mirror.NetworkTime.time}"),
			reported => Drift(reported) < 0.25f,
			reported => reported == null
				? $"{ctx.OtherRole} never reported its wave clock"
				: $"wave clock is {Drift(reported):F2}s off {ctx.OtherRole}'s");
	}

	private static string CurrentWeather() => GameManager.Instance.WeatherController.CurrentWeatherData.name;

	private static IEnumerator ChangeWeather(TestContext ctx, string key)
	{
		// Pick a weather neither player has, or the follower could pass without anything syncing
		var followerWeather = "";
		yield return ctx.Receive(key + ".before", s => followerWeather = s);

		var zone = GameManager.Instance.Player.PlayerZoneDetector.GetCurrentZone();
		var weather = GameManager.Instance.DataLoader.allWeather
			.FirstOrDefault(w => w.permittedZones.HasFlag(zone) && w.name != CurrentWeather() && w.name != followerWeather);
		Expect.That(weather != null, $"no third weather is allowed in {zone}");

		GameManager.Instance.WeatherController.ChangeWeather(weather);
		ctx.Share(key, weather.name);
		yield return ctx.Receive(key + ".seen", _ => { }, 15);
	}

	private static IEnumerator FollowWeather(TestContext ctx, string key)
	{
		ctx.Share(key + ".before", CurrentWeather());

		var weather = "";
		yield return ctx.Receive(key, s => weather = s);
		yield return Expect.Until(() => CurrentWeather() == weather, 10,
			$"{ctx.OtherRole}'s weather {weather} (have {CurrentWeather()})");
		ctx.Share(key + ".seen", "");
	}

	private static IEnumerator NoModErrors(TestContext ctx)
	{
		Expect.That(CFBCore.LoggedErrors.Count == 0,
			$"{CFBCore.LoggedErrors.Count} errors, first: {CFBCore.LoggedErrors.FirstOrDefault()}");
		yield break;
	}

	private static IEnumerator TimeOfDayMatches(TestContext ctx)
	{
		ctx.Share("time", GameManager.Instance.Time.TimeAndDay);

		var other = 0f;
		yield return ctx.Receive("time", s => other = float.Parse(s, CultureInfo.InvariantCulture));

		var drift = Math.Abs(GameManager.Instance.Time.TimeAndDay - other);
		Expect.That(drift < 0.01f, $"time differs from {ctx.OtherRole} by {drift} days");
	}
}
