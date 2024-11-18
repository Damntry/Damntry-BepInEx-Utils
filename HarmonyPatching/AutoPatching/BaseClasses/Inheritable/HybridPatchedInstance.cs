using System;
using System.Collections.Generic;
using System.Reflection;
using static Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.AutoPatcher;

namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable {



	/// <summary>
	/// Provides extended functionality for class patching.
	/// The derived class acts itself as an "instance", in the sense of it being 
	/// an independent object in itself, that can also be accessed from the outside
	/// as an hybrid Singleton.
	/// It is meant to be used to encapsulate multiple patches in a single class that, as a whole,
	/// works to implement a specific functionality.
	/// There shouldnt be much functionality in the class itself other than patches. Otherwise, it is 
	/// recommended to move them to a static helper class.
	/// Can be used with the <see cref="AutoPatcher"/> functionality to do the patching of all
	/// objects inheriting this abstract class, much like Harmony.PatchAll() does.
	/// Works with base harmony patching, except for patching nested classes. To patch nested classes, use
	/// the method <see cref="HarmonyInstancePatcher.StartRecursivePatching"/> or refer on how to use <see cref="AutoPatcher"/>.
	/// Generates an unique harmonyId so method patches can be reverted in the derived class instance.
	/// </summary>
	/// <typeparam name="T">
	/// The <see cref="Type"/> of the deriving class.
	/// Example: <code>public class DoSomethingPatch : ExtendedInstancedPatch<DoSomethingPatch></code>
	/// </typeparam>
	public abstract class HybridPatchedInstance : AutoPatchedInstanceBase {


		//RaiseEventOnAutoPatchFinish will rarely be ever needed in a HybridPatchedInstance, so I
		//	override it here and keep unused to make it less annoying for derived classes, while
		//	still letting them make use of it for any specific cases, like for example, when it
		//	relies mainly on multipatching but needs some secondary manual patching done.
		public override void RaiseEventOnAutoPatchFinish(AutoPatchResult autoPatchResult) { }


		public override List<MethodInfo> PatchInstance() {
			return harmonyPatchInstance.Value.PatchInstance();
		}

		public override void UnpatchInstance() {
			harmonyPatchInstance.Value.UnpatchInstance();
		}

		public List<MethodInfo> PatchClassByType(Type classType) {
			return harmonyPatchInstance.Value.PatchClassByType(classType);
		}

		public void UnpatchMethod(Type classType, string methodName) {
			harmonyPatchInstance.Value.UnpatchMethod(classType, methodName);
		}

	}
}
