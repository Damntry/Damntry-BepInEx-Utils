using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Damntry.Utils.ExtensionMethods;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.IL;
using Damntry.UtilsBepInEx.MirrorNetwork.Attributes;
using Damntry.UtilsBepInEx.MirrorNetwork.Helpers;
using Damntry.UtilsBepInEx.MirrorNetwork.SyncVar;
using HarmonyLib;
using Mirror;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Components {

	internal interface ISyncVarBehaviour {

		void StartNetworkSession(NetworkMode networkMode);

	}

	//TODO 0 Network - Change name of this. Its no longer just for SyncVars, but also CMDs and RPCs
	/// <summary>
	/// Primarily meant to be used to automatically initialize <see cref="SyncVar{T}"/> objects 
	/// annotated with the attribute <see cref="SyncVarNetworkAttribute"/>
	/// </summary>
	/// <typeparam name="T">The class that derives from this SyncVarNetworkBehaviour.</typeparam>
	public abstract class SyncVarNetworkBehaviour<T> : NetworkBehaviour, ISyncVarBehaviour 
			where T : SyncVarNetworkBehaviour<T> {

		//TODO 0 Network - Out of curiosity, do a quick test and see if the annotated function having a
		//	return object works (it shouldnt). See how easy it would be to fix, or otherwise just check for it
		//	and throw an exception.

		//TODO Global 6 - If I host with a mod using this, and a client joins without the mod, everything
		//	works on the host side, but the client receives isValid in the console when the host calls
		//	Spawn(), since it doesnt know what to do with that data.
		//	The error doesnt cause any issues in itself.
		//	No idea how to fix this now outside of patching Mirror to control who receives certain data,
		//	which is over the top so it ll have to stay for now.

		private static readonly BindingFlags SearchFlags = BindingFlags.DeclaredOnly | BindingFlags.Instance |
				BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;


		private readonly Type derivedType = typeof(T);

		private readonly List<ISyncVar> validSyncVarList = new();

		private static readonly Dictionary<MethodBase, MethodInfo> methodRedirects = new();

		private readonly Harmony harmony = new (typeof(SyncVarNetworkBehaviour<T>).FullName);


		/// <summary>
		/// A new network session has been started, and Mirror objects were already spawned.
		/// SyncVars that are to be used should be instantiated here or earlier.
		/// </summary>
		/// <param name="networkMode">The current network mode.</param>
		protected virtual void OnStartNetworkSession(NetworkMode networkMode) { }

		/// <summary>
		/// Valid SyncVars have been set to its default values in preparation for their network initialization.
		/// SyncVars values cannot be changed at this point until they have been made network ready.
		/// </summary>
		protected virtual void OnSyncVarValuesDefaulted() { }

		/// <summary>
		/// Valid SyncVars have been fully network initialized and are ready to be used.
		/// This method is called before OnStartClient().
		/// </summary>
		protected virtual void OnSyncVarsNetworkReady() { }


		/// <summary>
		/// A new network session has been started, and Mirror objects were already spawned.
		/// All valid SyncVars are validated to be used later.
		/// </summary>
		void ISyncVarBehaviour.StartNetworkSession(NetworkMode networkMode) {
			NetworkSpawnManager.DebugLog(() => $"{nameof(OnStartNetworkSession)} call begins for type {derivedType.Name}.");

			OnStartNetworkSession(networkMode);

			validSyncVarList.Clear();

			var members = GetDerivedSyncVarsOfType(typeof(SyncVar<>));
			foreach (MemberInfoHelper syncVarInfoHelper in members) {
				bool isValid = CheckSyncVarValidity(syncVarInfoHelper, out ISyncVar syncVarInstance);
				if (!isValid) {
					//Skip from later network initialization
					continue;
				}

				validSyncVarList.Add(syncVarInstance);

				syncVarInstance.SetToDefaultValue();
			}

#if DEBUG  //TODO 0 Network - DEBUG ONLY TEMP UNTIL FINAL RELEASE                              
			InitializeRedirectsRPC_CMD();
#endif

			OnSyncVarValuesDefaulted();
		}

		private void Awake() {
			foreach (ISyncVar syncVarInstance in validSyncVarList) {
				InitializeSyncVar(syncVarInstance, (T)this);
			}

			validSyncVarList.Clear();

			NetworkSpawnManager.DebugLog(() => $"{nameof(OnSyncVarsNetworkReady)} call begins for type {derivedType.Name}.");
			OnSyncVarsNetworkReady();
		}

		/// <summary>
		/// Gets a list of SyncVar members declared in the deriving class.
		/// </summary>
		/// <param name="searchType">
		/// The base type of SyncVar to search for. Classes deriving from the type are also included.
		/// </param>
		private List<MemberInfoHelper> GetDerivedSyncVarsOfType(Type searchType) {
			//Find all fields and properties declared in the deriving class
			var members = derivedType.GetFields(SearchFlags).Cast<MemberInfo>()
				.Concat(derivedType.GetProperties(SearchFlags)).Cast<MemberInfo>();

			if (members == null || members.Count() == 0) {
				return [];
			}

			return members
				.Where(mi => mi.HasCustomAttribute<SyncVarNetworkAttribute>())
				.Select(mi => new MemberInfoHelper(mi))
				.Where(mif => mif.MemberInfoType.IsSubclassOfRawGeneric(searchType))
				.ToList();
		}

		private bool CheckSyncVarValidity(MemberInfoHelper syncVarInfoHelper, out ISyncVar syncVarInstance) {
			syncVarInstance = null;
			Type syncVarType = syncVarInfoHelper.MemberInfoType;

			if (!syncVarType.IsSubclassOf(typeof(SyncObject))) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} does not " +
					$"inherit from {nameof(SyncObject)} and will be skipped. Make sure that the " +
					$"{nameof(SyncVarNetworkAttribute)} annotation was intended.", LogCategories.Network);
				return false;
			}
			if (!typeof(ISyncVar).IsAssignableFrom(syncVarType)) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} does not " +
					$"inherit from {nameof(ISyncVar)} and will be skipped. Make sure that the " +
					$"{nameof(SyncVarNetworkAttribute)} annotation was intended.", LogCategories.Network);
				return false;
			}

			if (syncVarType.GetGenericArguments() == null || syncVarType.GetGenericArguments().Length == 0) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} does not " +
					$"declare any generic type parameters and will be skipped. Make sure that the type derives " +
					$"from {nameof(SyncVar<object>)}.", LogCategories.Network);
				return false;
			}

			syncVarInstance = (ISyncVar)syncVarInfoHelper.GetValueStaticAgnostic((T)this);
			if (syncVarInstance == null) {
				TimeLogger.Logger.LogTimeWarning($"The var {derivedType.Name}.{syncVarInfoHelper.Name} has not " +
					$"been instantiated and will be skipped. Make sure to call its constructor.", LogCategories.Network);
				return false;
			}

			return true;
		}



		private void InitializeSyncVar(ISyncVar syncVarInstance, T netBehaviourInstance) {
			if (!IsSyncVarNetworkInitialized((SyncObject)syncVarInstance)) {
				syncVarInstance.InitializeSyncObject(netBehaviourInstance);
			}
		}


		private bool IsSyncVarNetworkInitialized(SyncObject syncVarInstance) => syncObjects.Contains(syncVarInstance);


		//TODO 2 - Make this into a Globals method as generic as possible.
		private Delegate GetOnChangeDelegate(string onChangeMethodName, T netBehaviourInstance, Type syncVarType) {

			//Get type T used as value in the SyncVar
			Type syncVarValueType = syncVarType.GetGenericArguments()[0];

			//Get OnChange method
			MethodInfo onChange = derivedType.GetMethod(onChangeMethodName, SearchFlags,
				null, [syncVarValueType, syncVarValueType], null);
			if (onChange == null) {
				TimeLogger.Logger.LogTimeWarning($"Method \"{onChangeMethodName}\" could not be " +
					$"found in type {derivedType.Name}, or does not have the required signature:  " +
					$"{onChangeMethodName}({syncVarType.Name} oldValue, {syncVarType.Name} newValue).",
					LogCategories.Network);
				return null;
			}

			//Create delegate from MethodInfo to reduce reflection performance hit when invoking
			Type actionGeneric = typeof(Action<,>).MakeGenericType(syncVarValueType, syncVarValueType);

			Delegate methodDelegate = Delegate.CreateDelegate(actionGeneric, netBehaviourInstance, onChange);
			if (methodDelegate == null) {
				TimeLogger.Logger.LogTimeWarning($"A delegate for method " +
					$"\"{derivedType.Name}.{onChangeMethodName}\" could not be created.",
					LogCategories.Network);
				return null;
			}

			return methodDelegate;
		}

		// TODO 0 Network - For the comments
		//
		//	I forgot that the host commonly also need to execute the client code in the onStartClient, so
		//	they NEED to be able to execute it too, which means someone using the single method way, would
		//	need to create an annotated method that calls a normal method with all the actual logic in it, and
		//	the normal method would be called in the onStartClient for both hosts and clients.
		//	WHICH MEANS its literally doubling the methods anyway like my second idea, but still making
		//	it more obscure to understand, just for the moments where the host doesnt need to call the
		//	normal method, and thus you can have just the single annotated method with all the logic inside.
		//
		//	Also, mention that the original code in the annotated method will be executed for the host.

		private void InitializeRedirectsRPC_CMD() {
			var methodsRPC = GetRPC_MethodTargets();
			LOG.TEMPWARNING($"{methodsRPC.Count} RPC methods");
			foreach (var method in methodsRPC) {
				if (methodRedirects.ContainsKey(method.origMethod)) {
					//TODO 0 Network - Log error
					continue;
				}
				methodRedirects.Add(method.origMethod, method.targetMethod);

				//We need to transpile differently if its host or client, so we manually 
				//	patch our own transpile static method to dinamically generate the IL.
				MethodInfo mGet = ((Delegate)TranspileRPC_Call).Method;
				harmony.Patch(method.origMethod, transpiler: new HarmonyMethod(mGet));
			}
			LOG.TEMPWARNING($"Method patching finished.");
		}

		public override void OnStopClient() {
			harmony.UnpatchSelf();
			methodRedirects.Clear();
		}

		/// <summary>
		/// Find all valid methods declared in the deriving class, annotated 
		/// with the attribute <see cref="RPC_CallOnClientAttribute"/>
		/// </summary>
		private List<(MethodInfo origMethod, MethodInfo targetMethod)> GetRPC_MethodTargets() =>
			derivedType.GetMethods(SearchFlags)
				.Where(mi => mi.HasCustomAttribute<RPC_CallOnClientAttribute>())
				.Select(mi => (mi, GetMethodInfoFromRPCAttribute(mi)))
				.Where(tup => tup.Item2 != null)  //Filter out the invalid Delegates.
			.ToList();

		private MethodInfo GetMethodInfoFromRPCAttribute(MethodInfo methodInfo) {
			//Generate delegate from the attribute values
			RPC_CallOnClientAttribute attr = methodInfo.GetCustomAttribute<RPC_CallOnClientAttribute>();
			if (Type.GetType(attr.declaringType.AssemblyQualifiedName) == null) {
				TimeLogger.Logger.LogTimeError($"The type {nameof(attr.declaringType.FullName)} could not be found.",
					LogCategories.Network);
			}

			MethodInfo targetMethodInfo = AccessTools.Method(attr.declaringType, attr.targetMethodName, attr.parameters, attr.generics);

			if (targetMethodInfo != null) {
				if (!CompareMethodSignatures(methodInfo, targetMethodInfo)) {
					targetMethodInfo = null;
				}
			}

			return targetMethodInfo;
		}

		public static bool CompareMethodSignatures(MethodInfo mi1, MethodInfo mi2, bool compareDeclaringType = false) {
			List<string> errors = new();

			//TODO 0 Network - Is mi1.ContainsGenericParameters something I need to check?
			//	I guess I should return false right now and think if it is useful
			//	to add support for methods with generic parameters.
			//There is also mi1.IsGeneric? Check the difference.
			/*
			if (compareDeclaringType && mi1.DeclaringType != mi2.DeclaringType) {

			}
			*/
			if (mi1.IsGenericMethod || mi2.IsGenericMethod) {
				errors.Add("generic methods not supported");
			}
			if (mi1.ReturnType != mi2.ReturnType) {
				errors.Add("return type");
			}
			if (mi1.GetParameters().Length != mi2.GetParameters().Length) {
				errors.Add("number of parameters");
			} else {
				for (int i = 0; i < mi1.GetParameters().Length; i++) {
					if (mi1.GetParameters()[i].ParameterType != mi2.GetParameters()[i].ParameterType) {
						errors.Add($"param {i + 1} type");
					}
				}
			}
			if (mi1.IsStatic != mi2.IsStatic) {
				errors.Add($"static modifier");
			}

			if (errors.Count > 0) {
				TimeLogger.Logger.LogTimeError($"The methods {mi1.DeclaringType.Name}.{mi1.Name} and " +
					$"{mi2.DeclaringType.Name}.{mi2.Name} need to have the same method signature. Fix the " +
					$"following differences: {String.Join(", ", errors)}", LogCategories.Reflect);
			}
			
			return errors.Count == 0;
		}


		[HarmonyDebug]
		private static IEnumerable TranspileRPC_Call(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase originalMethod) {
			if (!methodRedirects.TryGetValue(originalMethod, out MethodInfo targetMethod)){
				//TODO 0 Network - Some error thrown.
			}

			CodeMatcher codeMatcher = new(instructions);

			LOG.TEMPWARNING($"Before emitting delegate " + codeMatcher.InstructionEnumeration().GetFormattedIL());
			if (NetworkServer.active) {
				LOG.TEMPWARNING("Host. RPC network send logic.");
				//Host. Generate and send the RPC request to the clients.
				
				codeMatcher.Advance(1);

				InsertRPC_GenerationCall(codeMatcher, generator, 
					targetMethod.GetParameters(), targetMethod.IsStatic);

			} else if (NetworkClient.active) {
				LOG.TEMPWARNING("Client. Executing next.");
				//Call target method.
				EmitCallTargetMethod(codeMatcher, targetMethod);
			} else {
				TimeLogger.Logger.LogTimeError($"RPC method {originalMethod.Name} was called " +
					$"when no Mirror network component was active.", LogCategories.Network);
			}
			LOG.TEMPWARNING($"After emitting delegate " + codeMatcher.InstructionEnumeration().GetFormattedIL());

			return codeMatcher.InstructionEnumeration();
		}

		private static void InsertRPC_GenerationCall(CodeMatcher codeMatcher, ILGenerator generator, 
				ParameterInfo[] parameters, bool isStatic) {

			///Generate this call:
			///		MakeRPC_Call(new object[] { Arg1, Arg2, Arg3, ... });

			int argCount = parameters.Length;

			if (argCount > 0) {
				//Create array
				codeMatcher
					.InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4, argCount))
					.InsertAndAdvance(new CodeInstruction(OpCodes.Newarr, typeof(object)));

				//If method is not static, skip arg0 with reference to "this"
				int startIndex = isStatic ? 0 : 1;

				//Add arguments into the array
				for (int i = 0; i < argCount; i++) {
					codeMatcher
						.InsertAndAdvance(new CodeInstruction(OpCodes.Dup))
						.InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4, i))
						.InsertAndAdvance(CodeInstructionNew.LoadArgument(i + startIndex));

					Type argType = parameters[i].ParameterType;
					if (argType.IsValueType) {
						codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Box, argType));
					}
					codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Stelem_Ref));
				}
			}

			//Call RPC method generation
			codeMatcher.Insert(Transpilers.EmitDelegate(MakeRPC_Call));
		}

		private static void MakeRPC_Call(object[] args) {
			//TODO 0 Network - Maybe I should just use the original Weaver functions and stop reinventing the wheel?
			//	https://github.com/MirrorNetworking/Mirror/blob/master/Assets/Mirror/Editor/Weaver/Processors/RpcProcessor.cs#L59

			LOG.TEMPWARNING($"MakeRPC_Call - {args.Length} ({args[0]}) params: {string.Join(", ", args)}");
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			foreach (object parameter in args) {
				
				//TODO 0 Network - For each parameter:
				//		writer.WriteString(SuperMarketText);
			}

			//TODO 0 Network - The 1º parameter is just for logging, the 2º param with the hash is the method to call.
			//SendRPCInternal("System.Void NetworkSpawner::RpcUpdateSuperMarketName(System.String)", -700807513, writer, 0, true);
			NetworkWriterPool.Return(writer);
			LOG.TEMPWARNING($"MakeRPC_Call finished");
		}

		private static void EmitCallTargetMethod(CodeMatcher codeMatcher, MethodInfo targetMethod) {
			//Advance just before the ret opcode to leave the original code intact.
			codeMatcher.End().Advance(-1);

			//Load into the stack the arguments and make the call to target method.
			LoadMethodArgIntoStack(codeMatcher, targetMethod);
			codeMatcher.Insert(new CodeInstruction(OpCodes.Call, targetMethod));
		}

		/// <summary>
		/// Loads into the stack the same number of arguments that targetMethod
		/// has, plus an extra one if the method is not static.
		/// </summary>
		private static void LoadMethodArgIntoStack(CodeMatcher codeMatcher, MethodInfo targetMethod) {
			//If not static, we need to also load the arg0 parameter referencing "this".
			int maxArgs = targetMethod.GetParameters().Count() + (targetMethod.IsStatic ? 0 : 1);

			for (int i = 0; i < maxArgs; i++) {
				codeMatcher.InsertAndAdvance(CodeInstructionNew.LoadArgument(i));
			}
		}


		//[MethodImpl(MethodImplOptions.NoInlining)]

		/* Method 1 - "Magic" redirect
			DISCARDED - Hosts need to call this logic too in the onStartClient, so it would need 2 methods anyway.

		[ClientRpcNetwork]
		private void UpdateSupermarketNameOnClient(string marketName) {
			//Name updating logic
		}
		*/


		//Method 2 - Attribute redirect
		[RPC_CallOnClient(typeof(SyncVarNetworkAttribute), nameof(UpdateSupermarketNameOnClient))]
		private void UpdateSupermarketName(string marketName) { }

		private static void UpdateSupermarketNameOnClient() {
			//Name updating logic
		}


		/* Method 3 - Method redirect
			This would need to generate delegates and such at runtime, when the call is made.
			It could be cached but first execution would be slower unless I complicate it even more.

		private void UpdateSupermarketName(string marketName) {
			RPC_CallOnClient(UpdateSupermarketNameOnClient);
		}

		private void UpdateSupermarketNameOnClient(string marketName) {
			//Name updating logic
		}
		*/


		/*
		static NetworkSpawner() {
			//This needs to be done automatically. The method name argument is used as a hashcode and I need
			//	to handle it internally, so better use my own way of generating it from the start.
			Mirror.RemoteCalls.RemoteProcedureCalls.RegisterCommand(typeof(NetworkSpawner), "System.Void NetworkSpawner::CmdSetSupermarketText(System.String)", 
				InvokeUserCode_CmdSetSupermarketText__String, requiresAuthority);
			RemoteProcedureCalls.RegisterRpc(typeof(NetworkSpawner), "System.Void NetworkSpawner::RpcUpdateSuperMarketName(System.String)", 
				InvokeUserCode_RpcUpdateSuperMarketName__String);
		}

		[SyncVar]
		public string SuperMarketName = "Supermarket";

		protected void onStartClient() {
			UpdateSupermarketName(SuperMarketName);
		}

		#region * VANILLA GAME CALL FLOW FROM HOST TO CLIENT *

		[ClientRpc]
		private void RpcUpdateSuperMarketName(string SuperMarketText) {
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteString(SuperMarketText);
			SendRPCInternal("System.Void NetworkSpawner::RpcUpdateSuperMarketName(System.String)", -700807513, writer, 0, true);
			NetworkWriterPool.Return(writer);
		}

		protected static void InvokeUserCode_RpcUpdateSuperMarketName__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection) {
			if (!NetworkClient.active) {
				Debug.LogError("RPC RpcUpdateSuperMarketName called on server.");
			} else {
				((NetworkSpawner)obj).UserCode_RpcUpdateSuperMarketName__String(reader.ReadString());
			}
		}

		protected void UserCode_RpcUpdateSuperMarketName__String(string SuperMarketText) {
			UpdateSupermarketName(SuperMarketText);
		}

		private void UpdateSupermarketName(string SuperMarketText) {
			//All the updating code in the client
		}

		#endregion

		#region * VANILLA GAME CALL FLOW FROM CLIENT TO HOST *

		[Command(requiresAuthority = false)]
		public void CmdSetSupermarketText(string SuperMarketText) {
			NetworkWriterPooled writer = NetworkWriterPool.Get();
			writer.WriteString(SuperMarketText);
			SendCommandInternal("System.Void NetworkSpawner::CmdSetSupermarketText(System.String)", -727745423, writer, 0, false);
			NetworkWriterPool.Return(writer);
		}

		protected static void InvokeUserCode_CmdSetSupermarketText__String(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection) {
			if (!NetworkServer.active) {
				Debug.LogError("Command CmdSetSupermarketText called on client.");
			} else {
				((NetworkSpawner)obj).UserCode_CmdSetSupermarketText__String(reader.ReadString());
			}
		}

		protected void UserCode_CmdSetSupermarketText__String(string SuperMarketText) {
			NetworkSuperMarketName = SuperMarketText;
			RpcUpdateSuperMarketName(SuperMarketText);
		}

		#endregion
		*/




		/* Obsolete automatic instantiation. In the end I decided to have the user set the initial 
			 * constructor values since attribute parameters wew a bit awkward to use for this case.
			 * I might use this as an extra at some point.
			
			private void InstantiateSyncVar(MemberInfoHelper syncVarInfoHelper, ISyncVar syncVarInstance) {
				//Create new instance of this SyncVar<T> using previous DefaultValue as current value.
				Type syncVarType = syncVarInfoHelper.MemberInfoType;
				try {
					syncVarInstance = (ISyncVar)Activator.CreateInstance(syncVarType,
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance, null, 
						[syncVarInstance, this, syncVarInfoHelper.Name], null, null);
					if (syncVarInstance == null) {
						TimeLogger.Logger.LogTimeWarning($"The SyncObject {syncVarInfoHelper.Name} of type {syncVarType} could " +
							$"not be instantiated for an unknown reason.", LogCategories.Network);
						return;
					}
				} catch (Exception ex) {
					TimeLogger.Logger.LogTimeExceptionWithMessage($"The SyncObject {syncVarInfoHelper.Name} of type {syncVarType} " +
							$"could not be instantiated.", ex, LogCategories.Network);
					return;
				}

				var declaringTypeInstance = syncVarInfoHelper.IsStatic ? null : (T)this;
				syncVarInfoHelper.SetValue(declaringTypeInstance, syncVarInstance);
			}
		*/

	}

}
