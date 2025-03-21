using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using Damntry.Utils.Logging;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.Configuration.ConfigurationManager.Patch {
	internal class ConfigEntryBasePatch {

		static ConfigEntryBasePatch() {
			keyNotes = new();
		}

		private static readonly HashSet<string> keyNotes;

		internal static Lazy<Harmony> Harmony { get; } = 
			new Lazy<Harmony>(() => new Harmony(typeof(ConfigEntryBasePatch).FullName));


		internal static void PatchSelf() {
			try {
				Harmony.Value.PatchAll(typeof(ConfigEntryBasePatch));
			} catch (Exception ex) {
				TimeLogger.Logger.LogTimeExceptionWithMessage($"Error while trying to apply " +
					$"patch in type {nameof(ConfigEntryBasePatch)}", ex, LogCategories.Config);
			}			
		}

		public static void AddNote(string configKey) {
			keyNotes.Add(configKey);
		}

		//Checks if the config being written to the config file is a note, to skip writting some lines.
		[HarmonyPatch(typeof(ConfigEntryBase), nameof(ConfigEntryBase.WriteDescription))]
		[HarmonyPrefix]
		public static bool WriteDescriptionPatch(ConfigEntryBase __instance, StreamWriter writer) {
			if (keyNotes.Contains(__instance.Definition.Key)) {
				if (!string.IsNullOrEmpty(__instance.Description.Description)) {
					writer.WriteLine("## " + __instance.Description.Description.Replace("\n", "\n## "));
				}
				//Skip rest of lines (Setting type, Default value, AcceptableValues).

				return false;
			}

			return true;
		}

	}
}
