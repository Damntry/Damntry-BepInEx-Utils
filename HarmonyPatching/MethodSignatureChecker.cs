using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Damntry.UtilsBepInEx.Logging;
using System.Text.Json;
using Damntry.Utils.Reflection;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using Mono.CompilerServices.SymbolWriter;
using Mono.Cecil.Rocks;


namespace SuperQoLity.SuperMarket.ModUtils {


	/// <summary>
	/// Utility to check if methods have changed between explicit runs.
	/// Both local variables and IL method body are used for this comparison.
	/// </summary>
	public class MethodSignatureChecker {

		//TODO Global 3 - The steps to make this work semiautomatically with autopatching are awkward.
		//	1. Create the instance of this class.
		//	2. Manually start autopatching passing this instance as parameter.
		//	3. Manually start signature checking
		//	
		//	Having to call the autopatcher like that for a signature checking to work is counterintuitive.
		//	The thing is that its both the combination of autopatching and harmony that goes through all
		//	patch methods, and replicating that functionality to do the signature checking without depending
		//	on the autopatching doing the actual patching, would require quite a bit of work, and it would
		//	be a bit wasteful.



		/// <summary>
		/// Initializes the MethodSignatureChecker and prepares the
		/// path in the mod subfolder to write the method signatures.
		/// </summary>
		/// <param name="pluginType">
		/// A Type that exists in the mod assembly, used to find the .dll path to save/load the signatures.
		/// Usually the BaseUnityPlugin plugin entry point, but can be any type from the same assembly.
		/// </param>
		public MethodSignatureChecker(Type pluginType) {
			currentMethodSignatures = new Dictionary<string, MethodSignature>();

			pathAssemblyFile = AssemblyUtils.GetAssemblyDllFolderPath(pluginType);
			string pathFolderSignatures = pathAssemblyFile + Path.DirectorySeparatorChar + nameof(MethodSignatureChecker);

			if (!Directory.Exists(pathFolderSignatures)) {
				Directory.CreateDirectory(pathFolderSignatures);
			}

			pathFileMethodSignatures = pathFolderSignatures + Path.DirectorySeparatorChar + "methodsignatures.json";
		}


		private Dictionary<string, MethodSignature> currentMethodSignatures;

		private Dictionary<string, MethodSignature> previousMethodSignatures;

		private readonly string pathFileMethodSignatures;

		private readonly string pathAssemblyFile;

		private bool signatureAdded;


		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		/// <param name="fullTypeName">
		/// The declaring type where the method resides, in the form: "Namespace.Type1.Type2".
		/// </param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="fullTypeParameters">
		/// Optional parameters to target a specific overload of the method.
		/// Must be in the format of "Namespace.Type1.TypeFind".
		/// </param>
		/// <param name="generics">Optional list of types that define the generic version of the method.</param>
		public void AddMethodSignature(string fullTypeName, string methodName, string[] fullTypeParameters = null, Type[] generics = null) {
			signatureAdded = true;

			Type declaringType = AssemblyUtils.GetTypeFromLoadedAssemblies(fullTypeName, true);

			AddMethodSignature(declaringType, methodName, fullTypeParameters, generics);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		/// <param name="declaringType">
		/// The declaring type where the method resides.
		/// </param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="fullTypeParameters">
		/// Optional parameters to target a specific overload of the method.
		/// Must be in the format of "Namespace.Type1.TypeFind".
		/// </param>
		/// <param name="generics">Optional list of types that define the generic version of the method.</param>
		public void AddMethodSignature(Type declaringType, string methodName, string[] fullTypeParameters = null, Type[] generics = null) {
			signatureAdded = true;

			Type[] argumentTypes = AssemblyUtils.GetTypesFromLoadedAssemblies(true, fullTypeParameters);
			MethodInfo methodInfo = AccessTools.Method(declaringType, methodName, argumentTypes, generics);

			if (methodInfo == null) {
				BepInExTimeLogger.Logger.LogTimeWarning($"The method {declaringType}:{methodName} could not be found to compare its signature.", Damntry.Utils.Logging.TimeLoggerBase.LogCategories.Loading);
				return;
			}

			AddMethodSignature(methodInfo);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		public void AddMethodSignature(MethodInfo methodInfo) {
			signatureAdded = true;

			MethodSignature mSig = CreateMethodSignature(methodInfo);

			string fullyQualifiedName = GetFullyQualifiedName(methodInfo);
			currentMethodSignatures.Add(fullyQualifiedName, mSig);
		}

		private string GetFullyQualifiedName(MethodInfo methodInfo) {
			return methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
		}

		//TODO 1 - Remember to note that it doesnt take into account the AutoPatchIgnore attribute, by design, and they will be included.
		private IEnumerable<MethodSignature> GetOriginalPatchMethodsSignature() {
			//TODO 1 - The idea of this is to trasverse all patch methods in this assembly, and those are the ones that
			//		will be automatically picked up for signature checking, instead of using autopatch.
			//TODO 2 - When I finish this and it works, remember to remove from AutoPatcher all related functionality.
			AssemblyDefinition.ReadAssembly(pathAssemblyFile);

			return AssemblyDefinition.ReadAssembly(pathAssemblyFile).MainModule
				.GetTypes()
				.SelectMany(typeDef => typeDef.Methods
					.Where(methodDef => methodDef.HasBody && 
						methodDef.CustomAttributes.Any(attr => attr.GetType().IsSubclassOf(typeof(HarmonyAttribute))))
					.Select(methodDef => CreateMethodSignature(methodDef)));
		}

		private MethodSignature CreateMethodSignature(MethodDefinition methodDef) {
			//TODO 1 - I need to get the original method that the method is patching, not the patch method
			//	itself, so I need to delve into the HarmonyPatch attributes of the patch classes I find.
			//	Basically now I have to repeat the same process as in GetPatchMethods but reading from the game
			//	assembly, ot mine, and selecting the methods that the harmony info property was pointing to.
			//		***There might be a way of creating a MethodDefinition from a constructor directly, which would
			//	be better than going through the assembly.

			/*
			methodDef.Parameters.
			methodDef.CustomAttributes.Single().;


			AssemblyDefinition.ReadAssembly(pathGameAssemblyDll);

			return AssemblyDefinition.ReadAssembly(pathAssemblyFile).MainModule
				.GetTypes()
				.SelectMany(typeDef => typeDef.Methods
					.Where(methodDef => methodDef.HasBody &&
						methodDef.CustomAttributes.Any(attr => attr.GetType().IsSubclassOf(typeof(HarmonyAttribute))))
					.Select(methodDef => CreateMethodSignature(methodDef)));
			*/
		}

		/*
		private MethodSignature CreateMethodSignature(MethodInfo methodInfo) {
			MethodSignature mSig = new();

			mSig.LocalSignatureMetadataToken = methodInfo.GetMethodBody().LocalSignatureMetadataToken;
			//Join implicitly calls ToString() on each element.
			mSig.LocalVariables = string.Join("", methodInfo.GetMethodBody().LocalVariables);
			mSig.IL_MethodBody = methodInfo.GetMethodBody().GetILAsByteArray();
			return mSig;
		}
		*/

		/// <summary>
		/// Starts the process of comparing old with new method signatures.
		/// After the comparison is done, new signatures are saved to disk
		/// and used in the next check as old signatures.
		/// </summary>
		/// <returns>True if no differences were detected, Otherwise false.</returns>
		/// <exception cref="InvalidOperationException">Error if Method signatures
		/// have not been added before using AddMethodSignature(...)</exception>
		public bool StartSignatureCheck() {
			bool signatureOk = true;

			if (!signatureAdded) {
				throw new InvalidOperationException("You need to try adding method signatures through \"AddMethodSignature(...)\" before starting the check.");
			}

			bool dataExists = LoadSignaturesFromDisk();
			if (dataExists) {
				signatureOk = CompareOldSignaturesAgainstNew();
			}

			SaveSignaturesToDisk();

			return signatureOk;
		}


		private bool LoadSignaturesFromDisk() {
			if (!File.Exists(pathFileMethodSignatures)) {
				return false;
			}

			try {
				string jsonString = File.ReadAllText(pathFileMethodSignatures, Encoding.Unicode);
				previousMethodSignatures = JsonSerializer.Deserialize<Dictionary<string, MethodSignature>>(jsonString);
			} catch (Exception ex) {
				BepInExTimeLogger.Logger.LogTimeExceptionWithMessage($"Error while reading and deserializing from " +
					$"file \"{pathFileMethodSignatures}\". It might be corrupted. Skipping loading signatures.", ex, Damntry.Utils.Logging.TimeLoggerBase.LogCategories.Loading);
				return false;
			}

			return true;
		}

		private void SaveSignaturesToDisk() {
			//File.WriteAllBytes(pathMethodSignatures, GetSignaturesByteArray());
			string jsonString = JsonSerializer.Serialize(currentMethodSignatures);
			File.WriteAllText(pathFileMethodSignatures, jsonString, Encoding.Unicode);
		}

		private bool CompareOldSignaturesAgainstNew() {
			if (previousMethodSignatures?.Any() != true || currentMethodSignatures?.Any() != true) {
				return true;
			}

			bool signaturesOk = true;

			foreach (var methodSigPair in currentMethodSignatures) {
				if (previousMethodSignatures.TryGetValue(methodSigPair.Key, out MethodSignature mSigOld)) {
					MethodSignature mSigNew = methodSigPair.Value;

					if (!mSigOld.Equals(mSigNew, out string errorDetail)) {
						signaturesOk = false;

						BepInExTimeLogger.Logger.LogTimeWarning($"The signature of the method {methodSigPair.Key} has changed " +
							$"since last check. Detail: {errorDetail}. Make sure to check if any changes are needed locally.",
							Damntry.Utils.Logging.TimeLoggerBase.LogCategories.Loading);
					}
				}
			}

			return signaturesOk;
		}

		private class MethodSignature {

			internal int LocalSignatureMetadataToken { get; set; }
			internal string LocalVariables { get; set; }
			internal byte[] IL_MethodBody { get; set; }

			internal bool Equals(MethodSignature other, out string errorDetail) {
				errorDetail = null;

				if (LocalSignatureMetadataToken != other.LocalSignatureMetadataToken) {
					errorDetail = "Local variable metadata signatures are different.";
					return false;
				}
				if (LocalVariables != other.LocalVariables) {
					errorDetail = "Local variable definitions are different.";
					return false;
				}
				if (!IL_MethodBody.SequenceEqual(other.IL_MethodBody)) {
					errorDetail = "IL body of the method is different.";
					return false;
				}

				return true;
			}

		}

		/*
		private class MethodSignature {

			internal int LocalSignatureMetadataToken { get; set; }
			internal string LocalVariables { get; set; }
			internal byte[] IL_MethodBody { get; set; }

			internal bool Equals(MethodSignature other, out string errorDetail) {
				errorDetail = null;

				if (LocalSignatureMetadataToken != other.LocalSignatureMetadataToken) {
					errorDetail = "Local variable metadata signatures are different.";
					return false;
				}
				if (LocalVariables != other.LocalVariables) {
					errorDetail = "Local variable definitions are different.";
					return false;
				}
				if (!IL_MethodBody.SequenceEqual(other.IL_MethodBody)) {
					errorDetail = "IL body of the method is different.";
					return false;
				}

				return true;
			}

		}
		*/

		/* Deprecated. Leave for now.
		
		private byte[] GetSignaturesByteArray() {
			List<byte[]> listByteArrSignatures = new();

			foreach (var methodSig in methodSignatures) {

				listByteArrSignatures.Add([
					.. IntToByteArray(methodSig.Value.LocalSignatureMetadataToken),
					.. Encoding.Unicode.GetBytes(methodSig.Value.LocalVariables.ToString()),
					.. methodSig.Value.IL_MethodBody
				]);
			}

			return listByteArrSignatures.SelectMany(b => b).ToArray();
		}

		private byte[] IntToByteArray(int intValue) {
			byte[] intBytes = BitConverter.GetBytes(intValue);
			if (BitConverter.IsLittleEndian) {
				Array.Reverse(intBytes);
			}
			return intBytes;
		}

		private int ByteArrayToInt(byte[] byteArrayValue) {
			return BitConverter.ToInt16(byteArrayValue, 0);
		}
		*/
	}

}
