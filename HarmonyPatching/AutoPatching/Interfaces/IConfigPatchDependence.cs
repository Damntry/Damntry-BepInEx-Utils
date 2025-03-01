using BepInEx.Configuration;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;

namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces {

	/// <summary>
	/// Interface to avoid the ConfigurationManager namespace (ConfigManagerController
	/// reference) being coupled to the AutoPatching namespace (ISelfPatch reference).
	/// AutoPatching will still be coupled and use the ConfigurationManager namespace.
	/// 
	/// </summary>
	public interface IConfigPatchDependence {

		void SetSettingPatchDependence<T>(ConfigEntry<T> configEntry);

	}
}
