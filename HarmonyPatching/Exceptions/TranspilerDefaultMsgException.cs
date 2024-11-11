using System;

namespace Damntry.UtilsBepInEx.HarmonyPatching.Exceptions {

	public class TranspilerDefaultMsgException : Exception {

		private const string defaultErrorText = "Transpiler couldnt perform its changes due to unexpected source code changes.";

		public TranspilerDefaultMsgException() :
			base(composeErrorText()) { }

		public TranspilerDefaultMsgException(string errorDetail)
			: base(composeErrorText(errorDetail)) {
		}

		public TranspilerDefaultMsgException(string errorDetail, Exception inner)
			: base(composeErrorText(errorDetail), inner) {
		}


		private static string composeErrorText(string errorDetail = "") {
			return defaultErrorText + "\n" + errorDetail;
		}

	}
}
