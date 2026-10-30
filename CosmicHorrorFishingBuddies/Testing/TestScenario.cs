using System;
using System.Collections;

namespace CosmicHorrorFishingBuddies.Testing;

/// <summary>
/// Host and Client are the two players every scenario pairs up; Extra is any further client, which most
/// scenarios leave out.
/// </summary>
internal enum TestRole { Host, Client, Extra }

internal sealed class TestScenario
{
	public readonly string Name;
	public readonly Func<TestContext, IEnumerator> Host;
	public readonly Func<TestContext, IEnumerator> Client;
	public readonly Func<TestContext, IEnumerator> Extra;
	public readonly float TimeoutSeconds;

	public TestScenario(string name, Func<TestContext, IEnumerator> host, Func<TestContext, IEnumerator> client,
		float timeoutSeconds = 60, Func<TestContext, IEnumerator> extra = null)
	{
		Name = name;
		Host = host;
		Client = client;
		Extra = extra;
		TimeoutSeconds = timeoutSeconds;
	}

	public static TestScenario Both(string name, Func<TestContext, IEnumerator> body, float timeoutSeconds = 60) =>
		new(name, body, body, timeoutSeconds);

	public static TestScenario Everyone(string name, Func<TestContext, IEnumerator> body, float timeoutSeconds = 60) =>
		new(name, body, body, timeoutSeconds, body);

	public Func<TestContext, IEnumerator> BodyFor(TestRole role) => role switch
	{
		TestRole.Host => Host,
		TestRole.Client => Client,
		_ => Extra,
	};
}

internal sealed class TestFailure : Exception
{
	public TestFailure(string message) : base(message) { }
}

internal static class Expect
{
	public static void That(bool condition, string message)
	{
		if (!condition) throw new TestFailure(message);
	}

	public static IEnumerator Until(Func<bool> condition, float timeoutSeconds, string description)
	{
		var deadline = UnityEngine.Time.realtimeSinceStartup + timeoutSeconds;
		while (!condition())
		{
			if (UnityEngine.Time.realtimeSinceStartup > deadline)
			{
				throw new TestFailure($"timed out after {timeoutSeconds}s waiting for: {description}");
			}
			yield return null;
		}
	}
}
