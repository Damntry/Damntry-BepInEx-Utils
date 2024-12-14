using System;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.HarmonyPatching.Attributes {

	/// <summary>
	/// Attribute to patch a method by searching types with strings. Works with Harmony built-in patching.
	/// This is an alternative to the harmonyPatch that uses an assemblyQualifiedDeclaringType, which is hard to maintain.
	/// It also adds the possibility of specifying method argument types by string.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = true)]
	public class HarmonyPatchStringTypes : HarmonyAttribute {


		/// <summary>
		/// Annotation to specify a target patch method by its class type full name and method name.
		/// For when you can only access its reference at runtime.
		/// </summary>
		/// <param name="fullTypeName">
		/// The full name of the type. That is, the complete namespace and its name.
		/// For example: "System.Reflection.Assembly"
		/// Its a bit different if the type is generic. Usually you need to add an extra "`1" string 
		/// to get the generic, and once you have it, you use Type.MakeGenericType to get the typed 
		/// version you need.
		/// If you have access to the type, you can get its full name with: typeof(SomeType).FullName
		/// </param>
		/// <param name="methodName">Name of the method</param>
		public HarmonyPatchStringTypes(string fullTypeName, string methodName) {
			SetMethodInfo(fullTypeName, methodName, argumentTypes: null);
		}

		/// <summary>
		/// Annotation to specify a target patch method by its class type full name, method name, and argument types.
		/// For when you can only access its reference at runtime.
		/// </summary>
		/// <param name="fullTypeName">
		/// The full name of the type. That is, the complete namespace and its name.
		/// For example: "System.Reflection.Assembly"
		/// Its a bit different if the type is generic. Usually you need to add an extra "`1" string 
		/// to get the generic, and once you have it, you use Type.MakeGenericType to get the typed 
		/// version you need.
		/// If you have access to the type, you can get its full name with: typeof(SomeType).FullName
		/// </param>
		/// <param name="methodName">Name of the method</param>
		/// <param name="argumentTypes">Argument types.</param>
		public HarmonyPatchStringTypes(string fullTypeName, string methodName, Type[] argumentTypes) {
			SetMethodInfo(fullTypeName, methodName, argumentTypes);
		}

		/// <summary>
		/// Annotation to specify a target patch method by its class type full name, method name, and arguments full type names.
		/// For when you can only access its reference at runtime.
		/// </summary>
		/// <param name="fullTypeName">
		/// The full name of the type. That is, the complete namespace and its name.
		/// For example: "System.Reflection.Assembly"
		/// Its a bit different if the type is generic. Usually you need to add an extra "`1" string 
		/// to get the generic, and once you have it, you use Type.MakeGenericType to get the typed 
		/// version you need.
		/// If you have access to the type, you can get its full name with: typeof(SomeType).FullName
		/// </param>
		/// <param name="methodName">Name of the method</param>
		/// <param name="argumentFullTypeNames">Arguments full type names. See <paramref name="fullTypeName"/> param for details on the format.</param>
		private void SetMethodInfo(string fullTypeName, string methodName, string[] argumentFullTypeNames) {
			Type[] argumentTypes = AssemblyUtils.GetTypesFromLoadedAssemblies(true, argumentFullTypeNames);

			SetMethodInfo(fullTypeName, methodName, argumentTypes);
		}

		private void SetMethodInfo(string fullTypeName, string methodName, Type[] argumentTypes) {
			Type declaringType = AssemblyUtils.GetTypeFromLoadedAssemblies(fullTypeName, true);
			if (declaringType == null) {
				throw new TypeNotFoundInAssemblyException($"The type with value \"{fullTypeName}\" couldnt be found in the assembly.");
			}

			info.declaringType = declaringType;
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
		}

	}

}
