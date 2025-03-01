using System;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.HarmonyPatching.Helpers {

	//TODO Global 5 - I could make this a Singleton and, instead of using a dictionary to save the args and then having 
	//	to refer the argument you want with a constant or something like that, see if I can use templating
	//	to add new attributes right here in this class. Maybe there is a way of having a cs class with snippets of
	//	templating? Otherwise make this class abstract, and a new template file will set a new class to derive it
	//	and create the arguments and instance so it generates the class that will be used to refer to the arguments.
	//
	//	Also make sure that the templating auto generates when building, and not just when saving. I remember seeing that somewhere.

	/// <summary>
	/// Helps passing new arguments to a transpiled class.
	/// This is for when you have a method A() that calls B(), but B is a transpiled method and you need
	/// to pass new arguments.
	/// You cant just add new arguments to a method through harmony, or IL load the arguments from 
	/// the receiving method. The method signature must be redefined, but I havent found a way that
	/// also works with Harmony.
	/// So instead, I have this class that represents an argument, and eases handling the IL side, but 
	/// basically its to be used as a global static field with IL support.
	/// </summary>
	public class ArgumentHelper<T> {

		public T Value { get; set; }

		/// <summary>
		/// Instruction to load into the stack this own instance, to later use its Getter.. or Setter.. methods.
		/// </summary>
		public CodeInstruction LoadFieldArgHelper_IL { get; private set; }

		/// <summary>
		/// Method call to the property getter of this argument value.
		/// Expected stack (NOT PROVIDED): Consumes a field in the stack holding this ArgumentHelper instance.
		/// Then puts its value in the stack.
		/// </summary>
		public CodeInstruction GetterValue_IL { get; private set; }

		/// <summary>
		/// Method call to the property setter of this argument value.
		/// Expected stack (NOT PROVIDED): Consumes the current value on the stack to set the value, and then consumes
		/// a field in the stack holding this ArgumentHelper instance.
		/// </summary>
		public CodeInstruction SetterValue_IL { get; private set; }


		/// <param name="declaringClassType">The class where this ArgumentHelper is being declared.</param>
		/// <param name="instanceName">Name of the instance of this ArgumentHelper.</param>
		/// <param name="argumentValue">Value asigned</param>
		public ArgumentHelper(Type declaringClassType, string instanceName, T argumentValue) {
			Value = argumentValue;

			//Create instruction to load this instance into the stack.
			FieldInfo OwnInstanceFieldInfo = AccessTools.Field(declaringClassType, instanceName);
			if (OwnInstanceFieldInfo == null) {
				throw new InvalidOperationException($"No field was found in the type {declaringClassType} with name {instanceName}.");
			}

			LoadFieldArgHelper_IL = new CodeInstruction(OpCodes.Ldsfld, OwnInstanceFieldInfo);
			if (!OwnInstanceFieldInfo.IsStatic) {
				LoadFieldArgHelper_IL.opcode = OpCodes.Ldfld;
			}

			//Create instructions to call the get/set of the value.
			PropertyInfo argumentPropInfo = AccessTools.Property(typeof(ArgumentHelper<T>), nameof(Value));

			GetterValue_IL = new CodeInstruction(OpCodes.Callvirt, argumentPropInfo.GetGetMethod());
			SetterValue_IL = new CodeInstruction(OpCodes.Callvirt, argumentPropInfo.GetSetMethod());
		}

	}
}
