using CosmicHorrorFishingBuddies.Core;
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
		TestScenario.Both("stationary boats are drawn where they are when a player joins", StationaryBoatsVisibleOnJoin),		TestScenario.Both("time of day matches the host", TimeOfDayMatches),
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
	/// Both sides keep publishing their live boat position and compare the other's latest report against
	/// where they draw the remote boat, until both agree or time runs out.
	/// </summary>
	private static IEnumerator RemoteBoatMatchesReportedPosition(TestContext ctx, string key)
	{
		var reported = new Vector2(float.NaN, float.NaN);
		float Distance() => RemotePlayer() is { } remote ? Vector2.Distance(XZ(remote.transform.position), reported) : float.PositiveInfinity;

		var matched = false;
		var otherMatched = false;
		var deadline = Time.realtimeSinceStartup + 15;
		while (!(matched && otherMatched))
		{
			var local = XZ(GameManager.Instance.Player.transform.position);
			ctx.Share(key, FormattableString.Invariant($"{local.x},{local.y}"));

			if (ctx.TryRead(key, out var s))
			{
				var parts = s.Split(',');
				reported = new Vector2(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
			}
			if (!matched && Distance() < 2f)
			{
				matched = true;
				ctx.Share(key + ".matched", "");
			}
			otherMatched = ctx.TryRead(key + ".matched", out _);

			Expect.That(Time.realtimeSinceStartup < deadline,
				matched
					? $"{ctx.OtherRole} never saw this boat where it is"
					: $"remote boat drawn {Distance():F1}m from where {ctx.OtherRole} reports it ({reported:F1})");
			yield return new WaitForSeconds(0.25f);
		}
	}

	private static IEnumerator StationaryBoatsVisibleOnJoin(TestContext ctx) => RemoteBoatMatchesReportedPosition(ctx, "position.stationary");

	private static IEnumerator TimeOfDayMatches(TestContext ctx)
	{
		ctx.Share("time", GameManager.Instance.Time.TimeAndDay);

		var other = 0f;
		yield return ctx.Receive("time", s => other = float.Parse(s, CultureInfo.InvariantCulture));

		var drift = Math.Abs(GameManager.Instance.Time.TimeAndDay - other);
		Expect.That(drift < 0.01f, $"time differs from {ctx.OtherRole} by {drift} days");
	}
}
