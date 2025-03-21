using System;
using System.Collections.Generic;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses;

namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching {


	public static class Container<T> where T : AutoPatchedInstanceBase {
		public static T Instance {
			get {
				return AutoPatchContainer.GetInstance<T>();
			}
		}
	}

	internal static class AutoPatchContainer {


		private static Dictionary<Type, AutoPatchedInstanceBase> registeredInstances = new();


		internal static T GetInstance<T>() where T : AutoPatchedInstanceBase {
			ThrowIfTypeInvalidOrNotRegistered<T>();

			return (T)registeredInstances[typeof(T)];
		}

		internal static AutoPatchedInstanceBase GetAbstractInstance(Type patchType) {
			ThrowIfTypeInvalidOrNotRegistered(patchType);

			return registeredInstances[patchType];
		}

		internal static IReadOnlyDictionary<Type, AutoPatchedInstanceBase> GetRegisteredAutoPatches() {
			return registeredInstances;
		}


		internal static void RegisterPatchClass(Type autoPatchType) {
			ThrowIfTypeInvalidOrAlreadyRegistered(autoPatchType);

			var instance = Activator.CreateInstance(autoPatchType);

			registeredInstances.Add(autoPatchType, (AutoPatchedInstanceBase)instance);
		}

		internal static void UnregisterPatchClass(Type autoPatchClass) {
			ThrowIfTypeInvalidOrNotRegistered(autoPatchClass);

			//TODO Global 4 - Implement Dispose on AutoPatchedInstanceBase, and call it here before removal.
			//		It has to unpatch the instance, null statics and so on.
			registeredInstances.Remove(autoPatchClass);
		}



		private static void ThrowIfTypeInvalidOrAlreadyRegistered<T>() {
			ThrowIfTypeInvalidOrAlreadyRegistered(typeof(T));
		}

		private static void ThrowIfTypeInvalidOrAlreadyRegistered(Type patchClassType) {
			if (patchClassType.IsAbstract || !patchClassType.IsSubclassOf(typeof(AutoPatchedInstanceBase))) {
				throw new InvalidOperationException($"The type {patchClassType.FullName} must be a non abstract subclass of AutoPatchedInstanceBase.");
			}
			if (registeredInstances.ContainsKey(patchClassType)) {
				throw new InvalidOperationException($"The type {patchClassType.FullName} has already been registered.");
			}
		}

		private static void ThrowIfTypeInvalidOrNotRegistered<T>() {
			ThrowIfTypeInvalidOrNotRegistered(typeof(T));
		}

		private static void ThrowIfTypeInvalidOrNotRegistered(Type patchClassType) {
			if (patchClassType.IsAbstract || !patchClassType.IsSubclassOf(typeof(AutoPatchedInstanceBase))) {
				throw new InvalidOperationException($"The type {patchClassType.FullName} must be a non abstract subclass of AutoPatchedInstanceBase.");
			}
			if (!registeredInstances.ContainsKey(patchClassType)) {
				throw new InvalidOperationException($"The type {patchClassType.FullName} is not registered.");
			}
		}

	}


}
