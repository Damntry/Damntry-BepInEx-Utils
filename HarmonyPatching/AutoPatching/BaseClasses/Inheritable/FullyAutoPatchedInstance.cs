using System;
using System.Collections.Generic;
using System.Reflection;
using static Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.AutoPatcher;


namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses.Inheritable {

	// TODO 7 - I completely forgot in which cases a Harmony.PatchAll() call doesnt work with nested patches.
	// Because I think remembering that some cases DO work? I ended up mapping how patching and attributes work from
	// Harmony source code to figure it all out, but my brain deleted pretty much all of it.
	// I think it was when a class had multiple subclasses? Something like that maybe. I need to check again.
	// Brain started working for a nanosecond and I think Harmony has zero nested patching, and I was remembering
	// wrong, maybe thinking of a transpiler anonymous method?


	/// <summary>
	/// Provides extended functionality for class patching through the auto patching functionality (<see cref="AutoPatcher"/>.).
	/// Simplifies patching logic by implementing the auto patching methods and automating some of the
	/// state logic, with the disadvantage of only working through auto patching with no possibility of manual patching.
	/// If manual patching is also necessary, inherit instead from the class <see cref="ExtendedInstancedFullAutoPatch"/>.
	/// The derived class acts itself as an "instance", in the sense of it being  an independent 
	/// object in itself, that can also be accessed from the outside as an hybrid Singleton.
	/// It is meant to be used to encapsulate multiple Harmony patches in a single class that, as a 
	/// whole, works to implement a specific functionality.
	/// There shouldnt be much functionality in the class itself other than patches. Otherwise, it is 
	/// recommended to move them to a static helper class.
	/// Any already applied patches of this instance, will automatically revert when a subsequent patch errors out,
	/// but this can be changed by overriding <see cref="IsRollbackOnAutoPatchFail"/>.
	/// </summary>
	/// <typeparam name="T">
	/// The <see cref="Type"/> of the deriving class.
	/// Example: <code>public class DoSomethingPatch : ExtendedInstancedFullAutoPatch<DoSomethingPatch></code>
	/// </typeparam>
	public abstract class FullyAutoPatchedInstance : AutoPatchedInstanceBase {


		public override bool IsRollbackOnAutoPatchFail => true;

		public override bool IsPatchActive { get; protected set; }

		public void AutoPatchResultEvent(AutoPatchResult autoPatchResult) => IsPatchActive = IsPatchActiveFromResult(autoPatchResult);


		public override event Action<bool> OnPatchFinished;


		public override List<MethodInfo> PatchInstance() {
			return harmonyPatchInstance.Value.PatchInstance();
		}

		public override void UnpatchInstance() {
			harmonyPatchInstance.Value.UnpatchInstance();
		}


		public override void RaiseEventOnAutoPatchFinish(AutoPatchResult autoPatchResult) {
			AutoPatchResultEvent(autoPatchResult);

			//Raise event for all subscribers
			if (OnPatchFinished != null) {
				OnPatchFinished(IsPatchActive);
			}
		}

	}
}
