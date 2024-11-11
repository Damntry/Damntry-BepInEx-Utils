using System;


namespace Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Attributes {

	/// <summary>
	/// It will ignore any patches that exist on that class, but keeps searching through nested classes to patch.
	/// Harmony patching ignores this attribute.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class AutoPatchIgnoreClass : AutoPatchAttribute {

		public AutoPatchIgnoreClass() { }

	}

	/// <summary>
	/// Annotation to tell the Auto Patcher to ignore a class and any of its nested classes.
	/// Used in <see cref="HarmonyInstancePatcher{T}"/>. 
	/// Harmony patching ignores this attribute.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class AutoPatchIgnoreClassAndNested : AutoPatchAttribute {

		public AutoPatchIgnoreClassAndNested() { }

	}

	/// <summary>
	/// Common attribute of all Auto patch attributes.
	/// </summary>
	public abstract class AutoPatchAttribute : Attribute {

		protected AutoPatchAttribute() { }

	}
}
