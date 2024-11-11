using BepInEx.Configuration;
using Damntry.UtilsBepInEx.ConfigurationManager;
using Damntry.UtilsBepInEx.ConfigurationManager.SettingAttributes;

namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces {

	/// <summary>
	/// Interface to avoid the ConfigurationManager namespace (ConfigManagerController
	/// reference) being coupled to the AutoPatching namespace (ISelfPatch reference).
	/// AutoPatching will still be coupled and use the ConfigurationManager namespace.
	/// 
	/// </summary>
	public interface IConfigPatchDependence {

		void SetSettingPatchDependence<T>(ConfigManagerController configManagerControl, ConfigEntry<T> configEntry);

	}
}
