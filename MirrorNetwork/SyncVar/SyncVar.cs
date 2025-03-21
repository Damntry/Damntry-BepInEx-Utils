
//Entire class taken from here:
//https://storage.googleapis.com/mirror-api-docs/html/d8/dfa/_sync_var_8cs_source.html
//With some changes made to ease modding Unity games.

// SyncVar<T> to make [SyncVar] weaving easier.
//
// we can possibly move a lot of complex logic out of weaver:
//   * set dirty bit
//   * calling the hook
//   * hook guard in host mode
//   * GameObject/NetworkIdentity internal netId storage
//
// here is the plan:
//   1. develop SyncVar<T> along side [SyncVar]
//   2. internally replace [SyncVar]s with SyncVar<T>
//   3. eventually obsolete [SyncVar]
//
// downsides:
//   - generic <T> types don't show in Unity Inspector
//
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Mirror;
using UnityEngine;

namespace Damntry.UtilsBepInEx.MirrorNetwork.SyncVar {

	public interface ISyncVar {

		void SetToDefaultValue();

		bool Writable();

		void InitializeSyncObject(NetworkBehaviour netBehaviour);

	}

	// 'class' so that we can track it in SyncObjects list, and iterate it for
	//   de/serialization.
	[Serializable]
	public class SyncVar<T> : SyncObject, ISyncVar, IEquatable<T> {
		// Unity 2020+ can show [SerializeField]<T> in inspector.
		// (only if SyncVar<T> isn't readonly though)
		[SerializeField] protected T _Value;

		private NetworkBehaviour networkBehaviourContainer;

		// Value property with hooks
		// virtual for SyncFieldNetworkIdentity netId trick etc.
		public virtual T Value {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _Value;
			set {
				SetValue(value, true);
			}
		}

		protected void SetValue(T value, bool checkWritable) {
			if (checkWritable && !Writable()) {
				return;
			}
			// only if value changed. otherwise don't dirty/hook.
			// we have .Equals(T), simply reuse it here.
			if (!Equals(value)) {
				// set value, set dirty bit
				T old = _Value;
				_Value = value;
				if (OnDirty != null) {
					OnDirty();
				}

				// Value.set calls the hook if changed.
				// calling Value.set from within the hook would call the
				// hook again and deadlock. prevent it with hookGuard.
				// (see test: Hook_Set_DoesntDeadlock)
				if (!hookGuard &&
						//Damntry. Use SyncDirection and ownership to decide if its allowed to be called.
						//	Not having authority means it only receive updates, and must execute the callback.
						networkBehaviourContainer != null && !networkBehaviourContainer.authority) {
					hookGuard = true;
					InvokeCallback(old, value);
					hookGuard = false;
				}
			}
		}

		// OnChanged Callback.
		// Damntry: Text below obsolete since I allowed the callback to be
		// passed on the constructor.
		//
		// Named 'Callback' for consistency with SyncList etc.
		// Needs to be public so we can assign it in OnStartClient.
		// (ctor passing doesn't work, it can only take static functions)
		// assign via: field.Callback += ...!
		public Action<T, T> OnValueChangedCallback;

		// OnCallback is responsible for calling the callback.
		// this is necessary for inheriting classes like SyncVarGameObject,
		// where the netIds should be converted to GOs and call the GO hook.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected virtual void InvokeCallback(T oldValue, T newValue) =>
			OnValueChangedCallback?.Invoke(oldValue, newValue);

		// Value.set calls the hook if changed.
		// calling Value.set from within the hook would call the hook again and
		// deadlock. prevent it with a simple 'are we inside the hook' bool.
		bool hookGuard;

		/// <summary>
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </summary>
		public T DefaultValue { get; private set; }


		public override void ClearChanges() { }
		public override void Reset() { }

		/// <summary>
		/// The value of this SyncVar will be set to its default value if it is not owned 
		/// by this connection. Later it will be overwritten by the host/client value,
		/// depending on NetworkBehaviour.SyncDirection.
		/// This is to avoid the case where a SyncVar is intended to be managed by the 
		/// remote connection, but they dont have the mod installed, which results in this 
		/// SyncVar keeping its previous value and causing unintended consequences.
		/// </summary>
		public virtual void SetToDefaultValue() {
			NetworkSpawnManager.DebugLog(() => $"Setting SyncVar from {_Value} to its DefaultValue {DefaultValue}");
			_Value = DefaultValue;
		}

		/// <summary>
		/// Initializes a SyncVar, with both current value and default value as the one passed by parameter.
		/// </summary>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		public SyncVar(T defaultValue) {
			InitValues(defaultValue, defaultValue, null);
		}

		/// <summary>
		/// Initializes a SyncVar with the value passed by parameter.
		/// </summary>
		/// <param name="value">The initial value for the SyncVar.</param>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		public SyncVar(T value, T defaultValue) {
			InitValues(value, defaultValue, null);
		}

		/// <summary>
		/// Initializes a SyncVar with the value passed by parameter.
		/// </summary>
		/// <param name="value">The initial value for the SyncVar.</param>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		/// <param name="onValueChangedCallback">
		/// Method called in the non owner when the value changes. 
		/// The method must be declared in the same NetworkBehaviour as this SyncVar.
		/// </param>
		public SyncVar(T value, T defaultValue, Action<T, T> onValueChangedCallback) {
			OnValueChangedCallback += onValueChangedCallback;

			InitValues(value, defaultValue, null);
		}

		/// <summary>
		/// Initializes a SyncVar with the value passed by parameter.
		/// This constructor is only really useful when you want to skip the 
		/// whole automatic SyncVar initialization process to do it yourself.
		/// </summary>
		/// <param name="netBehaviour">
		/// The <see cref="NetworkBehaviour"/> instance from where the SyncVar is being initialized.
		/// </param>
		/// <param name="value">The initial value for the SyncVar.</param>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		public SyncVar(NetworkBehaviour netBehaviour, T value, T defaultValue) {
			// recommend explicit GameObject, NetworkIdentity, NetworkBehaviour
			// with persistent netId method
			/*
			if (this is SyncVar<GameObject>)
				Debug.LogWarning($"Use explicit {nameof(SyncVarGameObject)} class instead of {nameof(SyncVar<T>)}<GameObject>. It stores netId internally for persistence.");

			if (this is SyncVar<NetworkIdentity>)
				Debug.LogWarning($"Use explicit {nameof(SyncVarNetworkIdentity)} class instead of {nameof(SyncVar<T>)}<NetworkIdentity>. It stores netId internally for persistence.");

			if (this is SyncVar<NetworkBehaviour>)
				Debug.LogWarning($"Use explicit SyncVarNetworkBehaviour class instead of {nameof(SyncVar<T>)}<NetworkBehaviour>. It stores netId internally for persistence.");
			*/
			if (netBehaviour == null) {
				throw new ArgumentNullException(nameof(netBehaviour));
			}

			InitValues(value, defaultValue, netBehaviour);
		}

		/// <summary>
		/// Initializes a SyncVar with the value passed by parameter.
		/// This constructor is only really useful when you want to skip the 
		/// whole automatic SyncVar initialization process to do it yourself.
		/// </summary>
		/// <param name="netBehaviour">
		/// The <see cref="NetworkBehaviour"/> instance from where the SyncVar is being initialized.
		/// </param>
		/// <param name="value">The initial value for the SyncVar.</param>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		/// <param name="onValueChangedCallback">
		/// Method called in the Client when the value changes. 
		/// The method must be declared in the same NetworkBehaviour as this SyncVar.
		/// </param>
		public SyncVar(NetworkBehaviour netBehaviour, T value, T defaultValue, Action<T, T> onValueChangedCallback) {
			if (netBehaviour == null) {
				throw new ArgumentNullException(nameof(netBehaviour));
			}

			OnValueChangedCallback += onValueChangedCallback;

			InitValues(value, defaultValue, netBehaviour);
		}

		private void InitValues(T value, T defaultValue, NetworkBehaviour netBehaviour) {
			_Value = value;
			DefaultValue = defaultValue;
			
			if (typeof(T).IsEnum) {
				RegisterCustomEnum(value);
			}

			if (netBehaviour != null) {
				InitSyncObjectReflection(netBehaviour);
			}
		}

		//TODO Global 3 - SyncVar supports most basic C# types. Those are automatically generated by Weaver
		//	in the file "GeneratedNetworkCode.InitReadWriters" of the Assembly-CSharp.dll project being modded.
		//	Since we dont mod in the Unity editor with Mirror, any types not included in InitReadWriters
		//	must be defined manually. I ve only added support for Enums so far.
		//Check if there is any way for us to call Weaver manually and make it write the GeneratedNetworkCode 
		//	file, instead of doing it manually. Check Weaver.Weave()(...)

		/// <summary>
		/// Register the enum in Mirror network Reader and Writer.
		/// When using the Unity editor, this is generated automatically 
		/// by Mirror in GeneratedNetworkCode.cs.
		/// </summary>
		/// <typeparam name="TEnum"></typeparam>
		/// <param name="value"></param>
		private void RegisterCustomEnum<TEnum>(TEnum value) {
			//TODO Global 3 - Right now we asume they are long so we dont lose data.
			//	Change this to read the enum size and use the proper type.
			if (Writer<TEnum>.write == null) {
				Writer<TEnum>.write = (writer, value) => writer.WriteLong(EnumExtension.EnumToLong(value));
			}
			if (Reader<TEnum>.read == null) {
				Reader<TEnum>.read = (reader) => EnumExtension.LongToEnumUnconstrained<TEnum>(reader.ReadLong());
			}
		}

		/// <summary>
		/// Returns if the Value is currently modifiable.
		/// </summary>
		public bool Writable() {
			if (networkBehaviourContainer == null) {
				//If the SyncVar is not network initialized, only allow to write on it if we are offline.
				bool isNetworkOffline = !NetworkSpawnManager.IsNetworkOnline();
				NetworkSpawnManager.DebugLog(() => $"Writable returned {isNetworkOffline} for a non networked SyncVar!");

				return isNetworkOffline;
			}

			if (NetworkServer.active && NetworkClient.active) {
				return networkBehaviourContainer.syncDirection == SyncDirection.ServerToClient 
					|| networkBehaviourContainer.netIdentity.isOwned;
			}
			if (NetworkServer.active) {
				return networkBehaviourContainer.syncDirection == SyncDirection.ServerToClient;
			}
			if (NetworkClient.active) {
				return networkBehaviourContainer.netIdentity.netId == 0U ||
					(networkBehaviourContainer.syncDirection == SyncDirection.ClientToServer 
					&& networkBehaviourContainer.netIdentity.isOwned);
			}
			return true;
		}

		/// <summary>
		/// Only intended to be used for making the SyncVar netowrk ready manually. 
		/// </summary>
		/// <param name="netBehaviour">The NetworkBehaviour the SyncVar will be attached to.</param>
		public virtual void InitializeSyncObject(NetworkBehaviour netBehaviour) {
			//Never add in this method any code intended to be always executed with
			//	InitSyncObjectReflection, or derived methods wont use it.
			InitSyncObjectReflection(netBehaviour);
		}

		/// <summary>
		/// Performs the initialization of the SyncVar by registering it to the NetworkBehaviour instance.
		/// </summary>
		/// <param name="netBehaviour"></param>
		protected void InitSyncObjectReflection(NetworkBehaviour netBehaviour) {
			networkBehaviourContainer = netBehaviour;
			NetworkSpawnManager.DebugLog(() => $"Initializing SyncVar of Value type {typeof(T).Name} in {netBehaviour.name}");

			ReflectionHelper.CallMethod(netBehaviour, "InitSyncObject", [this]);

			NetworkSpawnManager.DebugLog(() => $"Finished initializing SyncVar of Value type {typeof(T).Name} in {netBehaviour.name}");
		}


		// implicit conversion: int value = SyncVar<T>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator T(SyncVar<T> field) => field.Value;

		// implicit conversion: SyncVar<T> = value
		// even if SyncVar<T> is readonly, it's still useful: SyncVar<int> = 1;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator SyncVar<T>(T value) => new SyncVar<T>(value);

		
		// serialization (use .Value instead of _Value so hook is called!)
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override void OnSerializeAll(NetworkWriter writer) => writer.Write(Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override void OnSerializeDelta(NetworkWriter writer) => writer.Write(Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override void OnDeserializeAll(NetworkReader reader) => SetValue(reader.Read<T>(), false);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override void OnDeserializeDelta(NetworkReader reader) => SetValue(reader.Read<T>(), false);


		// IEquatable should compare Value.
		// SyncVar<T> should act invisibly like [SyncVar] before.
		// this way we can do SyncVar<int> health == 0 etc.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(T other) =>
			// from NetworkBehaviour.SyncVarEquals:
			// EqualityComparer method avoids allocations.
			// otherwise <T> would have to be :IEquatable (not all structs are)
			EqualityComparer<T>.Default.Equals(Value, other);

		// ToString should show Value.
		// SyncVar<T> should act invisibly like [SyncVar] before.
		public override string ToString() => Value.ToString();

	}

}