using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Damntry.Utils.Reflection;
using Damntry.Utils.Logging;
using HarmonyLib;
using MonoMod.Utils;
using Mono.Cecil;
using Mono.Collections.Generic;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using static Damntry.UtilsBepInEx.HarmonyPatching.CheckResult;
using Damntry.UtilsBepInEx.HarmonyPatching.Exceptions;

namespace Damntry.UtilsBepInEx.HarmonyPatching {

	/// <summary>
	/// Utility to check if methods have changed between explicit runs.
	/// Both local variables and IL method body are used for this comparison.
	/// </summary>
	public class MethodSignatureChecker {

		//TODO Global 7 - When a different signature is found, instead of not overwriting the signature file, write a
		//		new dated file with the errors, and every launch check if its there to show the error again to remind
		//		me, with the date of failure.
		//		This way we have the detail of having progressive changes detected, dates and what not that I could customize.

		//TODO Global 6 - Implement a way to ignore specific methods. Either an attribute or through parameters or both.

		/// <summary>
		/// Contains the result and possible error messages of the last check performed.
		/// </summary>
		public CheckResult LastCheckResult { get; private set; }

		private Dictionary<string, MethodSignature> currentMethodSignatures;

		private Dictionary<string, MethodSignature> previousMethodSignatures;

		private readonly string pathFileMethodSignatures;

		private readonly Type pluginType;



		/// <summary>
		/// Initializes this class and prepares the
		/// path in the mod subfolder to write method signatures.
		/// </summary>
		/// <param name="pluginType">
		/// A Type that exists in the mod assembly, used to find the .dll path to save/load the signatures.
		/// Usually the BaseUnityPlugin plugin entry point, but can be any type from the same assembly.
		/// </param>
		public MethodSignatureChecker(Type pluginType) {
			this.pluginType = pluginType;
			currentMethodSignatures = new Dictionary<string, MethodSignature>();
			LastCheckResult = new CheckResult();

			string pathFolderSignatures = AssemblyUtils.GetCombinedPathFromAssemblyFolder(pluginType, nameof(MethodSignatureChecker));

			if (!Directory.Exists(pathFolderSignatures)) {
				Directory.CreateDirectory(pathFolderSignatures);
			}

			pathFileMethodSignatures = pathFolderSignatures + Path.DirectorySeparatorChar + "methodsignatures.json";
		}

		/// <summary>
		/// 
		/// </summary>
		public void PopulateMethodSignaturesFromHarmonyPatches() {
			foreach (var targetMethodInfo in GetAllPatchMethodTargets(pluginType)) {
				AddMethodSignature(targetMethodInfo);
			}
		}

		/// <summary>
		/// Initialized this object, adds all harmony patched method signatures, and starts to check immediately.
		/// For when you dont need to add method signatures manually.
		/// </summary>
		/// <param name="pluginType">
		/// A Type that exists in the mod assembly, used to find the .dll path to save/load the signatures.
		/// Usually the BaseUnityPlugin plugin entry point, but can be any type from the same assembly.
		/// </param>
		/// <returns></returns>
		public static MethodSignatureChecker StartSignatureCheck(Type pluginType) {
			var mSigCheck = new MethodSignatureChecker(pluginType);
			mSigCheck.PopulateMethodSignaturesFromHarmonyPatches();
			mSigCheck.StartSignatureCheck();
			return mSigCheck;
		}


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
			Type[] argumentTypes = AssemblyUtils.GetTypesFromLoadedAssemblies(true, fullTypeParameters);

			AddMethodSignature(declaringType, methodName, argumentTypes, generics);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		/// <param name="declaringType">
		/// The declaring type where the method resides.
		/// </param>
		/// <param name="methodName">Name of the method.</param>
		public void AddMethodSignature(Type declaringType, string methodName) {
			AddMethodSignature(declaringType, methodName, parameters: null, null);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		/// <param name="declaringType">
		/// The declaring type where the method resides.
		/// </param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="parameters">
		/// Optional parameters to target a specific overload of the method.
		/// </param>
		/// <param name="generics">Optional list of types that define the generic version of the method.</param>
		public void AddMethodSignature(Type declaringType, string methodName, Type[] parameters = null, Type[] generics = null) {
			MethodInfo methodInfo = AccessTools.Method(declaringType, methodName, parameters, generics);

			if (methodInfo == null) {
				//TODO 6 - methodInfo null could mean that the method no longer exists, and it needs to be warned properly.
				//	Since we cant differentiate between a dev error and the method vanishing, we should not throw an 
				//	error here (its fine in AddMethodSignature(MethodInfo methodInfo) since the caller is supposed to control
				//	that), and instead have a way to have this possible error included when the check is started.
				//	Make sure to note in the log that it could just be the devs fault from wrong param values.
				return;
			}

			AddMethodSignature(methodInfo);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		public void AddMethodSignature(MethodInfo methodInfo) {
			if (methodInfo == null) {
				throw new ArgumentNullException(nameof(methodInfo));
			}

			MethodDefinition methodDef = GetMethodDefinition(methodInfo);

			MethodSignature mSig = CreateMethodSignature(methodInfo, methodDef);

			string fullyQualifiedName = GetFullyQualifiedName(methodInfo);

			if (!currentMethodSignatures.ContainsKey(fullyQualifiedName)) {  //There can be multiple patches targeting the same method
				currentMethodSignatures.Add(fullyQualifiedName, mSig);
			}
		}

		private string GetFullyQualifiedName(MethodInfo methodInfo) {
			return methodInfo.DeclaringType.FullName + "." + methodInfo.Name;
		}

		/// <summary>
		/// Gets a Mono.Cecil MethodDefinition from a MethodInfo.
		/// </summary>
		private MethodDefinition GetMethodDefinition(MethodInfo methodInfo) {
			string dllPath = AssemblyUtils.GetAssemblyDllFilePath(methodInfo.DeclaringType);
			//TODO Global 5 - I should be caching this per dllPath
			var assemblyDef = AssemblyDefinition.ReadAssembly(dllPath);

			MethodDefinition methodDef = assemblyDef.MainModule
				.GetType(methodInfo.DeclaringType.FullName)
				.FindMethod(methodInfo.GetID());

			//TODO Global 6 - Check which method performs faster while still working most of the time, to make it the first option.
			if (methodDef == null) {
				//Try second method.
				methodDef = MethodBaseToMethodDefinition(methodInfo);
			}

			return methodDef;
		}

		public MethodDefinition MethodBaseToMethodDefinition(MethodBase method) {
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
		private IEnumerable<MethodInfo> GetAllPatchMethodTargets(Type assemblyType) {
			return Assembly.GetAssembly(assemblyType).GetTypes()
				.SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(mInfo => IsHarmonyAttribute(mInfo))
					.Select(mInfo => GetTargetMethodFromHarmonyPatchMethod(type, mInfo)));
		}

		private bool IsHarmonyAttribute(MethodInfo methodInfo) {
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
		/// <param name="methodInfo">The MethodInfo of the patch method.</param>
		/// <returns>The MethodInfo of the method that the patch is targetting.</returns>
		private MethodInfo GetTargetMethodFromHarmonyPatchMethod(Type methodClassType, MethodInfo methodInfo) {
			//Get method info from the HarmonyAttributes of the method
			var harmonyMethods = methodInfo.GetCustomAttributes(true)
				.Where(attr => attr is HarmonyAttribute)
				.Select(attr => ((HarmonyAttribute)attr).info);

			//Merge all annotations into a single complete one.
			HarmonyMethod harmonyMethod = HarmonyMethod.Merge(harmonyMethods.ToList());

			if (harmonyMethod.method == null && harmonyMethod.declaringType == null) {
				//Support that the containing class of the method can have the annotation
				//	for the target type, instead of being in the method itself.
				HarmonyMethod harmonyClassAttr = HarmonyMethod.Merge(HarmonyMethodExtensions.GetFromType(methodClassType));
				harmonyMethod = harmonyClassAttr.Merge(harmonyMethod);
			}

			//Access the internal method that handles getting a methodInfo, taking into account all harmony attributes.
			//TODO 3 - Cache this method. If its null I guess I ll have to use the older method and try/catch it since it
			//		doesnt work in cases where the methodType is not MethodType.Normal.
			//Old method:
			//return harmonyMethod.method ??
			//	AccessTools.Method(harmonyMethod.declaringType, harmonyMethod.methodName, harmonyMethod.argumentTypes);
			MethodInfo mInfoInternal = AccessTools.Method("HarmonyLib.PatchTools:GetOriginalMethod", [typeof(HarmonyMethod)]);
			harmonyMethod.methodType ??= MethodType.Normal;

			return (MethodInfo)mInfoInternal.Invoke(null, [harmonyMethod]);
		}

		private MethodSignature CreateMethodSignature(MethodInfo methodInfo, MethodDefinition methodDef) {
			MethodSignature mSig = new MethodSignature();

			//Join implicitly calls ToString() on each element.
			mSig.Arguments = string.Join("", (object[])methodInfo.GetParameters());
			mSig.ReturnType = methodInfo.ReturnType.FullName;
			mSig.Set_IL_BodyHashcode(methodDef.Body.Instructions);

			return mSig;
		}


		/// <summary>
		/// Starts the process of comparing old with new method signatures.
		/// After the comparison is done, new signatures are saved to disk
		/// and used in the next check as old signatures.
		/// </summary>
		/// <returns>True if no differences were detected, Otherwise false.</returns>
		/// <exception cref="InvalidOperationException">Error if Method signatures
		/// have not been added before using AddMethodSignature(...)</exception>
		public CheckResult StartSignatureCheck() {
			CheckResult checkResult = new CheckResult(SignatureCheckResult.Started, "");

			if (currentMethodSignatures?.Any() != true) {
				return checkResult.SetValues(SignatureCheckResult.NoSignaturesAdded, "No method signature has been added. Signature comparison will be skipped.");
			}

			if (TryLoadSignaturesFromFile(checkResult)) {
				CompareOldSignaturesAgainstNew(checkResult);
			}

			if (checkResult.Result != SignatureCheckResult.SignaturesDifferent) {	//Dont save, so next launch we keep warning about different signatures.
				SaveSignaturesToFile();
			}

			if (checkResult.Result == SignatureCheckResult.Started) {
				throw new InvalidOperationException("Something went wrong. Method check finished with result \"Started\".");
			}

			return LastCheckResult = checkResult;
		}


		private bool TryLoadSignaturesFromFile(CheckResult checkResult) {
			if (!File.Exists(pathFileMethodSignatures)) {
				checkResult.SetValues(SignatureCheckResult.NoPreviousSignatures, "No method signature file. Skipped check.");
				return false;
			}
			
			try {
				string jsonString = File.ReadAllText(pathFileMethodSignatures, Encoding.Unicode);
				
				previousMethodSignatures = JsonConvert.DeserializeObject<Dictionary<string, MethodSignature>>(jsonString);
			} catch (Exception ex) {
				checkResult.SetValues(SignatureCheckResult.FileError,
					TimeLoggerBase.FormatException(ex, "Error while reading and deserializing from file " +
					$"\"{pathFileMethodSignatures}\". It might be corrupted. Trying to delete file and skipping loading signatures."));

				try {	//Try to delete but if it doesnt work it wont matter. This is a convenience for the dev, not the user.
					File.Delete(pathFileMethodSignatures);
				}catch { }

				return false;
			}
			
			return true;
		}

		private void SaveSignaturesToFile() {
			string jsonString = JsonConvert.SerializeObject(currentMethodSignatures, Formatting.Indented);
			File.WriteAllText(pathFileMethodSignatures, jsonString, Encoding.Unicode);
		}

		private void CompareOldSignaturesAgainstNew(CheckResult checkResult) {
			if (previousMethodSignatures?.Any() != true || currentMethodSignatures?.Any() != true) {
				return;
			}

			foreach (var methodSigPair in currentMethodSignatures) {
				if (previousMethodSignatures.TryGetValue(methodSigPair.Key, out MethodSignature mSigOld)) {
					MethodSignature mSigNew = methodSigPair.Value;

					if (!mSigOld.Equals(mSigNew, out string errorDetail)) {
						checkResult.Result = SignatureCheckResult.SignaturesDifferent;
						checkResult.AddErrorMessage($"The signature of the method {methodSigPair.Key} has changed " +
							$"since last check. Detail: {errorDetail}. Make sure to check if any changes are needed locally.");
					}
				}
			}

			if (checkResult.Result != SignatureCheckResult.SignaturesDifferent) {
				checkResult.SetValues(SignatureCheckResult.SignaturesOk, "Method signature check ok.");
			}
		}


		internal class MethodSignature {

			public string Arguments { get; set; }
			public string ReturnType { get; set; }
			public int IL_BodyHashcode { get; set; }

			/* Test to see IL
			public void Set_IL_BodyHashcode(Collection<Instruction> instructions) {
				if (instructions == null) {
					throw new ArgumentNullException(nameof(instructions));
				}

				StringBuilder sb = new StringBuilder();
				foreach (var instruction in instructions) {
					sb.AppendLine(instruction.ToString());
				}

				IL_BodyHashcode = sb.ToString();
			}
			*/
			/// <summary>
			/// Taken from https://stackoverflow.com/a/263416/739345 by Jon Skeet
			/// </summary>
			public void Set_IL_BodyHashcode(Collection<Instruction> instructions) {
				if (instructions == null) {
					throw new ArgumentNullException(nameof(instructions));
				}

				unchecked { // Overflow is fine, just wrap
					int hash = 17;
					foreach (var instruction in instructions) {
						hash = hash * 71 + instruction.ToString().GetHashCode();
					}

					IL_BodyHashcode = hash;
				}
			}
			
			internal bool Equals(MethodSignature other, out string errorDetail) {
				errorDetail = null;

				if (Arguments != other.Arguments) {
					errorDetail = "Arguments are different";
					return false;
				}
				if (ReturnType != other.ReturnType) {
					errorDetail = "Return types are different";
					return false;
				}
				if (IL_BodyHashcode != other.IL_BodyHashcode) {
					errorDetail = "IL body hashcode of the method is different";
					return false;
				}

				return true;
			}

		}

	}

	public class CheckResult {

		public enum SignatureCheckResult {
			Unchecked,
			Started,    //To detect errors in flow. The result must never be this.
			FileError,
			NoSignaturesAdded,
			NoPreviousSignatures,
			SignaturesDifferent,
			SignaturesOk
		}

		internal CheckResult() {
			ResultMessage = "";
			Result = SignatureCheckResult.Unchecked;
		}

		internal CheckResult(SignatureCheckResult result, string resultMessage) {
			ResultMessage = resultMessage;
			Result = result;
		}

		internal CheckResult SetValues(SignatureCheckResult result, string resultMessage) {
			ResultMessage = resultMessage;
			Result = result;
			return this;
		}

		internal void AddErrorMessage(string resultMessage) {
			if (ResultMessage != "") {
				ResultMessage += $"\n{resultMessage}";
			} else {
				ResultMessage = resultMessage;
			}
		}

		/// <summary>
		/// If a message exists, logs it and optionally shows it in-game.
		/// </summary>
		/// <param name="logLevel">Log level.</param>
		/// <param name="onlyWhenNotOk">Only logs if the result was some kind of problem we should be aware of.</param>
		/// <param name="showInGame">If it should show in-game too.</param>
		public void LogResultMessage(TimeLoggerBase.LogTier logLevel, bool onlyWhenNotOk, bool showInGame) {
			if (ShouldLogMessage(onlyWhenNotOk)) {
				TimeLoggerBase.Logger.LogTime(logLevel, ResultMessage, TimeLoggerBase.LogCategories.MethodChk, showInGame);
			}
		}

		private bool ShouldLogMessage(bool onlyWhenNotOk) {
			if (string.IsNullOrEmpty(ResultMessage)) {
				if (Result == SignatureCheckResult.Unchecked) {
					return false;
				} else {
					//This shouldnt happen.
					ResultMessage = "Result message was empty for a result where it is not allowed.";
					return true;
				}
			}

			if (onlyWhenNotOk && (Result == SignatureCheckResult.SignaturesOk || Result == SignatureCheckResult.NoPreviousSignatures)) {
				return false;
			}

			return true;
		}


		public SignatureCheckResult Result { get; internal set; }

		private string _result;

		public string ResultMessage {
			get { return _result; }
			private set {
				if (value == null) {
					_result = "";
				}
				_result = value;
			}
		}

	}

}
