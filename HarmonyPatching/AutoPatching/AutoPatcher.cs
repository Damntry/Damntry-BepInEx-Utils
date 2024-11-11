using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.BaseClasses;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces;
using Damntry.UtilsBepInEx.Logging;
using HarmonyLib;


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


		public static bool StartAutoPatcher() {
			return StartAutoPatcher(Assembly.GetCallingAssembly());
		}

		public static bool StartAutoPatcher(Type assemblyType) {
			return StartAutoPatcher(assemblyType.Assembly);
		}

		public static bool StartAutoPatcher(Assembly assembly) {
			int patchErrorCount = 0;
			int patchDisabledCount = 0;

			List<Type> autoPatchTypes = GetAutoPatchClasses(assembly).ToList();

			AutoPatchResult result;

			//Patch all them patches
			foreach (Type autoPatchType in autoPatchTypes) {
				result = Patch(autoPatchType);

				if (result == AutoPatchResult.disabled) {
					patchDisabledCount++;
				} else if (result == AutoPatchResult.error) {
					patchErrorCount++;
				}
			}

			if (patchErrorCount == 0) {
				BepInExTimeLogger.Logger.LogTimeInfo($"All Auto-Patches applied successfully.", TimeLoggerBase.LogCategories.Loading);

				return true;
			} else {
				BepInExTimeLogger.Logger.LogTimeFatal($"Oh oh, {patchErrorCount} out of {autoPatchTypes.Count - patchDisabledCount} patches failed. " +
					$"Check above for errors.", TimeLoggerBase.LogCategories.Loading);

				return false;
			}
		}

		private static AutoPatchResult Patch(Type autoPatchType) {
			IAutoPatchSupport autoPatchInstance = null;
			AutoPatchResult result = AutoPatchResult.none;

			try {			
				autoPatchInstance = (IAutoPatchSupport)AccessTools.Property(autoPatchType.BaseType, nameof(AutoPatchedInstanceBase.Instance)).GetValue(null);

				if (autoPatchInstance.IsAutoPatchEnabled) {
					autoPatchInstance.PatchInstance();
					result = AutoPatchResult.success;
				} else {
					result = AutoPatchResult.disabled;
				}

			} catch (Exception ex) {
				BepInExTimeLogger.Logger.LogTimeExceptionWithMessage($"Error auto patching class {autoPatchType.FullName}.", ex, TimeLoggerBase.LogCategories.Loading);
				//Show custom message in game.
				if (autoPatchInstance != null) {
					BepInExTimeLogger.Logger.LogTimeErrorShowInGame(autoPatchInstance.ErrorMessageOnAutoPatchFail, TimeLoggerBase.LogCategories.AutoPatch);

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
			return assembly.GetTypes().Where
				(type => type.IsClass && !type.IsAbstract && typeof(IAutoPatchSupport).IsAssignableFrom(type));
		}

	}

}
