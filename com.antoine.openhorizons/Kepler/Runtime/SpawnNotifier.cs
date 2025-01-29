using System;
using UnityEngine;

namespace BigWorld.Kepler
{
	/// <summary>
	/// Component for tracking kepler bodies creation.
	/// </summary>
	public class SpawnNotifier : MonoBehaviour
	{
		private static event Action<KeplerOrbitMover> OnGlobalBodySpawnedEvent;

		public event Action<KeplerOrbitMover> onBodySpawnedEvent;

		private void Awake()
		{
			OnGlobalBodySpawnedEvent += OnGlobalNotify;
		}

		private void OnDestroy()
		{
			OnGlobalBodySpawnedEvent -= OnGlobalNotify;
		}

		private void OnGlobalNotify(KeplerOrbitMover b)
		{
			onBodySpawnedEvent?.Invoke(b);
		}

		public void NotifyBodySpawned(KeplerOrbitMover b)
		{
			OnGlobalBodySpawnedEvent?.Invoke(b);
		}
	}
}