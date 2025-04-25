using System;
using Damntry.Utils.Logging;

namespace Damntry.UtilsBepInEx.MirrorNetwork.Attributes {

	[AttributeUsage(AttributeTargets.Method)]

	public class RPC_CallOnClientAttribute : Attribute {

		//TODO 0 Network - This should probably go out, since I only plan on giving support
		//	to methods in the same derived class.
		public Type declaringType;

		public string targetMethodName;

		public Type[] parameters;

		public Type[] generics;


		public RPC_CallOnClientAttribute(Type declaringType, string targetMethodName, Type[] parameters = null) {
			SetTargetMethod(declaringType, targetMethodName, parameters);
		}

		public RPC_CallOnClientAttribute(Type declaringType, string targetMethodName, Type[] parameters, Type[] generics) {
			SetTargetMethod(declaringType, targetMethodName, parameters, generics);
		}


		private void SetTargetMethod(Type declaringType, string targetMethodName, Type[] parameters = null, Type[] generics = null) {
			if (declaringType == null) {
				TimeLogger.Logger.LogTimeError($"Parameter {nameof(declaringType)} is null.", LogCategories.Network);
				return;
			}
			if (string.IsNullOrEmpty(targetMethodName)) {
				TimeLogger.Logger.LogTimeError($"Parameter {nameof(targetMethodName)} is null or empty.",
					LogCategories.Network);
				return;
			}


			this.declaringType = declaringType;
			this.targetMethodName = targetMethodName;
			this.parameters = parameters;
			this.generics = generics;
		}

	}
}
