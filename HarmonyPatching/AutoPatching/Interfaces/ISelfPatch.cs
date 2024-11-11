using System;


namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces {

	/// <summary>
	/// Defines a patch class that contains a single functionality that can be patched and unpatched in its entirery.
	/// </summary>
	public interface ISelfPatch {

		void PatchInstance();

		void UnpatchInstance();

		/// <summary>
		/// True if the patch is currently working. This must always hold the correct value 
		///		regardless of if it has been patched with the autopatcher, or manually.
		/// </summary>
		bool IsPatchActive { get; }

		/// <summary>
		/// Raised after patching has been attempted for this instance, regardless of the result. 
		/// The bool parameter indicates if the patch was successfully activated.
		/// </summary>
		event Action<bool> OnPatchFinished;

	}
}
