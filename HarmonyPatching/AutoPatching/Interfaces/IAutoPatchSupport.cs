namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces {

	/// <summary>
	/// Defines a patch that can automatically patch itself with little manual intervention.
	/// NOTE: To use auto patching, the patch class must inherit <see cref="HybridPatchedInstance{T}"/> or 
	/// <see cref="FullyAutoPatchedInstance{T}"/>, which implements this interface.
	/// Then call <see cref="AutoPatcher.StartAutoPatcher(...)"/> to start the patching process.
	/// </summary>
	public interface IAutoPatchSupport : ISelfPatch {

		/// <summary>Indicates if the auto patcher will try and patch this class instance. Otherwise it is skipped.</summary>
		bool IsAutoPatchEnabled { get; }

		/// <summary>
		/// Raised when a auto patching process finishes, passing the result as parameter.
		/// </summary>
		void RaiseEventOnAutoPatchFinish(AutoPatcher.AutoPatchResult patchResult);

		/// <summary>
		/// If, when the auto patching process fails to apply a patch, it 
		/// should unpatch all previously applied patches of this instance, if any.
		/// Only matters if there is more than one patch in the instance.
		/// </summary>
		bool IsRollbackOnAutoPatchFail { get; }

		/// <summary>
		/// On auto patch failure of this instace, a text with this message will
		/// be shown ingame if the project added support for it through TimeLogger.
		/// Additionally, it will be logged as an error.
		/// </summary>
		string ErrorMessageOnAutoPatchFail { get; }

	}
}
