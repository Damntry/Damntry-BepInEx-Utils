using System;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.Configuration.ConfigurationManager.Patch {

	[HarmonyPatch]
	public class ConfigurationManagerPatch {

		private static Lazy<Harmony> Harmony { get; } = 
			new Lazy<Harmony>(() => new Harmony(typeof(ConfigurationManagerPatch).FullName));

		internal static object ConfigMngInstance { get; private set; }

		internal static void PatchSelf() {
			try {
				Harmony.Value.PatchAll(typeof(ConfigurationManagerPatch));
			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeExceptionWithMessage($"Error while trying to apply " +
					$"patch in type {nameof(ConfigurationManagerPatch)}", ex, LogCategories.Config);
			}
		}

		[HarmonyPatchStringTypes(ConfigManagerController.ConfigMngFullTypeName, "Update")]
		[HarmonyPostfix]
		internal static void GetConfigManagerInstancePatch(object __instance) {
			if (__instance != null) {
				ConfigMngInstance = __instance;
				Harmony.Value.UnpatchSelf();
			}
		}

	}

}
