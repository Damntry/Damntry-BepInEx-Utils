using System;
using Damntry.UtilsBepInEx.HarmonyPatching.Attributes;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.Configuration.ConfigurationManager {

	[HarmonyPatch]
	public class ConfigurationManagerPatch {

		internal static Lazy<Harmony> Harmony { get; } = new Lazy<Harmony>(() => new Harmony(typeof(ConfigurationManagerPatch).FullName));

		internal static object ConfigMngInstance { get; private set; }


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
