using System;

namespace Damntry.UtilsBepInEx.HarmonyPatching.Exceptions {
	public class TypeNotFoundInAssemblyException : Exception {

		public TypeNotFoundInAssemblyException() :
			base() { }

		public TypeNotFoundInAssemblyException(string errorDetail)
			: base(errorDetail) { }

		public TypeNotFoundInAssemblyException(string errorDetail, Exception inner)
			: base(errorDetail, inner) { }


	}

}
