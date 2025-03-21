using System;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes {

	/// <summary>
	/// Attribute to set this class or method to be patched before another auto patch instance.
	/// Regardless of order in which auto patches are executed, Harmony will get in charge of
	/// queuing patches to ensure order of execution based on these attributes.
	/// Works with base harmony patching.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class HarmonyBeforeInstance : HarmonyPatch {

		private HarmonyBeforeInstance() { }

		public HarmonyBeforeInstance(params Type[] beforeInstances) {
			info.before = AutoPatcher.GetHarmonyInstanceIdsForAttribute(beforeInstances);
		}

	}

	/// <summary>
	/// Attribute to set this class or method to be patched after another auto patch instance.
	/// Regardless of order in which auto patches are executed, Harmony will get in charge of
	/// queuing patches to ensure order of execution based on these attributes.
	/// Works with base harmony patching.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class HarmonyAfterInstance : HarmonyPatch {

		private HarmonyAfterInstance() { }

		public HarmonyAfterInstance(params Type[] afterInstances) {
			info.after = AutoPatcher.GetHarmonyInstanceIdsForAttribute(afterInstances);
		}

	}

}
