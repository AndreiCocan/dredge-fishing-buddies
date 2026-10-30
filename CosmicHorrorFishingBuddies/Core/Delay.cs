using System;
using System.Collections;
using UnityEngine;

namespace CosmicHorrorFishingBuddies.Core
{
	internal static class Delay
	{
		private class Runner : MonoBehaviour { }

		private static Runner _runner;

		/// <summary>
		/// Coroutines run on their own object: CFBNetworkManager.Awake deactivates the mod's main object, which
		/// silently killed any coroutine started on it before then.
		/// </summary>
		private static Runner Coroutines
		{
			get
			{
				if (_runner == null)
				{
					var gameObject = new GameObject($"{nameof(CosmicHorrorFishingBuddies)}.{nameof(Delay)}");
					UnityEngine.Object.DontDestroyOnLoad(gameObject);
					_runner = gameObject.AddComponent<Runner>();
				}
				return _runner;
			}
		}

		public static void FireOnNextUpdate(Action action) => Coroutines.StartCoroutine(FireOnNextUpdateEnumerator(action));

		private static IEnumerator FireOnNextUpdateEnumerator(Action action)
		{
			yield return null;
			action?.Invoke();
		}

		public static void RunWhen(Func<bool> predicate, Action action) => Coroutines.StartCoroutine(RunWhenEnumerator(predicate, action));

		private static IEnumerator RunWhenEnumerator(Func<bool> prediate, Action action)
		{
			yield return new WaitUntil(prediate);
			action?.Invoke();
		}

		public static void FireInNUpdates(int n, Action action) => Coroutines.StartCoroutine(FireInNUpdatesEnumerator(n, action));

		private static IEnumerator FireInNUpdatesEnumerator(int n, Action action)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
			action?.Invoke();
		}
	}
}
