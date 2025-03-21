using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Damntry.Utils.Logging;
using Damntry.Utils.Maths;
using Damntry.UtilsBepInEx.MirrorNetwork.Components;
using HarmonyLib;
using Mirror;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Helpers {

	//TODO Global 4 - Optionally pass Scenes (by id? name? ...) to the RegisterNetwork method, for the 
	//	AddNetworkSpawns method to filter out or in the NetworkBehaviour.

	/*  

	Introduction

		This framework provides automation under the hood for online syncing of objects
		between host and client/s. It tries to make your life easier by doing most of the
		boilerplate, while still being customizable.
		
		To this end, two new classes were introduced: SyncVar and SyncVarSetting.
		SyncVar requires you to set its value manually, while SyncVarSetting is tied to 
		a Bepinex ConfigEntry, so when the config value changes, the SyncVarSetting value does so too.

		When a SyncVar/SyncVarSetting value is changed, it checks if the player has permissions to do
		so depending on sync direction (server to client by default, configurable) and ownership.
		If it has permissions, the value is assigned and sent to the opposite connection, but if not, the value is 
		neither set locally, nor sent.


	*************************************************************************************

	Example of use, step by step:

	- Choose an unique Id signature for your mod. It must be a number between 1 and 4293, both inclusive.

	- On the entry point of your mod (Plugin.cs by default), call:

		NetworkSpawnManager.Initialize(<IdModSignature>);

	- Now, we create a class that handles some synced vars.
	  The class needs to inherit NetworkBehaviour, but you ll want to make it inherit SyncVarNetworkBehaviour<T> 
	  specifically, so it takes care of SyncVar network logic. 
	  This is an example of the simplest, working class you can create

		public class NetworkBehaviourExample1 : SyncVarNetworkBehaviour<NetworkBehaviourExample1> {

			[SyncVarNetwork]
			public static SyncVar<bool> TestSyncVar { get; private set; } = new SyncVar<bool>(false)

		}
		
	  This class once registered (shown further below) will already synchronize its value from server to 
	  connected clients, and any new clients joining will also receive its current value.
	  

	- The SyncVar can be static or not, a property or a field, and can be instanced in any way you 
		consider appropriate. The only requirement is that it is instanced before, or in, the method 
		OnStartNetworkSession().
		Here is a example of a class with the full range of methods available to you (Most,
		of these methods wont be necessary for the majority of use cases):


		public class NetworkBehaviourExample2 : SyncVarNetworkBehaviour<NetworkBehaviourExample2> {

			[SyncVarNetwork]
			public SyncVar<int> TestSyncVarWithOnChange;

			#region Inherited methods

				protected override void OnStartNetworkSession(NetworkMode networkMode) {
					//Network session starts
					TestSyncVarWithOnChange = new(5, 0, onValueChangedParam);
				}

				protected override void OnSyncVarValuesDefaulted() {
					//SyncVar values are set to its default value in preparation for spawning.
				}

				protected override void OnSyncVarsNetworkReady() {
					//SyncVars were initialized and are network ready.
				}

				public override void OnStartClient() {
					//Mirror method
				}

			#endregion

			public static void onValueChangedParam(int oldValue, int newValue) {
				//Called automatically when the value changes
			}

		}

	- The last step is to register your NetworkBehaviours. This only needs to be done once before any 
	  networked game session starts.
	  The parameter is an unique id for the networkBehaviour. Must be > 0 and have less than 7 digits [1 to 999999].

		NetworkSpawnManager.RegisterNetwork<MyNetworkBehaviourExample1>(<UniqueId1>);
		NetworkSpawnManager.RegisterNetwork<MyNetworkBehaviourExample2>(<UniqueId2>);

	- And done. Now, when the value of any of those SyncVars is changed by the server, it will be
		recieved and set in the clients.
	

	*************************************************************************************

	Technical details

	- SyncVar Types supported
		Most basic C# types works. For a list of supported types, there should be a file named
		"GeneratedNetworkCode.cs" in the Assembly-CSharp.dll of the project you are modding. 
		Look for the method InitReadWriters(), it will contain the types supported in that game.
		I ve coded in support for Enums, but for any other types not already supported, you would
		have to create your own Writer/Read implementation. 
		In normal cases, Mirror Weaver would automatically generate a complete GeneratedNetworkCode.cs
		file from the Unity editor, but this is not possible outside of it.
		More info here: https://mirror-networking.gitbook.io/docs/manual/guides/serialization
		Support for more types will be added eventually.
	
	- Sync direction and ownership
		The Server <-> Client direction signals what to do when a SyncVar value is changed.
		If the value is being changed from the target direction, The SyncVar value is not changed and 
		the command is completely ignored.
		This direction can be set in your NetworkBehaviour class with the "syncDirection" field, and 
		affects all SyncVars inside that class.
		Alternatively, you can set "netIdentity.isOwned" to true, from the NetworkBehaviour class, which
		allows the SyncVars in it to alway set and send data regardless of syncDirection.

	- SyncVar.DefaultValue is meant to be used as what would be a Disabled/Vanilla value.
		This is so when we connect to a host that doesnt have the mod, the SyncVar acts as 
		if the setting is disabled.

	- Server headless mode and offline mode are not really all that well supported. It might work, 
		but not without having to manually do some extra work, if even that.

	- Unique Ids
		The unique ids are required so NetworkBehaviour have an unique assetId assigned.
	  With the signature id when initializing the NetworkSpawnManager, and the parameter 
	  id when registering, they get combined to generate a complete assetId.
	  The idea of having the signature id separate from the register id is a convenience, so when some 
	  other mod using this framework, also has the same signature id as you, you can fix it by simply changing your own 
	  signature without having to worry about also manually changing all the individual ids from 
	  registered NetworkBehaviours.

	*************************************************************************************

	Network initialization flow and lifecycle:

	- Game starts loading new scene
	- Existing Unity networked objects are spawned
	* If any of the Mirror network objects (NetworkServer/NetworkClient) is active:
		* For each NetworkBehaviour previously registered with NetworkSpawnManager.RegisterNetwork:
			- An inactive GameObject is created, with a NetworkIdentity and registered NetworkBehaviour attached to it.
			* For each SyncVar that is statically initialized (manually) in the NetworkBehaviour.
				- SyncVar is instantiated, but with no network capabilities yet.
			* Only for SyncVarNetworkBehaviour classes:
				- SyncVarNetworkBehaviour.OnStartNetworkSession() is called.
					** This method is your last chance to instantiate SyncVars that were not statically initialized.
				* For each instanced SyncVar that is annotated with the [SyncVarNetwork] attribute.
					- SyncVar Value is set to its preset DefaultValue.
				- SyncVarNetworkBehaviour.OnSyncVarValuesDefaulted() is called.
		* If Host:
			- All registered NetworkBehaviours are spawned and enabled as a fully functioning network object.
		* If Client:
			- Receive network messages from Host with the specific NetworkBehaviours to be spawned locally, by assetId.
			- All NetworkBehaviours that match the received assetIds and were also registered locally, are spawned and enabled as a fully functioning network object.
		* For each NetworkBehaviour spawned and enabled
			- Its Unity Awake() is called
			* Only for SyncVarNetworkBehaviour classes:
				* For each SyncVar in the SyncVarNetworkBehaviour
					- SyncVars are initialized to be network ready.
					* Only for SyncVarSetting
						- If the SyncVarSetting is writable, its Value is set to the one from its ConfigEntry, and if a onValueChangedCallback was specified it gets called.
				- SyncVarNetworkBehaviour.OnSyncVarsNetworkReady() is called.
	- Process finished. Now Mirror can resume finishing its own logic and call OnStartClient(), etc...

 */

	public enum NetworkMode {
		NotInitialized,
		Offline,
		ServerOnly,
		ClientOnly,
		Host
	}

	[HarmonyPatch]
	public class NetworkSpawnManager {

		private readonly static Harmony harmony;

		private readonly static Dictionary<Type, INetworkPrefabSpawner> networkBehaviourRegistry;

		private static uint _assetIdSignature;

		public static bool NetworkDebugLog = true;

		static NetworkSpawnManager() {
			harmony = new(typeof(NetworkSpawnManager).FullName);
			networkBehaviourRegistry = new();
		}


		public static bool IsNetworkActive() => NetworkServer.active || NetworkClient.active;

		public static bool IsNetworkOnline() =>
			NetworkManager.singleton != null && NetworkManager.singleton.mode != NetworkManagerMode.Offline;

		/// <summary>
		/// Returns true if we are currently hosting a game, whether we are actively playing or in headless mode.
		/// </summary>
		public static bool IsNetworkOnlineHosting() =>
			NetworkManager.singleton != null && 
				NetworkManager.singleton.mode == NetworkManagerMode.Host ||
				NetworkManager.singleton.mode == NetworkManagerMode.ServerOnly;

		/// <summary>Returns true if we have joined a hosted game as a client.</summary>
		public static bool IsNetworkClientOnly() =>
			NetworkManager.singleton != null &&
				NetworkManager.singleton.mode == NetworkManagerMode.ClientOnly;

		public static NetworkMode GetCurrentNetworkMode() =>
			NetworkManager.singleton == null ? NetworkMode.NotInitialized :
				NetworkManager.singleton.mode switch {
					NetworkManagerMode.Offline => NetworkMode.Offline,
					NetworkManagerMode.ServerOnly => NetworkMode.ServerOnly,
					NetworkManagerMode.ClientOnly => NetworkMode.ClientOnly,
					NetworkManagerMode.Host => NetworkMode.Host,
					_ => throw new NotImplementedException()
				};


		/// <summary>
		/// Initializes the spawner manager. finished loading.
		/// </summary>
		/// <param name="assetIdSignature">
		/// The beginning numbers of all generated AssetIds in this mod.
		/// This number must be a value between 1 and 4293, both inclusive, so it can fit within an uint.
		/// </param>
		public static void Initialize(int assetIdSignature) {
			if (assetIdSignature < 1 || assetIdSignature > 4293) {
				TimeLogger.Logger.LogTimeFatal($"The value of assetIdSignature is {assetIdSignature} " +
					$"but it must be between 1 and 4293, both inclusive. " +
					"All related network functionality will not work.", LogCategories.Network);
				return;
			}

			_assetIdSignature = (uint)assetIdSignature;

			harmony.PatchAll(typeof(NetworkSpawnManager));

			if (!harmony.GetPatchedMethods().Any()) {
				TimeLogger.Logger.LogTimeFatal("NetworkSpawnManager patch failed. Modded network features wont work.", LogCategories.Network);
			}
		}

		//Hook after objects have been spawned or registered, so we give preference to existing assetIds, if any.
		[HarmonyPatch(typeof(NetworkServer), nameof(NetworkServer.SpawnObjects))]
		[HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.PrepareToSpawnSceneObjects))]
		[HarmonyPostfix]
		public static void OnStartNetworkSessionPrefix(MethodBase __originalMethod) {
			DebugLog(() => $"OnStartNetworkSessionPrefix - Coming from method " +
				$"{__originalMethod.Name} - Network mode: {NetworkManager.singleton.mode}");

			if (IsNetworkActive()) {
				DebugLog("Mirror network system is active. Adding network spawns");
				AddNetworkSpawns();
			} else {
				DebugLog("Mirror network system is NOT active. Skipping this session initialization");
			}
		}

		/// <summary>
		/// Registers the NetworkBehaviour to be added and spawned in the network.
		/// It is recommended to use a <see cref="SyncVarNetworkBehaviour{T}"/> to
		/// automate most of the manual work, but a normal NetworkBehaviour works too.
		/// </summary>
		/// <typeparam name="T">The type of NetworkBehaviour</typeparam>
		/// <param name="assetId">
		/// The unique assetId for this networkBehaviour. Must be > 0 and have less than 7 digits [1 to 999999].
		/// </param>
		/// <exception cref="InvalidOperationException">Thrown when a duplicated NetworkBehaviour is added.</exception>
		public static void RegisterNetwork<T>(uint assetId)
				where T : NetworkBehaviour {

			if (assetId == 0) {
				throw new ArgumentException("The assetId parameter cant be 0.");
			}
			if (MathMethods.CountDigits(assetId) > 6) {
				throw new ArgumentException("The assetId parameter must have less than 7 digits [1 to 999999].");
			}
			Type netBehaviourType = typeof(T);
			if (networkBehaviourRegistry.ContainsKey(netBehaviourType)){
				throw new InvalidOperationException($"The NetworkBehaviour of type {netBehaviourType.Name} " +
					$"was already registered.");
			}

			uint fullAssetId = GetAssetIdFromSignature(assetId);
			var netSpawner = new NetworkPrefabSpawner<T>(fullAssetId);

			networkBehaviourRegistry.Add(netBehaviourType, netSpawner);
		}

		/*
		/// <summary>
		/// *****  This method is completely experimental and untested. Use at your own risk  *****
		/// And not even finished,
		/// </summary>
		public static void UnregisterNetwork<T>() {
			Type netBehaviourType = typeof(T);

			if (!networkBehaviourRegistry.TryGetValue(netBehaviourType, out INetworkPrefabSpawner netBehaviour)) {
				//Show Error: Network is not registered locally
				XXX
				return;
			}

			//XXXX Move this logic to new method NetworkPrefabSpawner<T>.RemoveFromPrefabs
			if (!NetworkPrefabHelper.FindSpaw<nhandlerByAssetId(netBehaviour.DefinedAssetId, 
					out _, out SpawnHandlerDelegate spawnDelegate)) {
				//Show error: Prefab is not registered in Mirror
				XXX
			} else {
				try {
					//XXXX If the network is in use with its syncvars and everything, call a Destroy
					//	method in the SyncVarNetworkBehaviour to null everything?
					//	But what happens to a normal NetworkBehaviour?

					
					//GameObject prefabObj = spawnDelegate(new SpawnMessage());
					//UnityEngine.Object.Destroy(prefabObj);
					
					NetworkClient.UnregisterSpawnHandler(netBehaviour.DefinedAssetId);
				} finally {
					networkBehaviourRegistry.Remove(netBehaviourType);
				}
			}
		}
		*/

		private static void AddNetworkSpawns() {
			foreach (KeyValuePair<Type, INetworkPrefabSpawner> keyValue in networkBehaviourRegistry) {
				keyValue.Value.AddToPrefabs();
			}

			if (NetworkServer.active) {
				SpawnHost();
			}
		}

		private static void SpawnHost() {
			NetworkSpawnManager.DebugLog(() => $"Spawning objects - NetworkServer active? {NetworkServer.active}");
			if (NetworkServer.active) {
				//Mirror will replicate it onto the client/s.
				foreach (KeyValuePair<Type, INetworkPrefabSpawner> keyValue in networkBehaviourRegistry) {
					keyValue.Value.Spawn();
				}
			}
		}


		private static uint GetAssetIdFromSignature(uint assetId) {
			return _assetIdSignature * 1000000U + assetId;
		}

		public static void DebugLog(Func<string> textLambda) {
#if DEBUG
			LOG.Debug_func(textLambda, LogCategories.Network, NetworkDebugLog);
#endif 
		}

		public static void DebugLog(string text) {
#if DEBUG
			LOG.Debug(text, LogCategories.Network, NetworkDebugLog);
#endif
		}


	}

}
