using System;
using System.Collections.Generic;
using System.Reflection;
using Damntry.Utils.ExtensionMethods;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.HarmonyPatching {

	public class HarmonyInstancePatcher {


		private readonly Type harmonyInstanceType;

		private readonly Lazy<Harmony> harmonyPatch;


		private enum PatchRecursiveAction {
			StopAll,                //Completely stop this recursive branch.
			SkipAndContinueNested,  //Dont patch, but keep going recursively.
			PatchAndContinueNested  //Do surface level patches that are not in any subclass, and keep going recursively.
		}


		public HarmonyInstancePatcher(Type harmonyInstanceType) {
			this.harmonyInstanceType = harmonyInstanceType;

			harmonyPatch = new Lazy<Harmony>(() => new Harmony(GetHarmonyInstanceId()));
		}


		internal string GetHarmonyInstanceId() {
			return harmonyInstanceType.FullName;
		}

		public List<MethodInfo> PatchInstance() {
			return StartRecursivePatching(harmonyPatch.Value);
		}

		public void UnpatchInstance() {
			harmonyPatch.Value.UnpatchSelf();
		}

		private List<MethodInfo> StartRecursivePatching(Harmony harmonyPatch) {
			List<MethodInfo> listPatchedMethods = new();

			bool continueRecursive = PatchClass(harmonyInstanceType, harmonyPatch, listPatchedMethods);

			if (continueRecursive) {
				//Patch recursively through nested classes
				PatchNestedClassesRecursive(new List<Type>(), harmonyInstanceType, harmonyPatch, listPatchedMethods);
			}

			return listPatchedMethods;
		}

		private void PatchNestedClassesRecursive(List<Type> classList, Type classType, Harmony harmony, List<MethodInfo> listPatchedMethods) {
			Type[] nestedTypes = classType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

			foreach (Type nestedType in nestedTypes) {
				if (nestedType.IsClass) {

					bool continueRecursive = PatchClass(nestedType, harmony, listPatchedMethods);

					if (!continueRecursive) {
						continue;
					}

					PatchNestedClassesRecursive(classList, nestedType, harmony, listPatchedMethods);
				}
			}
		}

		private bool PatchClass(Type classType, Harmony harmonyPatch, List<MethodInfo> listPatchedMethods) {
			PatchRecursiveAction patchAction = CheckAttributesForAllowedAction(classType);

			if (patchAction == PatchRecursiveAction.StopAll) {
				return false;
			}

			if (patchAction == PatchRecursiveAction.PatchAndContinueNested) {
				var patchProcessor = harmonyPatch.CreateClassProcessor(classType, allowUnannotatedType: true);

				listPatchedMethods.AddRange(
					patchProcessor.Patch()
				);
			}

			return true;
		}

		private PatchRecursiveAction CheckAttributesForAllowedAction(Type classType) {
			if (classType.HasCustomAttribute<AutoPatchIgnoreClassAndNested>()) {
				//Completely stop this recursive branch.
				return PatchRecursiveAction.StopAll;
			}

			if (classType.HasCustomAttribute<AutoPatchIgnoreClass>()) {
				//Dont patch, but keep going recursively.
				return PatchRecursiveAction.SkipAndContinueNested;
			}

			//Do surface level patches that are not in any subclass, and keep going recursively.
			return PatchRecursiveAction.PatchAndContinueNested;
		}

		public List<MethodInfo> PatchClassByType(Type classType) {
			ThrowIfNotOwnInstanceNestedClass(classType);

			return harmonyPatch.Value.CreateClassProcessor(classType).Patch();
		}

		/// <summary>Unpatches a method.</summary>
		/// <param name="originalClassType">The type of the original class that is going to be unpatched. 
		/// * Not the class where the patch is, but the target *</param>
		/// <param name="originalMethodName">The name of the method in the original class that is going to be unpatched. </param>
		public void UnpatchMethod(Type originalClassType, string originalMethodName) {
			if (originalMethodName == null) {
				throw new ArgumentNullException($"{nameof(originalMethodName)} cannot be null.");
			}

			MethodInfo method = AccessTools.Method(originalClassType, originalMethodName);

			if (method == null) {
				throw new InvalidOperationException($"The method \"{originalMethodName}\" couldnt be found in the type {originalClassType.FullName}.");
			}
			harmonyPatch.Value.Unpatch(method, HarmonyPatchType.All, harmonyPatch.Value.Id);
		}

		private void ThrowIfNotOwnInstanceNestedClass(Type classType) {
			if (classType == null) {
				throw new ArgumentNullException("ClassType argument cant be null.");
			}
			if (classType == harmonyInstanceType) {
				throw new InvalidOperationException($"Use the method PatchInstance/UnpatchInstance() instead.");
			}
			if (GetTopClassOfNested(classType) != harmonyInstanceType) {
				throw new InvalidOperationException($"Class must be a nested class of {harmonyInstanceType.FullName}. " +
					$"If you want to do this action on another Instance, use that instance methods instead.");
			}
		}

		private Type GetTopClassOfNested(Type classType) {
			while (classType.IsNested) {
				classType = classType.DeclaringType;
			}

			return classType;
		}

	}
}
