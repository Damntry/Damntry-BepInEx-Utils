using System;
using System.Collections.Generic;
using System.Reflection;
using Damntry.Utils.ExtensionMethods;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.HarmonyPatching
{

    public class HarmonyInstancePatcher<T> where T : IAutoPatchSupport {


		private readonly Type thisInstanceType = typeof(T);

		private readonly Lazy<Harmony> harmonyPatch;


		public HarmonyInstancePatcher() {
			harmonyPatch = new Lazy<Harmony>(() => new Harmony(GetHarmonyInstanceId()));
		}


		internal string GetHarmonyInstanceId() {
			return thisInstanceType.FullName;
		}

		public void PatchInstance() {
			StartRecursivePatching(harmonyPatch.Value);
		}

		public void UnpatchInstance() {
			harmonyPatch.Value.UnpatchSelf();
		}

		private void StartRecursivePatching(Harmony harmonyPatch) {
			bool keepGoing = DoPatchIfAttributeAllows(harmonyPatch, thisInstanceType);

			if (!keepGoing) {
				return;
			}

			//Patch recursively through nested classes
			PatchNestedClassesRecursive(new List<Type>(), thisInstanceType, harmonyPatch);
		}

		private List<Type> PatchNestedClassesRecursive(List<Type> classList, Type classType, Harmony harmony) {
			Type[] nestedTypes = classType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

			foreach (Type nestedType in nestedTypes) {
				//Skip classes with the attribute [AutoPatchIgnoreClass]
				if (nestedType.IsClass) {

					bool keepGoing = DoPatchIfAttributeAllows(harmony, nestedType);

					if (!keepGoing) {
						continue;
					}

					PatchNestedClassesRecursive(classList, nestedType, harmony);
				}
			}

			return classList;
		}

		private bool DoPatchIfAttributeAllows(Harmony harmony, Type classType) {
			if (classType.HasCustomAttribute<AutoPatchIgnoreClassAndNested>()) {
				return false;
			}

			if (!classType.HasCustomAttribute<AutoPatchIgnoreClass>()) {
				//Do surface level patches that are not in any subclass
				harmony.PatchAll(classType);
			}

			return true;
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
			if (classType == thisInstanceType) {
				throw new InvalidOperationException($"Use the method PatchInstance/UnpatchInstance() instead.");
			}
			if (GetTopClassOfNested(classType) != thisInstanceType) {
				throw new InvalidOperationException($"Class must be a nested class of {thisInstanceType.FullName}. " +
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
