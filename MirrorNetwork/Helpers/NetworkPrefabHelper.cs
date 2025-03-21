using System;
using System.Collections.Generic;
using System.Reflection;
using Damntry.Utils.Logging;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Helpers {
	public static class NetworkPrefabHelper {

		private static readonly string NetworkRootObjName = "NetworkedObjects";


		public static GameObject GetNetworkReadyPrefab<T>(string prefabName, out NetworkBehaviour networkBehaviour) 
				where T : NetworkBehaviour {

			GameObject prefabObj = new GameObject(prefabName);
			//Needs to be inactive to be properly initialized and spawned later by the network identity.
			prefabObj.SetActive(false);
			prefabObj.transform.parent = GetRootNetworkTransform();
			prefabObj.AddComponent<NetworkIdentity>();
			networkBehaviour = prefabObj.AddComponent<T>();

			return prefabObj;
		}

		private static Transform GetRootNetworkTransform() {
			//Any mod using this lib can create the parent object, so search if it already exists.
			GameObject networkParentObj = GameObject.Find(NetworkRootObjName);
			networkParentObj ??= new GameObject(NetworkRootObjName);

			return networkParentObj.transform;
		}

		public static bool AssetIdExists(uint assetId) => NetworkClient.prefabs.ContainsKey(assetId);

		public static bool IsPrefabGameObjectRegistered(GameObject prefabObj, uint expectedAssetId) =>
			NetworkClient.prefabs != null
				&& NetworkClient.prefabs.TryGetValue(expectedAssetId, out GameObject prefab)
				&& GameObject.ReferenceEquals(prefabObj, prefab);

		public static bool IsPrefabHandlerRegistered(SpawnHandlerDelegate spawnHandlerDelegate, uint expectedAssetId) {
			if (FindSpawnhandlerByAssetId(expectedAssetId, out bool IsReflectionError, out SpawnHandlerDelegate spawnDelegate)) {
				return GameObject.ReferenceEquals(spawnHandlerDelegate, spawnDelegate);
			}

			//If there was a reflection error, name might have changed because of a different mirror version.
			//	If thats the case, and since this is a confirmation check, we ll just have to continue
			//	and trust the prefab is there.
			return IsReflectionError;
		}

		/// <summary>
		/// Finds if there is a registered SpawnHandlerDelegate by assetId.
		/// </summary>
		/// <param name="assetId">The assetId of the SpawnHandlerDelegate to find.</param>
		/// <param name="IsFieldNotFound">If the  reflection errors happened.</param>
		/// <param name="spawnDelegate">The found SpawnHandlerDelegate, if any</param>
		/// <returns>True if the spawnHandler was found with that assetId.</returns>
		public static bool FindSpawnhandlerByAssetId(uint assetId, out bool IsFieldNotFound, out SpawnHandlerDelegate spawnDelegate) {
			spawnDelegate = null;
			IsFieldNotFound = false;
			try {
				string spawnHandlersName = "spawnHandlers";
				FieldInfo shInfo = typeof(NetworkClient).GetField("spawnHandlers", AccessTools.all);
				if (shInfo != null) {
					var spawnHandler = (Dictionary<uint, SpawnHandlerDelegate>)shInfo.GetValue(null);
					return spawnHandler.TryGetValue(assetId, out spawnDelegate);
				}
				TimeLogger.Logger.LogTimeError($"The field {spawnHandlersName} could not be found in type " +
					$"{typeof(NetworkClient).Name}.", LogCategories.Network);
			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeException(ex, LogCategories.Network);
			}

			IsFieldNotFound = true;
			return false;
		}


		/* Discarded assetId auto-generation.

		private const uint StartingAssetId = 12321;

		/// <summary>Current iteration of assetId used to register the network behaviour object</summary>
		private static uint iteration = 1;


		public static uint GetUniqueAssetId<T>() where T : NetworkBehaviour {
			//Generate a salt from the type so different mods have its own sequence of assetIds
			uint modSalt = (uint)typeof(T).GetHashCode();

			uint genAssetId = GenerateNextId(modSalt);
			while (AssetIdExists(genAssetId)) {
				genAssetId = GenerateNextId(modSalt);
			}

			return genAssetId;
		}

		private static uint GenerateNextId(uint modSalt) {
			unchecked {
				//Has to be deterministic so host and clients go through the same Id sequence.
				return (StartingAssetId + modSalt) * iteration++ * 3;
			}
		}

		public static void RestartAssetIdSequence() {
			iteration = 1;
		}
		*/

	}

}
