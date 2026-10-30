using System;
using System.Collections;
using System.IO;

namespace CosmicHorrorFishingBuddies.Testing;

/// <summary>
/// Both game instances run on one machine, so they coordinate through files in a shared folder
/// rather than through the network code under test.
/// </summary>
internal sealed class TestContext
{
	public readonly TestRole Role;
	public readonly string SharedFolder;

	public TestContext(TestRole role, string sharedFolder)
	{
		Role = role;
		SharedFolder = sharedFolder;
	}

	public TestRole OtherRole => Role == TestRole.Host ? TestRole.Client : TestRole.Host;

	private string PathFor(string key, TestRole role) => Path.Combine(SharedFolder, $"{role}.{key}.txt");

	public void Share(string key, object value) =>
		WriteFile(PathFor(key, Role), Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));

	/// <summary>
	/// Readers (the other instance, the harness script) may hold the file open for a moment, which blocks
	/// replacing it on Windows, so retry briefly instead of failing the run.
	/// </summary>
	public static void WriteFile(string path, string contents)
	{
		File.WriteAllText(path + ".tmp", contents);
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				if (File.Exists(path)) File.Delete(path);
				File.Move(path + ".tmp", path);
				return;
			}
			catch (IOException) when (attempt < 50)
			{
				System.Threading.Thread.Sleep(10);
			}
			catch (UnauthorizedAccessException) when (attempt < 50)
			{
				System.Threading.Thread.Sleep(10);
			}
		}
	}

	public bool TryRead(string key, out string value)
	{
		var path = PathFor(key, OtherRole);
		try
		{
			value = File.ReadAllText(path);
			return true;
		}
		catch (IOException)
		{
			value = null;
			return false;
		}
	}

	public IEnumerator Receive(string key, Action<string> onValue, float timeoutSeconds = 60)
	{
		var path = PathFor(key, OtherRole);
		yield return Expect.Until(() => File.Exists(path), timeoutSeconds, $"{OtherRole} to share '{key}'");
		onValue(File.ReadAllText(path));
	}

	public IEnumerator Barrier(string name, float timeoutSeconds = 120)
	{
		Share("barrier." + name, "");
		yield return Receive("barrier." + name, _ => { }, timeoutSeconds);
	}
}
