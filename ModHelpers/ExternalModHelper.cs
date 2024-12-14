using System;
using BepInEx.Bootstrap;
using BepInEx;


namespace Damntry.UtilsBepInEx.ModHelpers {

	/// <summary>
	/// Provides basic functionality to detect if an external mod is present,
	/// and manages its info and version compatibilities against our supported
	/// version, aswell as general error checking.
	/// </summary>
	public abstract class ExternalModHelper {

		protected ModLoadStatus ModStatus { get; private set; }

		public bool IsModLoadedAndEnabled {
			get {
				return ModStatus != ModLoadStatus.NotLoaded;
			}
		}

		public ModInfoData ModInfo { get; private set; }


		protected enum ModLoadStatus {
			NotLoaded,
			DifferentVersion,
			LoadedOk
		}

		private ExternalModHelper() { }

		public ExternalModHelper(string GUID, string modName, Version supportedVersion) {
			checkArgsForErrors(GUID, modName, supportedVersion);
			Init(GUID, modName, supportedVersion);
		}

		public ExternalModHelper(string GUID, string modName, string supportedVersion) {
			checkArgsForErrors(GUID, modName, supportedVersion, out Version parsedSupportedVersion);
			Init(GUID, modName, parsedSupportedVersion);
		}

		private void checkArgsForErrors(string GUID, string modName, string supportedVersion, out Version parsedSupportedVersion) {
			if (supportedVersion == null) {
				throw new ArgumentNullException(nameof(supportedVersion));
			}
			if (!Version.TryParse(supportedVersion, out parsedSupportedVersion)) {
				throw new ArgumentException($"The arg {nameof(supportedVersion)} doesnt have a valid Version format.");
			}

			checkArgsForErrors(GUID, modName, parsedSupportedVersion);
		}

		private void checkArgsForErrors(string GUID, string modName, Version supportedVersion) {
			if (GUID == null) {
				throw new ArgumentNullException(nameof(GUID));
			}
			if (modName == null) {
				throw new ArgumentNullException(nameof(modName));
			}
			if (supportedVersion == null) {
				throw new ArgumentNullException(nameof(supportedVersion));
			}
		}

		private void Init(string GUID, string modName, Version supportedVersion) {
			ModInfo = new ModInfoData(GUID, modName, supportedVersion);
			ModStatus = IsModLoaded();
		}


		protected virtual ModLoadStatus IsModLoaded() {
			bool isModLoaded = Chainloader.PluginInfos.TryGetValue(ModInfo.GUID, out PluginInfo ModPluginInfo);

			if (isModLoaded) {
				//Check loaded version against the one we support.
				ModInfo.LoadedVersion = ModPluginInfo.Metadata.Version;

				if (ModInfo.LoadedVersion != ModInfo.SupportedVersion) {
					return ModLoadStatus.DifferentVersion;
				}

				return ModLoadStatus.LoadedOk;
			}

			return ModLoadStatus.NotLoaded;
		}

	}

	/// <summary>
	/// Holds basic mod information for ExternalModHelper to work. This class is usually extended to 
	/// override, at least, the GUID as a const value, since attributes cant use strings assigned at runtime.
	/// </summary>
	public class ModInfoData {
		internal ModInfoData(string GUID, string modName, Version SupportedVersion) {
			this.GUID = GUID;
			this.Name = modName; 
			this.SupportedVersion = SupportedVersion;
		}

		public ModInfoData(Version SupportedVersion) {
			this.SupportedVersion = SupportedVersion;
		}

		public string GUID { get; internal set; }
		public string Name { get; internal set; }

		public Version SupportedVersion { get; internal set; }
		public Version LoadedVersion { get; internal set; }
	}

}
