using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Newtonsoft.Json;
using static Damntry.UtilsBepInEx.HarmonyPatching.CheckResult;


namespace Damntry.UtilsBepInEx.HarmonyPatching {

	/// <summary>
	/// Utility to check if methods have changed between explicit runs.
	/// Both local variables and IL method body are used for this comparison.
	/// </summary>
	public class MethodSignatureChecker {

		//TODO Global 3 - When different signatures are found, write a new dated file with the errors,
		//		overwrite the signature file, and every launch show all those error files.
		//		This way we have the detail of having progressive changes detected, dates and what not.
		//		The error file will have a field like "handled == false", and I would manually make it true
		//		so it stops showing. Eventually I will do something to automate it.

		//TODO Global 6 - Implement a way to ignore specific methods. Either an attribute or through parameters or both.

		/// <summary>
		/// Contains the result and possible error messages of the last check performed.
		/// </summary>
		public CheckResult LastCheckResult { get; private set; }

		private List<MethodInfoData> listMethodInfoData;

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
			LastCheckResult = new CheckResult();

			string pathFolderSignatures = AssemblyUtils.GetCombinedPathFromAssemblyFolder(pluginType, nameof(MethodSignatureChecker));

			if (!Directory.Exists(pathFolderSignatures)) {
				Directory.CreateDirectory(pathFolderSignatures);
			}

			pathFileMethodSignatures = pathFolderSignatures + Path.DirectorySeparatorChar + "methodsignatures.json";
			listMethodInfoData = new();
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
		/// Automatically adds target methods to be checked, by getting all
		/// harmony patches that exist in the assembly passed to the constructor.
		/// </summary>
		public void PopulateMethodSignaturesFromHarmonyPatches() {
			foreach (var (methodInfo, harmonyMethod) in HarmonyMonoMethods.GetAllPatchMethodTargets(pluginType, true)) {
				//targetMethodInfo.mInfo can be null if the method doesnt exist anymore. We keep
				//		all values used so later we have enough info to be notified about it.
				AddMethod(
					methodInfo,
					harmonyMethod.declaringType,
					harmonyMethod.methodName,
					harmonyMethod.argumentTypes);
			}
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// Requires string data from the method in case the methodInfo is null (when the method stops existing)
		/// so we can name the missing function.
		/// </summary>
		/// <param name="methodInfo">The MethodInfo.</param>
		/// <param name="declaredTypeName">The name of the declaring type where the method resides.</param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="typeParameterNames">
		/// Optional parameter names to target a specific overload of the method.
		/// </param>
		public void AddMethod(MethodInfo methodInfo, string declaredTypeName, string methodName,
				string[] typeParameterNames = null) {

			listMethodInfoData.Add(
				new MethodInfoData(methodInfo, declaredTypeName, methodName, typeParameterNames)
			);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// Requires string data from the method in case the methodInfo is null (when the method stops existing)
		/// so we can name the missing function.
		/// </summary>
		/// <param name="methodInfo">The MethodInfo.</param>
		/// <param name="declaringType">The declaring type where the method resides.</param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="parameters">
		/// Optional parameters to target a specific overload of the method.
		/// </param>
		public void AddMethod(MethodInfo methodInfo, Type declaringType, string methodName,
					Type[] parameters = null) {

			listMethodInfoData.Add(
				new MethodInfoData(methodInfo, declaringType, methodName, parameters)
			);
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
		public void AddMethod(string fullTypeName, string methodName, 
				string[] fullTypeParameters = null, Type[] generics = null) {
			listMethodInfoData.Add(
				new MethodInfoData(fullTypeName, methodName, fullTypeParameters, generics)
			);
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
		public void AddMethod(Type declaringType, string methodName, 
				string[] fullTypeParameters = null, Type[] generics = null) {
			listMethodInfoData.Add(
				new MethodInfoData(declaringType, methodName, fullTypeParameters, generics)
			);
		}

		/// <summary>
		/// Adds a new method signature to later check if a previous one exist of the same method to compare against.
		/// </summary>
		/// <param name="declaringType">
		/// The declaring type where the method resides.
		/// </param>
		/// <param name="methodName">Name of the method.</param>
		public void AddMethod(Type declaringType, string methodName) {
			listMethodInfoData.Add(
				new MethodInfoData(declaringType, methodName, parameters: null, null)
			);
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
		public void AddMethod(Type declaringType, string methodName, Type[] parameters = null, Type[] generics = null) {
			listMethodInfoData.Add(
				new MethodInfoData(declaringType, methodName, parameters, generics)
			);
		}

		private MethodSignature CreateMethodSignature(MethodInfo methodInfo, MethodDefinition methodDef) {
			MethodSignature mSig = new();

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
		/// have not been added before using AddMethod(...)</exception>
		public CheckResult StartSignatureCheck() {
			CheckResult checkResult = new CheckResult(SignatureCheckResult.Started, "");

			Dictionary<string, MethodSignature> currentMethodSignatures = new();

			foreach (MethodInfoData methodInfoData in listMethodInfoData) {
				if (GetMethodSignature(methodInfoData, out var mSigEntry)) {
					//There can be multiple patches targeting the same method,
					//	 so this case is allowed and we just continue.
					if (!currentMethodSignatures.ContainsKey(mSigEntry.Key)) {
						currentMethodSignatures.Add(mSigEntry.Key, mSigEntry.Value);
					}
				}
			}

			if (currentMethodSignatures?.Any() != true) {
				return checkResult.SetValues(SignatureCheckResult.NoSignaturesAdded, "No method signature has been added. Signature comparison will be skipped.");
			}

			if (TryLoadSignaturesFromFile(checkResult)) {
				CompareOldSignaturesAgainstNew(checkResult, currentMethodSignatures);
			}

			//Overwrite existing signatures, but only if there was no error. Otherwise we want 
			//	next launch to keep warning about different signatures until dealt with manually.
			if (checkResult.Result != SignatureCheckResult.SignaturesDifferent) {
				SaveSignaturesToFile(currentMethodSignatures);
			}

			if (checkResult.Result == SignatureCheckResult.Started) {
				throw new InvalidOperationException("Something went wrong. Method check finished with result \"Started\".");
			}

			return LastCheckResult = checkResult;
		}

		private bool GetMethodSignature(MethodInfoData methodInfoData, out KeyValuePair<string, MethodSignature> methodSigEntry) {
			string fullMethodName = "";
			MethodSignature mSig = null;

			MethodInfo methodInfo = methodInfoData.GetMethodInfo();

			if (methodInfo != null) {
				MethodDefinition methodDef = HarmonyMonoMethods.GetMethodDefinition(methodInfo);
				if (methodDef == null) {
					methodSigEntry = default;
					return false;
				}

				mSig = CreateMethodSignature(methodInfo, methodDef);
			}

			fullMethodName = methodInfoData.GetFullMethodName();

			methodSigEntry = new KeyValuePair<string, MethodSignature>(fullMethodName, mSig);
			return true;
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
					TimeLogger.FormatException(ex, "Error while reading and deserializing from file " +
					$"\"{pathFileMethodSignatures}\". It might be corrupted. Trying to delete file and skipping loading signatures."));

				try {	//Try to delete but if it doesnt work it wont matter. This is a convenience for the dev, not the user.
					File.Delete(pathFileMethodSignatures);
				}catch { }

				return false;
			}
			
			return true;
		}

		private void SaveSignaturesToFile(Dictionary<string, MethodSignature> currentMethodSignatures) {
			string jsonString = JsonConvert.SerializeObject(currentMethodSignatures, Formatting.Indented);
			File.WriteAllText(pathFileMethodSignatures, jsonString, Encoding.Unicode);
		}

		private void CompareOldSignaturesAgainstNew(CheckResult checkResult, 
				Dictionary<string, MethodSignature> currentMethodSignatures) {
			if (previousMethodSignatures?.Any() != true || currentMethodSignatures?.Any() != true) {
				return;
			}

			foreach (var methodSigPair in currentMethodSignatures) {
				if (previousMethodSignatures.TryGetValue(methodSigPair.Key, out MethodSignature mSigOld)) {
					MethodSignature mSigNew = methodSigPair.Value;

					if (mSigOld != null && !mSigOld.Equals(mSigNew, out string errorDetail)) {
						checkResult.Result = SignatureCheckResult.SignaturesDifferent;
						checkResult.AddErrorMessage($"{errorDetail} | {methodSigPair.Key}");
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

				if (other == null) {
					errorDetail = "\tWrong method call or no longer exists.";
				} else if (Arguments != other.Arguments) {
					errorDetail = "\tArguments are different";
				} else if (ReturnType != other.ReturnType) {
					errorDetail = "\tReturn types are different";
				} else if (IL_BodyHashcode != other.IL_BodyHashcode) {
					errorDetail = "\tIL method body hashcode is different";
				}

				if (errorDetail != null) {
					errorDetail = errorDetail.PadRight(38);   //TODO Global 7 - Autocalculate this length value.
				}
				
				return errorDetail == null;
			}

		}

		private readonly struct MethodInfoData {

			//Flag to indicate we are using the methodInfo var, not matter if its null.
			private readonly bool isMethodInfo;

			private readonly MethodInfo methodInfo;

			private readonly Type declaringType;
			private readonly string methodName;
			private readonly Type[] parameters;
			private readonly Type[] generics;

			private readonly string fullTypeName;
			private readonly string[] fullTypeParameters;

			private readonly FullMethodName fullMethodName;


			public MethodInfoData(MethodInfo methodInfo, string declaredTypeName, string methodName,
					string[] typeParameterNames = null) {

				if (string.IsNullOrEmpty(declaredTypeName)) {
					throw new ArgumentNullException($"{nameof(declaredTypeName)} cannot be empty.");
				}
				if (string.IsNullOrEmpty(methodName)) {
					throw new ArgumentNullException($"{nameof(methodName)} cannot be empty.");
				}

				this.methodInfo = methodInfo;
				fullMethodName = new FullMethodName(declaredTypeName, methodName, typeParameterNames);

				isMethodInfo = true;
			}

			public MethodInfoData(MethodInfo methodInfo, Type declaringType, string methodName,
					Type[] parameters = null, Type[] generics = null) {

				if (declaringType == null) {
					throw new ArgumentNullException($"{nameof(declaringType)} cannot be null.");
				}
				if (string.IsNullOrEmpty(methodName)) {
					throw new ArgumentException($"{nameof(methodName)} cannot be null or empty.");
				}

				this.methodInfo = methodInfo;
				fullMethodName = new FullMethodName(declaringType.Name, methodName, parameters);

				isMethodInfo = true;
			}

			public MethodInfoData(Type declaringType, string methodName,
					Type[] parameters = null, Type[] generics = null) {

				if (declaringType == null) {
					throw new ArgumentNullException($"{nameof(declaringType)} cannot be null.");
				}
				if (string.IsNullOrEmpty(methodName)) {
					throw new ArgumentException($"{nameof(methodName)} cannot be null or empty.");
				}

				this.declaringType = declaringType;
				this.methodName = methodName;
				this.parameters = parameters;
				this.generics = generics;
				fullMethodName = new FullMethodName(declaringType.Name, methodName, parameters);
			}

			public MethodInfoData(Type declaringType, string methodName,
					string[] fullTypeParameters = null, Type[] generics = null) {

				if (declaringType == null) {
					throw new ArgumentNullException($"{nameof(declaringType)} cannot be null.");
				}
				if (string.IsNullOrEmpty(methodName)) {
					throw new ArgumentException($"{nameof(methodName)} cannot be null or empty.");
				}

				this.declaringType = declaringType;
				this.methodName = methodName;
				this.fullTypeParameters = fullTypeParameters;
				this.generics = generics;
				fullMethodName = new FullMethodName(declaringType.Name, methodName, fullTypeParameters);
			}

			public MethodInfoData(string fullTypeName, string methodName,
					string[] fullTypeParameters = null, Type[] generics = null) {

				if (string.IsNullOrEmpty(fullTypeName)) {
					throw new ArgumentNullException($"{nameof(fullTypeName)} cannot be null or empty.");
				}
				if (string.IsNullOrEmpty(methodName)) {
					throw new ArgumentException($"{nameof(methodName)} cannot be null or empty.");
				}

				this.fullTypeName = fullTypeName;
				this.methodName = methodName;
				this.fullTypeParameters = fullTypeParameters;
				this.generics = generics;
				fullMethodName = new FullMethodName(fullTypeName, methodName, fullTypeParameters);
			}

			public MethodInfo GetMethodInfo() {
				if (isMethodInfo) {
					return methodInfo;
				} else {
					Type type = declaringType != null ? declaringType :
					AssemblyUtils.GetTypeFromLoadedAssemblies(fullTypeName, true);
					Type[] paramTypes = parameters != null ? parameters :
						AssemblyUtils.GetTypesFromLoadedAssemblies(true, fullTypeParameters);

					return AccessTools.Method(type, methodName, paramTypes, generics);
				}
			}

			public string GetFullMethodName() => fullMethodName.GetText();


			private readonly struct FullMethodName {

				private readonly string method;
				private readonly string declaringType;
				private readonly string[] parameters;

				public FullMethodName(string declaredTypeName, string methodName,
						string[] typeParameterNames = null) {

					this.method = methodName;
					this.declaringType = ReflectionHelper.ConvertFullTypeToNormal(declaredTypeName);
					this.parameters = ReflectionHelper.ConvertFullTypesToNormal(typeParameterNames);
				}

				public FullMethodName(string declaredTypeName, string methodName,
						Type[] typeParameter = null) {

					this.method = methodName;
					this.declaringType = ReflectionHelper.ConvertFullTypeToNormal(declaredTypeName);

					string[] param = null;
					if (typeParameter != null && typeParameter.Length > 0) {
						param = typeParameter.Where(t => t != null).Select(t => t.Name).ToArray();
					}
					this.parameters = param;
				}


				public string GetText() {
					string paramsStr = "";
					if (parameters != null && parameters.Length > 0) {
						paramsStr = string.Join(", ", parameters);
					}

					return $"{declaringType}.{method}({paramsStr})";
				}

			};

		}

	}

	public class CheckResult {

		public static CheckResult UnknownError = new CheckResult(
			SignatureCheckResult.Unchecked, "Unknown Error due to exception"
		);


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
			if (string.IsNullOrEmpty(ResultMessage)) {
				ResultMessage = $"Method signatures have changed since last successful check:";
			}
			ResultMessage += $"\n{resultMessage}";
		}

		/// <summary>
		/// If a message exists, logs it and optionally shows it in-game.
		/// </summary>
		/// <param name="logLevel">FileLogging level.</param>
		/// <param name="onlyWhenNotOk">Only logs if the result was some kind of problem we should be aware of.</param>
		/// <param name="showInGame">If it should show in-game too.</param>
		public void LogResultMessage(LogTier logLevel, bool onlyWhenNotOk, bool showInGame) {
			if (ShouldLogMessage(onlyWhenNotOk)) {
				TimeLogger.Logger.LogTime(logLevel, ResultMessage, LogCategories.MethodChk, showInGame);
			}
		}

		private bool ShouldLogMessage(bool onlyWhenNotOk) {
			if (string.IsNullOrEmpty(ResultMessage)) {
				if (Result == SignatureCheckResult.Unchecked) {
					return false;
				} else {
					//This shouldnt happen.
					ResultMessage = "Result message was empty for a result in which this is not allowed.";
					return true;
				}
			}

			if (onlyWhenNotOk && (Result == SignatureCheckResult.SignaturesOk || Result == SignatureCheckResult.NoPreviousSignatures)) {
				return false;
			}

			return true;
		}

	}

}
