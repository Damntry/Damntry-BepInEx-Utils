using System;
using System.Linq;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes {

	//TODO 1 - Wait a second. If the autopatcher is just going in whatever order it finds the patches,
	//		what happens with HarmonyBefore attributes? I imagine Harmony puts them in a queue or something?
	//		Check it out and update the class comment below if necessary.

	/// <summary>
	/// Attribute to set this class or method to be patched before another auto patch instance.
	/// Regardless of order in which auto patches are executed, Harmony will get in charge of
	/// queuing patches to ensure order of execution based on these attributes.
	/// Works with base harmony patching.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
	public class HarmonyBeforeInstance : HarmonyPatch {

		private HarmonyBeforeInstance() { }

		public HarmonyBeforeInstance(params AutoPatchedInstanceBase[] beforeInstances) {
			string[] beforeInstancesStr = beforeInstances.Select(inst => inst.harmonyPatchInstance.Value.GetHarmonyInstanceId()).ToArray();

			info.before = beforeInstancesStr;
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

		public HarmonyAfterInstance(params AutoPatchedInstanceBase[] afterInstances) {
			string[] afterInstancesStr = afterInstances.Select(inst => inst.harmonyPatchInstance.Value.GetHarmonyInstanceId()).ToArray();

			info.after = afterInstancesStr;
		}

	}

}
