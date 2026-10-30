using System;
using System.Collections;

namespace CosmicHorrorFishingBuddies.Testing;

internal enum TestRole { Host, Client }

internal sealed class TestScenario
{
	public readonly string Name;
	public readonly Func<TestContext, IEnumerator> Host;
	public readonly Func<TestContext, IEnumerator> Client;
	public readonly float TimeoutSeconds;

	public TestScenario(string name, Func<TestContext, IEnumerator> host, Func<TestContext, IEnumerator> client, float timeoutSeconds = 60)
	{
		Name = name;
		Host = host;
		Client = client;
		TimeoutSeconds = timeoutSeconds;
	}

	public static TestScenario Both(string name, Func<TestContext, IEnumerator> body, float timeoutSeconds = 60) =>
		new(name, body, body, timeoutSeconds);

	public Func<TestContext, IEnumerator> BodyFor(TestRole role) => role == TestRole.Host ? Host : Client;
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
