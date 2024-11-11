using System;
using BepInEx.Configuration;
using Damntry.UtilsBepInEx.ConfigurationManager.SettingAttributes;
using Damntry.UtilsBepInEx.ConfigurationManager;
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

		/// <summary>
		/// TODO 0 - Possible re-architecture of automatic patch class instancing.
		/// First an explanation of how the current system came to be.
		///
		/// Since BepInEx patch methods must be static, but by design my inherited methods from the base classes 
		/// are not, at the start I decided that patch classes would have their own public static property that holds
		/// an instance of themselves.
		/// To not have to implement a Singleton manually on each patch class, I have this semi Singleton pattern so
		/// the derived class can access its instance, and through it, all its base class logic. 
		/// 
		/// The problem of doing this is that the derived class needs to pass its own Type to the base class 
		/// through T, otherwise there is no way of knowing the Type to be instantated from a static method.
		/// This above specifically caused the most amount of trouble on many steps of the auto patcher cycle, and 
		/// though it has been manageable, in retrospect I should have done it in any of the usual ways, like having 
		/// a patch container class where I register all these classes, and you access this container to get a 
		/// preregistered instance.
		/// But I kind of like this small monster and it might not be worth changing it now for this project. We ll see.
		/// </summary>


		/// <summary>
		/// This constructor must NEVER be used. Use Instance() instead.
		/// Needed protected to allow inheritance.
		/// 
		/// </summary>
		protected AutoPatchedInstanceBase() { }

		internal readonly Lazy<HarmonyInstancePatcher<T>> harmonyPatchInstance = new Lazy<HarmonyInstancePatcher<T>>(() => new HarmonyInstancePatcher<T>());


		public abstract bool IsAutoPatchEnabled { get; }

		public abstract bool IsRollbackOnAutoPatchFail { get; }

		public abstract bool IsPatchActive { get; protected set; }

		public abstract string ErrorMessageOnAutoPatchFail { get; protected set; }


		public abstract event Action<bool> OnPatchFinished;


		public abstract void PatchInstance();
		public abstract void UnpatchInstance();

		public abstract void RaiseEventOnAutoPatchFinish(AutoPatchResult autoPatchResult);


		/// <summary>
		/// Makes a setting dependent on a patch instance being active. By default, settings using this functionality are hidden until 
		/// this Instance patching attempt is completed, the setting is shown only in ConfigurationManager if the patch was successful and currently active.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="configManagerControl"></param>
		/// <param name="configEntry">The ConfigEntry<typeparamref name="T"/> settings that depends on the patchInstance.</param>
		public void SetSettingPatchDependence<U>(ConfigManagerController configManagerControl, ConfigEntry<U> configEntry) {
			Instance.OnPatchFinished += (IsPatchActive) => {
				configManagerControl.SetConfigAttribute(configEntry, ConfigurationManagerAttributes.ConfigAttributes.Browsable, IsPatchActive);
			};
		}

	}
}
