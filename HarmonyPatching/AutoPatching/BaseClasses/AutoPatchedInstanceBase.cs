using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager.SettingAttributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces;
using static Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.AutoPatcher;


namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses {

	/// <summary>
	/// Provides base functionality for class patching.
	/// The derived class acts itself as an "instance", in the sense of it being 
	/// an independent object in itself, that can also be accessed from the outside
	/// as an hybrid Singleton (hybrid due the constructor being accessible to allow inheritance).
	/// It is meant to be used to encapsulate multiple patches in a single class that, as a whole,
	/// works to implement a specific functionality.
	/// There shouldnt be much functionality in the class itself other than patches. Otherwise, it is 
	/// recommended to move them to a static helper class.
	/// In itself, the only functionality this class provides is to add a patch dependency to a setting, 
	/// so when, and if, the patch is activated, the setting is enabled.
	/// Other than that, it delegates all IAutoPatchSupport interface implementation to the derived class.
	/// </summary>
	/// <typeparam name="T">
	/// The <see cref="Type"/> of the deriving class.
	/// Example: <code>public class DoSomethingPatch : ExtendedInstancedPatch<DoSomethingPatch></code>
	/// </typeparam>
	public abstract class AutoPatchedInstanceBase : IAutoPatchSupport, IConfigPatchDependence {

		// Short explanation of the old instance system, so I dont forget why I changed:
		//
		// Since BepInEx patch methods must be static, but by design my inherited methods from the base classes 
		// are not, at the start I decided to go for a simple system of patch classes that have their own public 
		// static property, that holds an instance of themselves.
		// To not have to implement a Singleton manually on each patch class, I made a semi Singleton pattern in
		// this class so the derived class can access its instance, and through it, all its base class logic.
		// 
		// But this needed that the derived class passes its own Type to the base class through T while inheriting,
		// otherwise there is no way right now of using generics to return the exact type at compile time.
		// And probably never: https://github.com/dotnet/csharplang/discussions/6452
		// 
		// The effect of having a hierarchy chain of abstract classes with a generic, which I needed
		// to reference in generic bound and unbound ways around the architecture, caused the most amount
		// of trouble on many steps of the auto patcher cycle, and though it was manageable, I got fed up and
		// ended up switching to the current patch container class registry.
		//
		// I kind of liked that old monster. You will not be missed.


		internal readonly Lazy<HarmonyInstancePatcher> harmonyPatchInstance;

		internal AutoPatchedInstanceBase() {
			harmonyPatchInstance = new Lazy<HarmonyInstancePatcher>(() => new HarmonyInstancePatcher(GetType()));
		}

		public abstract bool IsAutoPatchEnabled { get; }

		public abstract bool IsRollbackOnAutoPatchFail { get; }

		public abstract bool IsPatchActive { get; protected set; }

		public abstract string ErrorMessageOnAutoPatchFail { get; protected set; }


		public abstract event Action<bool> OnPatchFinished;


		public List<MethodInfo> PatchInstance() {
			return harmonyPatchInstance.Value.PatchInstance();
		}

		public void UnpatchInstance() {
			harmonyPatchInstance.Value.UnpatchInstance();
		}

		public int GetPatchedCount() {
			return harmonyPatchInstance.Value.GetPatchedCount();
		}
		


		public abstract void RaiseEventOnAutoPatchFinish(AutoPatchResult autoPatchResult);


		/// <summary>
		/// Makes a setting dependent on a patch instance being active. By default, settings using this functionality are hidden until 
		/// this Instance patching attempt is completed, the setting is shown only in ConfigurationManager if the patch was successful and currently active.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="configEntry">The ConfigEntry<T> setting that depends on the patchInstance.</param>
		public void SetSettingPatchDependence<U>(ConfigEntry<U> configEntry) {
			OnPatchFinished += (IsPatchActive) => {
				configEntry.SetConfigAttribute(ConfigurationManagerAttributes.ConfigAttributes.Browsable, IsPatchActive);
			};
		}

	}

}
