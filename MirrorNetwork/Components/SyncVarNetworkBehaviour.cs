using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.MirrorNetwork.Attributes;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;
using Mirror;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Components {

	internal interface ISyncVarBehaviour {

		void StartNetworkSession(NetworkMode networkMode);

	}

	/// <summary>
	/// Primarily meant to be used to automatically initialize <see cref="SyncVar{T}"/> objects 
	/// annotated with the attribute <see cref="SyncVarNetworkAttribute"/>
	/// </summary>
	/// <typeparam name="T">The class that derives from this SyncVarNetworkBehaviour.</typeparam>
	public abstract class SyncVarNetworkBehaviour<T> : NetworkBehaviour, ISyncVarBehaviour 
			where T : SyncVarNetworkBehaviour<T> {


		//TODO Global 6 - If I host with a mod using this, and a client joins without the mod, everything
		//	works on the host side, but the client receives isValid in the console when the host calls
		//	Spawn(), since it doesnt know what to do with that data.
		//	The error doesnt cause any issues in itself.
		//	No idea how to fix this now outside of patching Mirror to control who receives certain data,
		//	which is over the top so it ll have to stay for now.
		
		private static readonly BindingFlags SearchFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance |
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;


		private readonly Type derivedType = typeof(T);

		private readonly List<ISyncVar> validSyncVarList = new();

		/// <summary>
		/// A new network session has been started, and Mirror objects were already spawned.
		/// SyncVars that are to be used should be instantiated here or earlier.
		/// </summary>
		/// <param name="networkMode">The current network mode.</param>
		protected virtual void OnStartNetworkSession(NetworkMode networkMode) { }

		/// <summary>
		/// Valid SyncVars have been set to its default values in preparation for their network initialization.
		/// SyncVars values cannot be changed at this point until they have been made network ready.
		/// </summary>
		protected virtual void OnSyncVarValuesDefaulted() { }

		/// <summary>
		/// Valid SyncVars have been fully network initialized and are ready to be used.
		/// This method is called before OnStartClient().
		/// </summary>
		protected virtual void OnSyncVarsNetworkReady() { }


		/// <summary>
		/// A new network session has been started, and Mirror objects were already spawned.
		/// All valid SyncVars are validated to be used later.
		/// </summary>
		void ISyncVarBehaviour.StartNetworkSession(NetworkMode networkMode) {
			NetworkSpawnManager.DebugLog(() => $"{nameof(OnStartNetworkSession)} call begins for type {derivedType.Name}.");
			OnStartNetworkSession(networkMode);

			validSyncVarList.Clear();

			var members = GetDerivedSyncVarsOfType(typeof(SyncVar<>));
			foreach (MemberInfoHelper syncVarInfoHelper in members) {
				bool isValid = CheckSyncVarValidity(syncVarInfoHelper, out ISyncVar syncVarInstance);
				if (!isValid) {
					//Skip from later network initialization
					continue;
				}

				validSyncVarList.Add(syncVarInstance);

				syncVarInstance.SetToDefaultValue();
			}

			OnSyncVarValuesDefaulted();
		}

		private void Awake() {
			foreach (ISyncVar syncVarInstance in validSyncVarList) {
				InitializeSyncVar(syncVarInstance, (T)this);
			}
			validSyncVarList.Clear();

			NetworkSpawnManager.DebugLog(() => $"{nameof(OnSyncVarsNetworkReady)} call begins for type {derivedType.Name}.");
			OnSyncVarsNetworkReady();
		}

		/// <summary>
		/// Gets a list of SyncVar members declared in the deriving class.
		/// </summary>
		/// <param name="searchType">
		/// The base type of SyncVar to search for. Classes deriving from the type are also included.
		/// </param>
		private List<MemberInfoHelper> GetDerivedSyncVarsOfType(Type searchType) {
			//Find all fields and properties declared in the deriving class
			var members = derivedType.GetFields(SearchFlags).Cast<MemberInfo>()
				.Concat(derivedType.GetProperties(SearchFlags)).Cast<MemberInfo>();

			if (members == null || members.Count() == 0) {
				TimeLogger.Logger.LogTimeWarning($"{derivedType.Name} inherits {nameof(SyncVarNetworkBehaviour<T>)} " +
					$"to sync variables, but has no fields or properties to do so.", LogCategories.Network);
				return [];
			}

			return members
				.Where(mi => mi.HasCustomAttribute<SyncVarNetworkAttribute>())
				.Select(mi => new MemberInfoHelper(mi))
				.Where(mif => mif.MemberInfoType.IsSubclassOfRawGeneric(searchType))
				.ToList();
		}

		private bool CheckSyncVarValidity(MemberInfoHelper syncVarInfoHelper, out ISyncVar syncVarInstance) {
			syncVarInstance = null;
			Type syncVarType = syncVarInfoHelper.MemberInfoType;

			if (!syncVarType.IsSubclassOf(typeof(SyncObject))) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} does not " +
					$"inherit from {nameof(SyncObject)} and will be skipped. Make sure that the " +
					$"{nameof(SyncVarNetworkAttribute)} annotation was intended.", LogCategories.Network);
				return false;
			}
			if (!typeof(ISyncVar).IsAssignableFrom(syncVarType)) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} does not " +
					$"inherit from {nameof(ISyncVar)} and will be skipped. Make sure that the " +
					$"{nameof(SyncVarNetworkAttribute)} annotation was intended.", LogCategories.Network);
				return false;
			}

			if (syncVarType.GetGenericArguments() == null || syncVarType.GetGenericArguments().Length == 0) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} does not " +
					$"declare any generic type parameters and will be skipped. Make sure that the type derives " +
					$"from {nameof(SyncVar<object>)}.", LogCategories.Network);
				return false;
			}

			syncVarInstance = (ISyncVar)syncVarInfoHelper.GetValueStaticAgnostic((T)this);
			if (syncVarInstance == null) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} has not " +
					$"been instantiated and will be skipped. Make sure to call its constructor.", LogCategories.Network);
				return false;
			}

			return true;
		}



		private void InitializeSyncVar(ISyncVar syncVarInstance, T netBehaviourInstance) {
			if (!IsSyncVarNetworkInitialized((SyncObject)syncVarInstance)) {
				syncVarInstance.InitializeSyncObject(netBehaviourInstance);
			}

			/* Obsolete automatic instantiation. In the end I decided to have the user set the initial 
			 * constructor values since attribute parameters can be a bit awkward to use at times.
			 * I might readd this as an extra at some point.
				
				//Create new instance of this SyncVar<T> using previous DefaultValue as current value.
				Type syncVarType = syncVarInfoHelper.MemberInfoType;
				try {
					syncVarInstance = (ISyncVar)Activator.CreateInstance(syncVarType,
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, 
						[syncVarInstance, this, syncVarInfoHelper.Name], null, null);
					if (syncVarInstance == null) {
						TimeLogger.Logger.LogTimeWarning($"The SyncObject {syncVarInfoHelper.Name} of type {syncVarType} could " +
							$"not be instantiated for an unknown reason.", LogCategories.Network);
						return;
					}
				} catch (Exception ex) {
					TimeLogger.Logger.LogTimeExceptionWithMessage($"The SyncObject {syncVarInfoHelper.Name} of type {syncVarType} " +
							$"could not be instantiated.", ex, LogCategories.Network);
					return;
				}

				var declaringTypeInstance = syncVarInfoHelper.IsStatic ? null : (T)this;
				syncVarInfoHelper.SetValue(declaringTypeInstance, syncVarInstance);
			*/

		}

		private bool IsSyncVarNetworkInitialized(SyncObject syncVarInstance) => syncObjects.Contains(syncVarInstance);


		//TODO 2 - Make this into a Globals method as generic as possible.
		private Delegate GetOnChangeDelegate(string onChangeMethodName, T netBehaviourInstance, Type syncVarType) {

			//Get type T used as value in the SyncVar
			Type syncVarValueType = syncVarType.GetGenericArguments()[0];

			//Get OnChange method
			MethodInfo onChange = derivedType.GetMethod(onChangeMethodName, SearchFlags,
				null, [syncVarValueType, syncVarValueType], null);
			if (onChange == null) {
				TimeLogger.Logger.LogTimeWarning($"Method \"{onChangeMethodName}\" could not be " +
					$"found in type {derivedType.Name}, or does not have the required signature:  " +
					$"{onChangeMethodName}({syncVarType.Name} oldValue, {syncVarType.Name} newValue).",
					LogCategories.Network);
				return null;
			}

			//Create delegate from MethodInfo to reduce reflection performance hit when invoking
			Type actionGeneric = typeof(Action<,>).MakeGenericType(syncVarValueType, syncVarValueType);

			Delegate methodDelegate = Delegate.CreateDelegate(actionGeneric, netBehaviourInstance, onChange);
			if (methodDelegate == null) {
				TimeLogger.Logger.LogTimeWarning($"A delegate for method " +
					$"\"{derivedType.Name}.{onChangeMethodName}\" could not be created.",
					LogCategories.Network);
				return null;
			}

			return methodDelegate;
		}

	}

}
