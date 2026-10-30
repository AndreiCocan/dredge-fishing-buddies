using System;
using System.Collections;
using System.IO;
using System.Linq;

namespace CosmicHorrorFishingBuddies.Testing;

/// <summary>
/// All game instances run on one machine, so they coordinate through files in a shared folder
/// rather than through the network code under test.
/// </summary>
internal sealed class TestContext
{
	public const string HostName = "Host";
	public const string ClientName = "Client1";

	public readonly TestRole Role;
	public readonly string RoleName;
	public readonly string SharedFolder;
	private readonly string[] _players;

	public TestContext(TestRole role, string roleName, string[] players, string sharedFolder)
	{
		Role = role;
		RoleName = roleName;
		_players = players;
		SharedFolder = sharedFolder;
	}

	public int PlayerCount => _players.Length;

	/// <summary>
	/// The player this one exchanges values with: the host pairs with the first client, every client with the host.
	/// </summary>
	public TestRole OtherRole => Role == TestRole.Host ? TestRole.Client : TestRole.Host;
	private string OtherRoleName => Role == TestRole.Host ? ClientName : HostName;

	private string PathFor(string key, string roleName) => Path.Combine(SharedFolder, $"{roleName}.{key}.txt");

	public void Share(string key, object value) =>
		WriteFile(PathFor(key, RoleName), Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));

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
		var path = PathFor(key, OtherRoleName);
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
		var path = PathFor(key, OtherRoleName);
		yield return Expect.Until(() => File.Exists(path), timeoutSeconds, $"{OtherRoleName} to share '{key}'");
		onValue(File.ReadAllText(path));
	}

	public IEnumerator Barrier(string name, float timeoutSeconds = 120)
	{
		var key = "barrier." + name;
		Share(key, "");
		yield return Expect.Until(() => _players.All(player => File.Exists(PathFor(key, player))), timeoutSeconds,
			$"all of {string.Join(", ", _players)} to reach '{name}'");
	}
}
