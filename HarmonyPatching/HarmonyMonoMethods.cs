using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;

namespace Damntry.UtilsBepInEx.HarmonyPatching {
	public class HarmonyMonoMethods {

		/// <summary>
		/// Gets a Mono.Cecil MethodDefinition from a MethodInfo.
		/// </summary>
		public static MethodDefinition GetMethodDefinition(MethodInfo methodInfo) {
			MethodDefinition methodDef = MethodBaseToMethodDefinition(methodInfo);

			if (methodDef == null) {
				//Try second way
				string dllPath = AssemblyUtils.GetAssemblyDllFilePath(methodInfo.DeclaringType);
				//TODO Global 5 - I should be caching this per dllPath
				var assemblyDef = AssemblyDefinition.ReadAssembly(dllPath);
				try {
					methodDef = assemblyDef.MainModule
						.GetType(methodInfo.DeclaringType.FullName)
						.FindMethod(methodInfo.GetID(), false);
				} catch (Exception ex) {
					//TODO Global 8 - There is a case where GetType can return null. Its either when targeting a
					//	method in a nested class, or a class that returns an IEnumerator and yields.
					//	It matters little since its relatively specific and MethodBaseToMethodDefinition above
					//	has, so far, taken care of everything.
					TimeLogger.Logger.LogTimeDebug(TimeLogger.FormatException(ex, "Error while trying to convert " +
						"MethodInfo to MethodDefinition. You can safely ignore this error if you are not the dev."),
						LogCategories.MethodChk);
				}
			}

			return methodDef;
		}

		public static MethodDefinition MethodBaseToMethodDefinition(MethodBase method) {
			var module = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(method.DeclaringType.Module.FullyQualifiedName)));
			var declaring_type = (TypeDefinition)module.LookupToken(method.DeclaringType.MetadataToken);

			return (MethodDefinition)declaring_type.Module.LookupToken(method.MetadataToken);
		}

		/// <summary>
		/// Gets all static, <see cref="HarmonyAttribute"/> annotated methods from the assembly of the type passed by
		/// parameter, and obtains the <see cref="MethodInfo"/> of the method that the annotations are targeting.
		/// Ignores any attributes not inheriting from HarmonyPatch.
		/// </summary>
		/// <param name="assemblyType"></param>
		/// <param name="skipNonExecutablePatches">If it should skip classes where there is a Prepare
		/// attribute/method that returns false, and thus, would not patch.</param>
		public static IEnumerable<(MethodInfo methodInfo, HarmonyMethod harmonyMethod)> 
				GetAllPatchMethodTargets(Type assemblyType, bool skipNonExecutablePatches) {

			return Assembly.GetAssembly(assemblyType).GetTypes()
				.Where(type => skipNonExecutablePatches ? IsPatchExecutable(type) : true)
				.SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(mInfo => IsHarmonyAttribute(mInfo))
					.Select(mInfo => {
						MethodInfo mInfoTarget = GetTargetMethodFromHarmonyPatchMethod(
							type, mInfo, out HarmonyMethod harmonyMethod);
						return (mInfoTarget, harmonyMethod);
				}));
		}

		/// <summary>
		/// Finds whether there is a Prepare method in the class, and if it returns
		/// false so the patch wouldnt be executed.
		/// </summary>
		private static bool IsPatchExecutable(Type methodClassType) {
			//Search if there is a method with [HarmonyPrepare] or named "Prepare", that returns bool
			MethodInfo methodInfo = methodClassType.GetMethods(AccessTools.all)
				.Where(m => m.ReturnType == typeof(bool))
				.FirstOrDefault(m => m.Name == "Prepare" ||
					m.GetCustomAttributes(true)
						.Any(attr => attr.GetType().FullName == typeof(HarmonyPrepare).FullName)
			);

			if (methodInfo != null) {
				//There is a Prepare method. Invoke it with default parameters.
				var actualParameters = AccessTools.ActualParameters(methodInfo, []);
				return Utils.Reflection.ReflectionHelper.CallMethod<bool>(null, methodInfo, actualParameters);
			}

			//No prepare method.
			return true;
		}

		public static bool IsHarmonyAttribute(MethodInfo methodInfo) {
			//Hacky way to handle the case of methods patched with HarmonyPatchStringTypes,
			//	used when a type is not loaded at compile time so it is referenced using strings.
			//	Methods using this attribute throw an error while patching, by design, when the
			//	type doesnt exist, and must manually handle not patching by, for example, disabling
			//	its autopatching, or having a Prepare return false among other ways.
			//	Since we cant cover every custom case, we simply try to access the custom
			//	attributes of the method, and if the result is a TypeNotFoundInAssemblyException,
			//	we skip the method.
			IEnumerable<Attribute> attribs = null;
			try {
				attribs = methodInfo.GetCustomAttributes();
			} catch (TypeNotFoundInAssemblyException) {
				return false;
			}

			return attribs.Any(attr => attr is HarmonyAttribute);
		}

		/// <summary>
		/// Gets the original method that the patch method is targeting for patching.
		/// </summary>
		/// <param name="methodClassType">The class where the method is located.</param>
		/// <param name="patchMethodInfo">The MethodInfo of the patch method.</param>
		/// <returns>The MethodInfo of the method that the patch is targetting.</returns>
		public static MethodInfo GetTargetMethodFromHarmonyPatchMethod(Type methodClassType, 
				MethodInfo patchMethodInfo, out HarmonyMethod harmonyMethod) {

			//Get method info from the HarmonyAttributes of the method
			var harmonyMethods = patchMethodInfo.GetCustomAttributes(true)
				.Where(attr => attr is HarmonyAttribute)
				.Select(attr => ((HarmonyAttribute)attr).info);

			//Merge all annotations into a single complete one.
			harmonyMethod = HarmonyMethod.Merge(harmonyMethods.ToList());

			if (harmonyMethod.method == null && harmonyMethod.declaringType == null) {
				//Support that the containing class of the method can have the annotation
				//	for the target type, instead of being in the method itself.
				HarmonyMethod harmonyClassAttr = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(methodClassType));
				harmonyMethod = harmonyClassAttr.Merge(harmonyMethod);
			}
			harmonyMethod.methodType ??= MethodType.Normal;
			
			//Access the internal method that handles getting a methodInfo, taking into account all harmony attributes.
			MethodInfo mInfoInternal = AccessTools.Method("HarmonyLib.PatchTools:GetOriginalMethod", [typeof(HarmonyMethod)]);
			if (mInfoInternal != null) {
				return (MethodInfo)mInfoInternal.Invoke(null, [harmonyMethod]);
			} else {
				TimeLogger.Logger.LogTimeWarning("Reflection access to \"HarmonyLib.PatchTools:GetOriginalMethod\" returned " +
					"null. Using backup method.", LogCategories.MethodChk);
				return AccessTools.Method(harmonyMethod.declaringType, harmonyMethod.methodName, harmonyMethod.argumentTypes);
			}

		}

	}
}
