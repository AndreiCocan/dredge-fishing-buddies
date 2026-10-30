using CosmicHorrorFishingBuddies.Core;
using CosmicHorrorFishingBuddies.PlayerSync;
using Mirror;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.Testing;

/// <summary>
/// Runs <see cref="Scenarios.All"/> inside a host and a client instance started by tools/run-game-tests.ps1,
/// writing one results file per role into the shared test-results folder.
/// </summary>
internal class TestRunner : MonoBehaviour
{
	public static string ResultsFolder => Path.Combine(CFBCore.GetModFolder(), "test-results");

	private sealed class Result
	{
		public string name;
		public bool passed;
		public string error;
		public float seconds;
	}

	private sealed class Report
	{
		public TestRole role;
		public bool finished;
		public List<Result> results = new();
	}

	public void Start() => StartCoroutine(Run());

	private IEnumerator Run()
	{
		Directory.CreateDirectory(ResultsFolder);

		var joinDeadline = Time.realtimeSinceStartup + 180;
		while (NetworkPlayer.LocalPlayer == null)
		{
			if (Time.realtimeSinceStartup > joinDeadline)
			{
				CFBCore.LogError("[TEST] never got a local network player, giving up");
				Application.Quit();
				yield break;
			}
			yield return null;
		}

		var role = NetworkServer.activeHost ? TestRole.Host : TestRole.Client;
		var context = new TestContext(role, ResultsFolder);
		var report = new Report { role = role };
		WriteReport(report);
		context.Share("ready", "");
		CFBCore.LogInfo($"[TEST] runner started as {role}");

		for (var i = 0; i < Scenarios.All.Length; i++)
		{
			var scenario = Scenarios.All[i];
			Exception error = null;
			yield return Execute(context.Barrier($"start.{i}"), 300, e => error = e);

			var started = Time.realtimeSinceStartup;
			var body = scenario.BodyFor(role);
			if (error == null && body != null)
			{
				yield return Execute(body(context), scenario.TimeoutSeconds, e => error = e);
			}

			var result = new Result
			{
				name = scenario.Name,
				passed = error == null,
				error = error is TestFailure ? error.Message : error?.ToString(),
				seconds = Time.realtimeSinceStartup - started,
			};
			report.results.Add(result);
			WriteReport(report);
			CFBCore.LogInfo($"[TEST] {(result.passed ? "PASS" : "FAIL")} {scenario.Name} {result.error}");
		}

		yield return Execute(context.Barrier("done"), 120, _ => { });
		report.finished = true;
		WriteReport(report);

		// The client leaves first so the host never sees its server vanish mid-run
		if (role == TestRole.Host)
		{
			var leaveDeadline = Time.realtimeSinceStartup + 15;
			yield return new WaitUntil(() => PlayerManager.Players.Count <= 1 || Time.realtimeSinceStartup > leaveDeadline);
		}
		Application.Quit();
	}

	private static void WriteReport(Report report)
	{
		TestContext.WriteFile(Path.Combine(ResultsFolder, $"{report.role}.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
	}

	/// <summary>
	/// Unity coroutines swallow exceptions from nested enumerators, so walk the nesting by hand to catch them.
	/// </summary>
	private static IEnumerator Execute(IEnumerator root, float timeoutSeconds, Action<Exception> onError)
	{
		var stack = new Stack<IEnumerator>();
		stack.Push(root);
		var deadline = Time.realtimeSinceStartup + timeoutSeconds;
		while (stack.Count > 0)
		{
			if (Time.realtimeSinceStartup > deadline)
			{
				onError(new TestFailure($"exceeded {timeoutSeconds}s"));
				yield break;
			}

			object current;
			try
			{
				if (!stack.Peek().MoveNext())
				{
					stack.Pop();
					continue;
				}
				current = stack.Peek().Current;
			}
			catch (Exception e)
			{
				onError(e);
				yield break;
			}

			if (current is IEnumerator nested) stack.Push(nested);
			else yield return current;
		}
	}
}
