using System;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Mirror;

namespace Damntry.UtilsBepInEx.MirrorNetwork.SyncVar {

	/// <summary>
	/// Synced variable between host and clients. While in a game, setting 
	/// the value will only apply when hosting. Otherwise it does nothing.
	/// </summary>
	/// <typeparam name="T">The type that holds the value.</typeparam>
	public class SyncVarSetting<T> : SyncVar<T>, ISyncVar {

		public ConfigEntry<T> ConfigEntry { get; private set; }

		/// <summary>
		/// Initializes a SyncVarSetting with the default value and <see cref="ConfigEntry{T}"/> passed by parameter.
		/// Any changes made in the ConfigEntry value will be propagated automatically to the SyncVar.
		/// </summary>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		/// <param name="configEntry">
		/// The <see cref="ConfigEntry{T}"/> from which to keep the SyncVar Value updated.
		/// </param>
		public SyncVarSetting(T defaultValue, ConfigEntry<T> configEntry) :
				base(defaultValue, defaultValue) {

			Init(configEntry);
		}

		/// <summary>
		/// Initializes a SyncVarSetting with the default value and <see cref="ConfigEntry{T}"/> passed by parameter.
		/// Any changes made in the ConfigEntry value will be propagated automatically to the SyncVar.
		/// </summary>
		/// <param name="defaultValue">
		/// The SyncVar will automatically set its value to this defaultValue when starting an online session.
		/// Should be the equivalent of a Vanilla game value, or one that disables the modded functionality.
		/// </param>
		/// <param name="onValueChangedCallback">
		/// Method called in the Client when the value changes. 
		/// The method must be declared in the same NetworkBehaviour as this SyncVar.
		/// </param>
		/// <param name="configEntry">
		/// The <see cref="ConfigEntry{T}"/> from which to keep the SyncVar Value updated.
		/// </param>
		public SyncVarSetting(T defaultValue, Action<T, T> onValueChangedCallback, ConfigEntry<T> configEntry) :
				base(defaultValue, defaultValue, onValueChangedCallback) {

			Init(configEntry);
		}

		private void Init(ConfigEntry<T> configEntry) {
			if (configEntry == null) {
				throw new ArgumentNullException(nameof(configEntry), $"{nameof(SyncVarSetting<object>)} ConfigEntry cannot be null. " +
					"Make sure that Bepinex config binding has been completed successfully before this call. " +
					$"If not using a ConfigEntry is intended, use {nameof(SyncVar<object>)} instead.");
			}
			NetworkSpawnManager.DebugLog(() => $"{nameof(SyncVarSetting<object>)} constructor. " +
				$"Setting SyncVar to its configEntry value ({configEntry.Value}).");

			ConfigEntry = configEntry;

			this._Value = configEntry.Value;

			RegisterSyncvarSetting();
		}

		/// <summary>
		/// Ties the SyncVar Value to the one set in its <see cref="ConfigEntry{T}"/>.
		/// </summary>
		public void RegisterSyncvarSetting() {
			UnregisterSyncvarSetting();		//Remove existing, if any, to avoid duplicates.
			ConfigEntry.SettingChanged += SetValueFromConfig;
		}

		/// <summary>
		/// Removes the SyncVar dependency from its <see cref="ConfigEntry{T}"/> Value.
		/// </summary>
		public void UnregisterSyncvarSetting() {
			ConfigEntry.SettingChanged -= SetValueFromConfig;
		}

		/// <summary>
		/// Only intended to be used for making the SyncVar netowrk ready manually. 
		/// </summary>
		/// <param name="netBehaviour">The NetworkBehaviour the SyncVar will be attached to.</param>
		public override void InitializeSyncObject(NetworkBehaviour netBehaviour) {
			InitSyncObjectReflection(netBehaviour);

			NetworkSpawnManager.DebugLog(() => $"{nameof(SyncVarSetting<object>)} fully initialized. " +
				$"Setting SyncVar from {_Value} to its configEntry value ({ConfigEntry.Value}).");
			SetValueFromConfig();
		}

		private void SetValueFromConfig(object sender, EventArgs e) => SetValueFromConfig();

		/// <summary>
		/// Sets the value of the SyncVar to the current one from its <see cref="ConfigEntry{T}"/> 
		/// </summary>
		public void SetValueFromConfig() => Value = ConfigEntry.Value;


		// implicit conversion: int value = SyncVarSetting<T>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator T(SyncVarSetting<T> field) => field.Value;

	}
}
