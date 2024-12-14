using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses;


namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching {


	/// <summary>
	/// Provides functionality to start and manage the auto patching process, with exception control and logging.
	/// </summary>
	public class AutoPatcher {


		public enum AutoPatchResult {
			none,
			error,
			disabled,
			success
		}

		//	TODO 6 - If I create another mod that needs Damntry Utils too, and their dll are included, Im going to have duplicated dlls in the assembly.
		//		Possible solutions:
		//		- Bundle the Utils as a external required mod.
		//			This is the usual way to handle it, but in my case the Utils are still too bare and subject to refactoring to make it into
		//			its own stable thing.
		//		- Modify namespace to add version (For example: Damntry.UtilsBepInEx.0_X.Patching.AutoPatching).
		//			I would need to keep copies of versions directly referenced by any project, in case newer ones are not compatible
		//			(I should probably be doing this anyway). 


		//	TODO Global 6 - Related to the above of duplicated dlls, my assembly logic is all over the place since I dont even know what Im going to do.
		//		In some places I take the calling assembly, in others who knows... Then the AutoPatchContainer class is an static that could hold
		//		all autopatches across mods, but then there would be no separation whatsoever and I wouldnt be able to do things like unpatching a
		//		whole mod in an easy way.
		//		Its a mess, but at least it wont matter until I create a 2º mod for the same game, or someone copies this system (the madman).

		public static bool RegisterAllAutoPatchContainers() {
			List<Type> autoPatchTypes = GetAutoPatchClasses(Assembly.GetCallingAssembly()).ToList();
			if (autoPatchTypes.Count == 0) {
				return false;
			}

			foreach (var autoPatchType in autoPatchTypes) {
				AutoPatchContainer.RegisterPatchClass(autoPatchType);
			}

			return true;
		}


		public static bool StartAutoPatcher() {
			int patchErrorCount = 0;
			int patchDisabledCount = 0;

			AutoPatchResult result;
			
			//Patch all them patches
			foreach (KeyValuePair<Type, AutoPatchedInstanceBase> autoPatchInfo in AutoPatchContainer.GetRegisteredAutoPatches()) {
				result = Patch(autoPatchInfo.Key, autoPatchInfo.Value);

				if (result == AutoPatchResult.disabled) {
					patchDisabledCount++;
				} else if (result == AutoPatchResult.error) {
					patchErrorCount++;
				}
			}

			if (patchErrorCount == 0) {
				TimeLogger.Logger.LogTimeInfo($"All Auto-Patches applied successfully.", TimeLogger.LogCategories.Loading);

				return true;
			} else {
				TimeLogger.Logger.LogTimeFatal($"Oh oh, {patchErrorCount} out of {AutoPatchContainer.GetRegisteredAutoPatches().Count() - patchDisabledCount} patches failed. " +
					$"Check above for errors.", TimeLogger.LogCategories.Loading);

				return false;
			}
		}

		private static AutoPatchResult Patch(Type autoPatchType, AutoPatchedInstanceBase autoPatchInstance) {
			AutoPatchResult result = AutoPatchResult.none;

			try {
				if (autoPatchInstance == null) {
					throw new InvalidOperationException($"Auto patch received a null instance from the registered type {autoPatchType.FullName}.");
				}

				if (autoPatchInstance.IsAutoPatchEnabled) {
					var listMethodsPatched = autoPatchInstance.PatchInstance();

					result = AutoPatchResult.success;
				} else {
					result = AutoPatchResult.disabled;
				}

			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeExceptionWithMessage($"Error auto patching class {autoPatchType.FullName}.", ex, TimeLogger.LogCategories.Loading);
				//Show custom message in game.
				if (autoPatchInstance != null) {
					TimeLogger.Logger.LogTimeErrorShowInGame(autoPatchInstance.ErrorMessageOnAutoPatchFail, TimeLogger.LogCategories.AutoPatch);

					if (autoPatchInstance.IsRollbackOnAutoPatchFail == true) {
						autoPatchInstance.UnpatchInstance();
					}
				}
				
				result = AutoPatchResult.error;
			} finally {
				autoPatchInstance?.RaiseEventOnAutoPatchFinish(result);
			}

			return result;
		}

		public static bool IsPatchActiveFromResult(AutoPatchResult autoPatchResult) {
			return autoPatchResult switch {
				AutoPatchResult.success => true,
				AutoPatchResult.disabled or AutoPatchResult.error => false,
				_ => throw new NotImplementedException($"The switch case {autoPatchResult} is not implemented or should not have happened."),
			};
		}

		private static IEnumerable<Type> GetAutoPatchClasses(Assembly assembly) {
			return assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AutoPatchedInstanceBase)));

			/*
			List<AutoPatchedInstanceBase> autoPatchedInstanceBases = new List<Type>();
			foreach (var item in assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AutoPatchedInstanceBase)))) {
				autoPatchedInstanceBases.Cast<AutoPatchedInstanceBase>().(item);
			}

			TimeLogger.Logger.LogTimeExceptionWithMessage("", ex, TimeLogger.LogCategories.AutoPatch);
			return autoPatchedInstanceBases;

			return (IEnumerable<AutoPatchedInstanceBase>)assembly.GetTypes().Where
				(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(AutoPatchedInstanceBase)));
			*/
		}

		public static string[] GetHarmonyInstanceIdsForAttribute(Type[] autoPatchTypes) {
			return autoPatchTypes.Select((Type autoPatchType) => AutoPatchContainer.GetAbstractInstance(autoPatchType).harmonyPatchInstance.Value.GetHarmonyInstanceId()).ToArray();
		}

	}

}
