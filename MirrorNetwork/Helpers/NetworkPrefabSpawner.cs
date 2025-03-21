using System.Collections.Generic;
using System;
using Damntry.Utils.Logging;
using Mirror;
using UnityEngine;
using Damntry.UtilsBepInEx.MirrorNetwork.Components;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Helpers {

	public interface INetworkPrefabSpawner {

		Type NetworkBehaviourType { get; }
		
		uint DefinedAssetId { get; }

		/// <summary>
		/// Registers the NetworkSpawner behaviour into the predefined RootNetworkTransform, 
		/// in preparation for its later spawning.
		/// </summary>
		void AddToPrefabs();

		/// <summary>
		/// Spawns and activates the entity in the network server. Usually called when hosting starts.
		/// </summary>
		void Spawn();


	}

	public class NetworkPrefabSpawner<T> : INetworkPrefabSpawner
			where T : NetworkBehaviour {

		private GameObject prefabObj;

		/// <summary>The user provided assetId.</summary>
		public uint DefinedAssetId { get; init; }

		public bool PrefabRegisterOk { get; private set; }


		public Type NetworkBehaviourType { get; private set; }

		private NetworkBehaviour networkBehaviourInstance;


		public NetworkPrefabSpawner(uint assetId) {
			NetworkBehaviourType = typeof(T);
			DefinedAssetId = assetId;
		}

		public void AddToPrefabs() {
			string typeName = NetworkBehaviourType.Name;
			
			prefabObj = NetworkPrefabHelper.GetNetworkReadyPrefab<T>(typeName, out networkBehaviourInstance);

			if (prefabObj == null) {
				TimeLogger.Logger.LogTimeError($"A prefab object couldnt be created from type {typeName}", 
					LogCategories.Network);
				return;
			}
			
			uint assetId = DefinedAssetId;
			if (NetworkPrefabHelper.AssetIdExists(assetId)) {
				TimeLogger.Logger.LogTimeError($"The specified assetId \"{assetId}\" for the NetworkBehaviour " +
					$"class {typeName} was already found in the NetworkClient.", LogCategories.Network);
				return;
			}

			SpawnHandlerDelegate spawnHandlerDelegate = (SpawnMessage msg) => {
				//No need to use the message since prefabs are stored individually, for now.
				return prefabObj;
			};
			UnSpawnDelegate unSpawnDelegate = (GameObject spawned) => {
				//TODO Global 3 - Right now Im keeping the networked object alive once initialized.
				//	I would want to null it when the session has finished but then I would lose
				//		whatever value the user initialized it with. 3 options:
				//	- Manually do the opposite process of initSyncObject. This could fail in a different/future Mirror version.
				//	- Clone the user initialized version before it goes through initSyncObject. But then
				//		I would be using a bit more memory to save some, and overcomplicate everything.
				//	- Make the user initialize the values in a specific method that can be called by
				//		the SyncVarNetworkBehaviour.
			};

			//Register with SpawnHandler delegates to avoid the unnecessary object cloning that the other Mirror
			//	spawning system uses (via Object.Instantiate). This also preserves our GameObject hierarchy.
			NetworkClient.RegisterPrefab(prefabObj, DefinedAssetId, spawnHandlerDelegate, unSpawnDelegate);

			//Check manually if it has been correctly registered.
			PrefabRegisterOk = NetworkPrefabHelper.IsPrefabHandlerRegistered(spawnHandlerDelegate, DefinedAssetId);
			if (!PrefabRegisterOk) {
				//Whatever error there was, was already logged in the RegisterPrefab method.
				NetworkSpawnManager.DebugLog(() => $"Prefab NOT registered for NetworkBehaviour {typeName}.");
				return;
			}
			NetworkSpawnManager.DebugLog(() => $"Prefab was registered correctly for NetworkBehaviour {typeName}.");

			TriggerStartNetworkSession(NetworkSpawnManager.GetCurrentNetworkMode());
		}

		private void TriggerStartNetworkSession(NetworkMode networkMode) {
			if (typeof(ISyncVarBehaviour).IsAssignableFrom(typeof(T))) {
				((ISyncVarBehaviour)networkBehaviourInstance).StartNetworkSession(networkMode);
			}
		}

		public void Spawn() {
			if (!PrefabRegisterOk) {
				return;
			}
			if (prefabObj == null) {
				TimeLogger.Logger.LogTimeFatal($"The prefab object to spawn for type {NetworkBehaviourType.Name}. " +
					$"is null. Make sure to call AddToPrefabs() before spawning happens.", LogCategories.Network);
				return;
			}
			NetworkServer.Spawn(prefabObj);
		}

	}

	/*
	public class NetworkSpawnerComparer<T> : IEqualityComparer<T>
			where T : INetworkPrefabSpawner {

		public bool Equals(T o1, T o2) {
			return o1.NetworkBehaviourType == o2.NetworkBehaviourType;
		}

		/// <summary>
		/// Taken from https://stackoverflow.com/a/263416/739345 by Jon Skeet
		/// </summary>
		public int GetHashCode(T o) {
			if (o == null) {
				throw new ArgumentNullException($"The {nameof(INetworkPrefabSpawner)} object cant be null.");
			}

			unchecked { // Overflow is fine, just wrap
				int hash = 83;

				hash = hash * 3323 + o.NetworkBehaviourType.GetHashCode();
				hash = hash * 3323 + o.NetworkBehaviourType.GetHashCode();
				return hash;
			}
		}

	}
	*/

}
