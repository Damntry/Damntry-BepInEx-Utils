using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;


namespace Damntry.UtilsBepInEx.IL {

	/*	Methods below taken from:
			https://github.com/pardeike/Harmony/blob/master/Harmony/Tools/Extensions.cs/#L511
			https://github.com/pardeike/Harmony/blob/master/Harmony/Util/CodeInstructionExtensions.cs
	
		My Harmony version is older and doesnt have them yet.

		Some I have modified to expand their functionality in, most possibly, aberrant ways.
	*/


	public static class ILExtensionMethods {

		/// <summary>Returns the index targeted by this <c>ldloc</c>, <c>ldloca</c>, or <c>stloc</c></summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <returns>The index it targets</returns>
		/// <seealso cref="CodeInstruction.LoadLocal(int, bool)"/>
		/// <seealso cref="CodeInstruction.StoreLocal(int)"/>
		public static int LocalIndex(this CodeInstruction code) {
			if (code.opcode == OpCodes.Ldloc_0 || code.opcode == OpCodes.Stloc_0) return 0;
			else if (code.opcode == OpCodes.Ldloc_1 || code.opcode == OpCodes.Stloc_1) return 1;
			else if (code.opcode == OpCodes.Ldloc_2 || code.opcode == OpCodes.Stloc_2) return 2;
			else if (code.opcode == OpCodes.Ldloc_3 || code.opcode == OpCodes.Stloc_3) return 3;
			else if (code.opcode == OpCodes.Ldloc_S || code.opcode == OpCodes.Ldloc) return GetLocalOperandIndex(code.operand);
			else if (code.opcode == OpCodes.Stloc_S || code.opcode == OpCodes.Stloc) return GetLocalOperandIndex(code.operand);
			else if (code.opcode == OpCodes.Ldloca_S || code.opcode == OpCodes.Ldloca) return GetLocalOperandIndex(code.operand);
			else throw new ArgumentException("Instruction is not a load or store", nameof(code));
		}

		private static int GetLocalOperandIndex(object operand) {
			if (operand.GetType() == typeof(LocalBuilder)) {
				return ((LocalBuilder)operand).LocalIndex;
			} else {
				return Convert.ToInt32(operand);
			}
		}

		/// <summary>Returns the index targeted by this <c>ldarg</c>, <c>ldarga</c>, or <c>starg</c></summary>
		/// <param name="code">The <see cref="CodeInstruction"/></param>
		/// <returns>The index it targets</returns>
		/// <seealso cref="CodeInstruction.LoadArgument(int, bool)"/>
		/// <seealso cref="CodeInstruction.StoreArgument(int)"/>
		public static int ArgumentIndex(this CodeInstruction code) {
			if (code.opcode == OpCodes.Ldarg_0) return 0;
			else if (code.opcode == OpCodes.Ldarg_1) return 1;
			else if (code.opcode == OpCodes.Ldarg_2) return 2;
			else if (code.opcode == OpCodes.Ldarg_3) return 3;
			else if (code.opcode == OpCodes.Ldarg_S || code.opcode == OpCodes.Ldarg) return Convert.ToInt32(code.operand);
			else if (code.opcode == OpCodes.Starg_S || code.opcode == OpCodes.Starg) return Convert.ToInt32(code.operand);
			else if (code.opcode == OpCodes.Ldarga_S || code.opcode == OpCodes.Ldarga) return Convert.ToInt32(code.operand);
			else throw new ArgumentException("Instruction is not a load or store", nameof(code));
		}


		public static string GetFormattedIL(this IEnumerable<CodeInstruction> instrList) {
			//return instrList.Aggregate("", (combinedText, instr) => combinedText + "\n" + instr.opcode.ToString().PadRight(10) + (instr.operand != null ? instr.operand : ""));
			return instrList.Aggregate("", (combinedText, instr) => combinedText + "\n" + GetFormattedILSingleLine(instr));
		}

		private static string GetFormattedILSingleLine(CodeInstruction instruction) {
			var argStr = FormatArgument(instruction.operand, null); //TODO Global 9 - 2º parameter functionality not finished
			var space = argStr.Length > 0 ? " " : "";
			var opcodeName = instruction.opcode.ToString();
			if (instruction.opcode.FlowControl == FlowControl.Branch || instruction.opcode.FlowControl == FlowControl.Cond_Branch) opcodeName += " =>";
			opcodeName = opcodeName.PadRight(10);
			return string.Format("{0}{1}{2}{3}", CodePos(instruction), opcodeName, space, argStr);
		}

		private static string CodePos(CodeInstruction instruction) {
			//TODO Global 8 - Not implemented yet. Corresponds to the il.ILOffset, for which I have no access, so I
			//	would have to painstakingly state how many bytes each opcode/operand combination takes,
			//	and add it to the offset of the previous instruction.
			//	Generally I think an instruction takes either 1, 2 or 5 bytes.
			//	I think somewhere in the ILParser folder I downloaded there was something already made for this.
			return "";
		}

		private static string FormatArgument(object argument, string extra = null) {
			if (argument is null) return "";
			var type = argument.GetType();

			if (argument is MethodBase method)
				return method.FullDescription() + (extra is not null ? $" {extra}" : "");

			if (argument is FieldInfo field)
				return $"{field.FieldType.FullDescription()} {field.DeclaringType.FullDescription()}::{field.Name}";

			if (type == typeof(Label))
				return $"Label{((Label)argument).GetHashCode()}";

			if (type == typeof(Label[]))
				return $"Labels{string.Join(",", ((Label[])argument).Select(l => l.GetHashCode().ToString()).ToArray())}";

			if (type == typeof(LocalBuilder))
				return $"{((LocalBuilder)argument).LocalIndex} ({((LocalBuilder)argument).LocalType})";

			if (type == typeof(string))
				return argument.ToString().ToLiteral();

			return argument.ToString().Trim();
		}
	}

	public static class CodeInstructionNew {

		// --- LOCALS

		/// <summary>Creates a CodeInstruction loading a local with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index where the local is stored</param>
		/// <param name="useAddress">Use address of local</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.LocalIndex(CodeInstruction)"/>
		public static CodeInstruction LoadLocal(int index, bool useAddress = false) {
			if (useAddress) {
				if (index < 256) return new CodeInstruction(OpCodes.Ldloca_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldloca, index);
			} else {
				if (index == 0) return new CodeInstruction(OpCodes.Ldloc_0);
				else if (index == 1) return new CodeInstruction(OpCodes.Ldloc_1);
				else if (index == 2) return new CodeInstruction(OpCodes.Ldloc_2);
				else if (index == 3) return new CodeInstruction(OpCodes.Ldloc_3);
				else if (index < 256) return new CodeInstruction(OpCodes.Ldloc_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldloc, index);
			}
		}

		/// <summary>Creates a CodeInstruction storing to a local with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index where the local is stored</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.LocalIndex(CodeInstruction)"/>
		public static CodeInstruction StoreLocal(int index) {
			if (index == 0) return new CodeInstruction(OpCodes.Stloc_0);
			else if (index == 1) return new CodeInstruction(OpCodes.Stloc_1);
			else if (index == 2) return new CodeInstruction(OpCodes.Stloc_2);
			else if (index == 3) return new CodeInstruction(OpCodes.Stloc_3);
			else if (index < 256) return new CodeInstruction(OpCodes.Stloc_S, Convert.ToByte(index));
			else return new CodeInstruction(OpCodes.Stloc, index);
		}

		// --- ARGUMENTS

		/// <summary>Creates a CodeInstruction loading an argument with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index of the argument</param>
		/// <param name="useAddress">Use address of argument</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.ArgumentIndex(CodeInstruction)"/>
		public static CodeInstruction LoadArgument(int index, bool useAddress = false) {
			if (useAddress) {
				if (index < 256) return new CodeInstruction(OpCodes.Ldarga_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldarga, index);
			} else {
				if (index == 0) return new CodeInstruction(OpCodes.Ldarg_0);
				else if (index == 1) return new CodeInstruction(OpCodes.Ldarg_1);
				else if (index == 2) return new CodeInstruction(OpCodes.Ldarg_2);
				else if (index == 3) return new CodeInstruction(OpCodes.Ldarg_3);
				else if (index < 256) return new CodeInstruction(OpCodes.Ldarg_S, Convert.ToByte(index));
				else return new CodeInstruction(OpCodes.Ldarg, index);
			}
		}

		/// <summary>Creates a CodeInstruction storing to an argument with the given index, using the shorter forms when possible</summary>
		/// <param name="index">The index of the argument</param>
		/// <returns></returns>
		/// <seealso cref="CodeInstructionExtensions.ArgumentIndex(CodeInstruction)"/>
		public static CodeInstruction StoreArgument(int index) {
			if (index < 256) return new CodeInstruction(OpCodes.Starg_S, Convert.ToByte(index));
			else return new CodeInstruction(OpCodes.Starg, index);
		}

	}

}
