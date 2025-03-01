using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Damntry.Utils.Logging;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager.Patch;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager.SettingAttributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces;
using HarmonyLib;

namespace Damntry.UtilsBepInEx.Configuration.ConfigurationManager {

	public enum MultiplayerModInstallSide {
		/// <summary>Default value. Does nothing.</summary>
		Unspecified,
		/// <summary>Can be used by host or client, and doesnt affect each other (UI elements for example)</summary>
		Any,
		/// <summary>
		/// Only the host needs to have it installed. If installed on the client, it does nothing.
		/// Generally, this setting functionality must be controlled so only the
		/// host executes it, and the client doesnt.
		/// </summary>
		HostSideOnly,
		/// <summary>
		/// Needs to be installed in the host, and can also be installed in the client. If the client doesnt have 
		/// it installed, the functionality will not work for him, but it still will for the host. If the Host doesnt 
		/// have the mod, and the client does, the feature wont work at all, by design or because of the way it works.
		/// </summary>
		HostAndOptionalClient,
		/// <summary>
		/// Needs to be installed in the host, and should be installed in the client for it to work at its best.
		/// Some degraded functionality might be expected for the client, but it will still work overall with no 
		/// critical disruptions.
		/// Example: A feature to change the speed of non gameplay vital NPCs, whos position is periodically sent
		/// by the host, but smoothed with local speed values by the client.
		/// </summary>
		HostAndSoftClient,
		/// <summary>Needs to be installed in both the host and the client for it to even work in a meaningful way.</summary>
		HostAndHardClient
	}

	public class ConfigManagerController {

		internal const string ConfigMngFullTypeName = "ConfigurationManager.ConfigurationManager";

		private const string InstallSideInitialDescription = "[Multiplayer requirements] " +
			"This setting requires that the mod ";

		private ConfigFile configFile;

		private string currentSectionName;

		/// <summary>
		/// Used for sorting sections by prefixing text on the section headers, since its the only way.
		/// The prefix is only visual and doesnt change section names in the config file, so its ok to
		/// reorder sections afterwards.
		/// </summary>
		private int currentSectionOrder;

		/// <summary>
		/// Order of settings within a section. Higher numbers start showing first, then lower.
		/// </summary>
		private int currentConfigOrder;


		public ConfigManagerController(ConfigFile configFile) {
			this.configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));

			currentSectionOrder = 0;
			currentConfigOrder = int.MaxValue;

			//Check that the ConfigurationManager plugin is installed.
			bool configManagerLoaded = AssemblyUtils.GetTypeFromLoadedAssemblies(ConfigMngFullTypeName) != null;
			if (configManagerLoaded) {
				//Enable patch to get an instance of the ConfigurationManager object in the assembly.
				ConfigurationManagerPatch.PatchSelf();
			}
			ConfigEntryBasePatch.PatchSelf();
		}

		private void SetSection(string sectionName, bool skipSectionOrder = false) {
			//TODO Global 6 - The intended usage o this, was being able to go "back" to previous existing sections to add
			//	new configs, in case they depended on data that only exists later. And after, I would be able to go back
			//	to the latest section while the system automatically kept proper config order.
			//	That would mean I would have to change order ids of all the config options that exist after.
			//	That system was a bit weird to be honest, and ended up deleting all of it, but kept this method for the future.
			//	Think about it some more for a better way to do it.

			//Exit if we are not starting a new section
			if (sectionName == currentSectionName) {
				return;
			}

			if (!skipSectionOrder) {
				currentSectionOrder++;
			}

			currentConfigOrder = int.MaxValue;  //Higher numbers go first. Only affects ordering within a section.

			currentSectionName = sectionName;
		}

		/// <summary>
		/// Refreshes the settings and GUI of the configuration manager plugin.
		/// It makes calls through reflection to unreferenced assembly code, and
		/// thus it may fail at any moment in the future.
		/// In case of exception, logs and returns false to continue program flow.
		/// </summary>
		public static bool RefreshGUI() {
			if (ConfigurationManagerPatch.ConfigMngInstance != null) {
				string refreshMethodName = "BuildSettingList";

				try {
					ReflectionHelper.CallMethod(ConfigurationManagerPatch.ConfigMngInstance, refreshMethodName);
					return true;
				} catch (Exception e) {
					TimeLogger.Logger.LogTimeExceptionWithMessage($"Error when trying to call the method to refresh ConfigurationManager GUI.", e, Utils.Logging.LogCategories.Config);
				}
			}
			return false;
		}

		/// <summary>
		/// Creates a new section with the specified text, meant to be shown as a more visible message.
		/// Below the note there is an empty setting with readonly true value so in Configuration Manager it 
		/// shows as a checkbox, since its the least notorious component.
		/// Description should be left empty for the config file.
		/// The format is as follows in the config file:
		///<code>
		/// [{sectionText}]
		///
		///​ = true
		///</code>
		/// </summary>
		public ConfigEntry<bool> AddSectionNote(string sectionText, string description = null,
			IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool isAdvanced = false) {

			//HACK Global 4 - Even though this key string looks empty, there is a zero-width space character
			//	in it so Bepinex doesnt whine about it having whitespace text. If they add a check for it in
			//	the future, this would fail.
			//TODO Global 5 - I cant add more than one section note within the same section, or the key/section
			//	combination would be duplicated. Warn if it happens by checking it beforehand.
			string key = "​";

			ConfigEntryBasePatch.AddNote(key);

			return AddConfig(sectionText, key, true, description, patchInstanceDependency, MultiplayerModInstallSide.Unspecified, hidden, 
				isAdvanced: isAdvanced, disabled: true, hideDefaultButton: true, acceptableVal: null, skipSectionIncrease: true);
		}

		/// <summary>
		/// Creates a read-only setting with a textbox that shows the message.
		/// The format is as follows in the config file:
		/// <code>
		/// [{sectionName}]
		/// 
		/// ## {description}
		/// {key} = {textboxMessage}
		/// </code>
		/// </summary>
		public ConfigEntry<string> AddQuasiNote(string sectionName, string key, string textboxMessage,
				string description = null, IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool isAdvanced = false) {
			return AddQuasiNote(sectionName, key, textboxMessage, description, patchInstanceDependency, hidden, isAdvanced, skipSectionIncrease: false);
		}

		/// <summary>
		/// This is a read-only setting that only shows in the config file and not on ConfigurationManager.
		/// The format is as follows in the file:
		/// <code>
		/// [{sectionName}]
		/// 
		/// ## {description}
		/// {key} = 
		/// </code>
		/// </summary>
		public ConfigEntry<string> AddGUIHiddenNote(string sectionName, string key,
				string description = null, IConfigPatchDependence patchInstanceDependency = null, bool isAdvanced = false) {
			return AddQuasiNote(sectionName, key, null, description, patchInstanceDependency, hidden: true, isAdvanced, skipSectionIncrease: true);
		}

		/// <param name="skipSectionIncrease">
		/// When the note is meant to always be hidden so its hidden attribute will never change in the future.
		/// This is intended to be used for notes exclusively used for showing in the config file, and not in ConfigurationManager.
		/// It will skip assigning a category number to this section, which means it wont "use up" a number section counter,
		/// so it doesnt look visually as if a section is missing.
		/// For example, if this note were to be added between sections 01 and 02, and skipSectionIncrease is false, the note
		/// would take section 02, and the previous section 02 would become 03. This doesnt happen if skipSectionIncrease is true.
		/// </param>
		private ConfigEntry<string> AddQuasiNote(string sectionName, string key, string textboxMessage,
				string description = null, IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, 
				bool isAdvanced = false, bool skipSectionIncrease = false) {
			if (textboxMessage == null) {
				textboxMessage = "";    //If the default value is null, it outputs the key text twice
			}
			ConfigEntryBasePatch.AddNote(key);

			return AddConfig(sectionName, key, textboxMessage, description, patchInstanceDependency, MultiplayerModInstallSide.Unspecified, 
				hidden, isAdvanced: isAdvanced, disabled: true, hideDefaultButton: true, acceptableVal: null, skipSectionIncrease: skipSectionIncrease);
		}


		public ConfigEntry<T> AddConfig<T>(string sectionName, string key, T defaultValue, string description = null,
				IConfigPatchDependence patchInstanceDependency = null, MultiplayerModInstallSide modInstallSide = MultiplayerModInstallSide.Unspecified, 
				bool hidden = false, bool disabled = false, bool hideDefaultButton = false, bool isAdvanced = false) {
			return AddConfig(sectionName, key, defaultValue, description, patchInstanceDependency, modInstallSide, hidden, disabled, null, false, hideDefaultButton, isAdvanced);
		}

		public ConfigEntry<T> AddConfigWithAcceptableValues<T>(string sectionName, string key, T defaultValue, string description = null,
				IConfigPatchDependence patchInstanceDependency = null, MultiplayerModInstallSide modInstallSide = MultiplayerModInstallSide.Unspecified, 
				bool hidden = false, bool disabled = false, AcceptableValueList<T> acceptableValueList = null, bool hideDefaultButton = false, bool isAdvanced = false)
			where T : IEquatable<T> {

			return AddConfig(sectionName, key, defaultValue, description, patchInstanceDependency, modInstallSide, hidden, disabled, acceptableVal: acceptableValueList, false, hideDefaultButton, isAdvanced);
		}

		public ConfigEntry<T> AddConfigWithAcceptableValues<T>(string sectionName, string key, T defaultValue, string description = null,
				IConfigPatchDependence patchInstanceDependency = null, MultiplayerModInstallSide modInstallSide = MultiplayerModInstallSide.Unspecified, 
				bool hidden = false, bool disabled = false, AcceptableValueRange<T> acceptableValueRange = null,
				bool showRangeAsPercent = false, bool hideDefaultButton = false, bool isAdvanced = false)
			where T : IComparable {

			return AddConfig(sectionName, key, defaultValue, description, patchInstanceDependency, modInstallSide, hidden, disabled, acceptableVal: acceptableValueRange, showRangeAsPercent, hideDefaultButton, isAdvanced);
		}

		private ConfigEntry<T> AddConfig<T>(string sectionName, string key, T defaultValue, string description = null, 
				IConfigPatchDependence patchInstanceDependency = null, MultiplayerModInstallSide modInstallSide = MultiplayerModInstallSide.Unspecified, 
				bool hidden = false, bool disabled = false, AcceptableValueBase acceptableVal = null, bool showRangeAsPercent = false, 
				bool hideDefaultButton = false, bool isAdvanced = false, bool skipSectionIncrease = false) {
			SetSection(sectionName, skipSectionIncrease);

			return AddConfig(key, defaultValue, description, patchInstanceDependency, modInstallSide, hidden, disabled, acceptableVal, showRangeAsPercent, hideDefaultButton, isAdvanced);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T">
		/// By default it can be any of the types supported in <see cref="TomlTypeConverter"/>,
		/// but new ones can be added with <see cref="TomlTypeConverter.AddConverter(Type, TypeConverter)"/>
		/// so its better to check <see cref="TomlTypeConverter.GetSupportedTypes"/> at runtime.
		/// </typeparam>
		/// <param name="key">
		/// Text with a short description of the function for this setting.
		/// </param>
		/// <param name="defaultValue">
		/// Default value when first creating the config file. 
		/// It also allows you to reset the setting to this default value if the "Default" button is not hidden
		/// </param>
		/// <param name="description">
		/// Description that shows as a tooltip when overing over the <paramref name="key"/> text.
		/// Here you can expand on the setting functionality and possible caveats.
		/// </param>
		/// <param name="patchInstanceDependency">
		/// Makes a setting dependent on a patch instance being active. Settings using this functionality are set to hidden, and when
		/// this Instance patching attempt is completed and the patch is successfully active, the setting is shown.
		/// </param>
		/// <param name="modInstallSide">
		/// For a multiplayer game, this var specifies for the user if this mod has to be installed
		/// only on the host, the client, or both, by adding text at the end of the description.
		/// </param>
		/// <param name="hidden">Hides the setting from view. This doesnt affect the config file.</param>
		/// <param name="disabled">Sets the setting so its value cant be changed. This doesnt affect the config file.</param>
		/// <param name="acceptableVal">Acceptable values that the setting can have.</param>
		/// <param name="showRangeAsPercent">Shows the range of acceptable values as a percent. Basically it adds a % after the value.</param>
		/// <param name="hideDefaultButton">Hides the button to reset the setting to default values.</param>
		/// <param name="isAdvanced">Flags the setting as advanced, so it will only be shown if the "Advanced" checkbox option is enabled.</param>
		private ConfigEntry<T> AddConfig<T>(string key, T defaultValue, string description = null, IConfigPatchDependence patchInstanceDependency = null, MultiplayerModInstallSide modInstallSide = MultiplayerModInstallSide.Unspecified, bool hidden = false, bool disabled = false,
				AcceptableValueBase acceptableVal = null, bool showRangeAsPercent = false, bool hideDefaultButton = false, bool isAdvanced = false) {

			//ConfigDescription throws an error if description is null, even though
			//	description is explicitly allowed to be null deeper in the code.
			if (description == null) {
				description = "";
			}

			if (patchInstanceDependency != null) {
				//Hide by default so the depending patch automatically shows it later on, in its own event.
				hidden = true;
			}

			if (modInstallSide != MultiplayerModInstallSide.Unspecified) {
				string modInstallDescrip = GetDescriptionFromInstallSide(modInstallSide);
				if (!description.IsNullOrWhiteSpace()) {
					description += "\n\n";
				}

				description += modInstallDescrip;
			}

			ConfigDescription configDesc = new ConfigDescription(description, acceptableVal,
				new ConfigurationManagerAttributes {
					IsAdvanced = isAdvanced,
					Order = currentConfigOrder--,
					Browsable = !hidden,
					DefaultValue = defaultValue,    //Also shows a button to reset to defaults.
					ShowRangeAsPercent = showRangeAsPercent,
					ReadOnly = disabled,
					HideDefaultButton = hideDefaultButton,
					Category = $"{currentSectionOrder.ToString("D2")}. {currentSectionName}"  //Force minimum of 2 digits, so it starts from "01".
				});
			
			ConfigEntry<T> configEntry = configFile.Bind(currentSectionName, key, defaultValue, configDesc);

			if (patchInstanceDependency != null) {
				patchInstanceDependency.SetSettingPatchDependence(configEntry);
			}

			return configEntry;
		}

		public bool Remove(string section, string key) {
			return configFile.Remove(new ConfigDefinition(section, key));
		}

		public bool Remove(ConfigDefinition key) {
			return configFile.Remove(key);
		}

		public void ClearAllConfigs() {
			configFile.Clear();
		}

		public int Count() => configFile.Count;

		/*	Test to use icons instead of descriptions, with a Legend. Seems like bepinex does not use
			the UTF encoding needed for any of these, and they dont show correctly in the config file.

		    💻⌨☁👑⚡⚪⚫⛔❌✅
			MultiplayerModInstallSide.Any =>					✅
			MultiplayerModInstallSide.HostSideOnly =>			👑
			MultiplayerModInstallSide.HostAndOptionalClient =>	👑⌨⚪
			MultiplayerModInstallSide.HostAndSoftClient =>		👑⌨⚫
			MultiplayerModInstallSide.HostAndHardClient =>		👑⌨⚡
			
			Maybe with letters?
			MultiplayerModInstallSide.Any =>					H/C		Host/Client
			MultiplayerModInstallSide.HostSideOnly =>			H		Host
			MultiplayerModInstallSide.HostAndOptionalClient =>	HCO		HostClientOptional
			MultiplayerModInstallSide.HostAndSoftClient =>		HCS		HostClientShould
			MultiplayerModInstallSide.HostAndHardClient =>		HCR		HostClientRequired
		 */
		private string GetDescriptionFromInstallSide(MultiplayerModInstallSide modInstallSide) {
			return InstallSideInitialDescription +
				modInstallSide switch {
					MultiplayerModInstallSide.Any =>
						"is installed for the player/s that wants its functionality, hosting or not.",
					MultiplayerModInstallSide.HostSideOnly =>
						"is installed in the host. It is not needed in the client.",
					MultiplayerModInstallSide.HostAndOptionalClient =>
						"is installed in the host. Clients can also install it.",
					MultiplayerModInstallSide.HostAndSoftClient =>
						"is installed in the host, and should be installed in the clients.",
					MultiplayerModInstallSide.HostAndHardClient =>
						"is installed in both the host and the clients.",
					_ => ""
				};
		}

		/// <summary>
		/// Sets the value of any of the config attributes present in <see cref="ConfigManagerController"/>
		/// </summary>
		/// <typeparam name="T1">Type of the config value.</typeparam>
		/// <typeparam name="T2">Type of the attribute value.</typeparam>
		/// <param name="section">Section name to search for the config property.</param>
		/// <param name="key">Key name to search for the config property.</param>
		/// <param name="configAttribute">The type of attribute to change.</param>
		/// <param name="attrValue">The value of the attribute.</param>
		/// <returns>True if the attribute was set successfully</returns>
		public bool SetConfigAttribute<T1, T2>(string section, string key, ConfigurationManagerAttributes.ConfigAttributes configAttribute, T2 attrValue) {
			ConfigEntry<T2> configEntry = GetConfigEntry<T2>(section, key);

			return configEntry.SetConfigAttribute(configAttribute, attrValue);
		}

		public R GetConfigAttribute<T, R>(string section, string key, 
				ConfigurationManagerAttributes.ConfigAttributes configAttribute) {

			ConfigEntry<T> configEntry = GetConfigEntry<T>(section, key);
			return configEntry.GetConfigAttribute<T, R>(configAttribute);
		}

		private ConfigEntry<T> GetConfigEntry<T>(string section, string key) {
			bool exists = configFile.TryGetEntry(new ConfigDefinition(section, key), out ConfigEntry<T> configEntry);
			if (!exists || configEntry.Description == null || configEntry.Description.Tags == null) {
				throw new InvalidOperationException($"The config entry with section \"{section}\" and key \"{key}\" could not be found.");
			}

			return configEntry;
		}

	}

	public static class ConfigManagerExtension {

		/// <summary>
		/// Sets the value of any of the config attributes present in <see cref="ConfigManagerController"/>
		/// </summary>
		/// <typeparam name="T1">Type of the config value.</typeparam>
		/// <typeparam name="T2">Type of the attribute value.</typeparam>
		/// <param name="configEntry">A config entry added previously.</param>
		/// <param name="configAttribute">The type of attribute to change.</param>
		/// <param name="attrValue">The value of the attribute.</param>
		/// <returns>True if the attribute was set successfully</returns>
		public static bool SetConfigAttribute<T1, T2>(this ConfigEntry<T1> configEntry, ConfigurationManagerAttributes.ConfigAttributes configAttribute, T2 attrValue) {
			FieldInfo fieldInfoAttribute = GetConfigAttributeFieldInfo(configEntry, configAttribute, out ConfigurationManagerAttributes configManagerAttribInstance);

			if (fieldInfoAttribute == null) {
				return false;
			}

			fieldInfoAttribute.SetValue(configManagerAttribInstance, attrValue);

			return true;
		}

		public static R GetConfigAttribute<T, R>(this ConfigEntry<T> configEntry, ConfigurationManagerAttributes.ConfigAttributes configAttribute) {
			FieldInfo fieldInfoAttribute = GetConfigAttributeFieldInfo(configEntry, configAttribute, 
				out ConfigurationManagerAttributes configManagerAttribInstance);

			if (fieldInfoAttribute == null) {
				throw new InvalidOperationException($"The attribute \"{configAttribute.ToString()}\" could not be found.");
			}

			object value = fieldInfoAttribute.GetValue(configManagerAttribInstance);
			if (value != null && value is not R) {
				throw new InvalidOperationException($"The attribute value is of type \"{value.GetType().FullName}\" " +
					$"and is not compatible with the type \"{typeof(R).FullName}\" passed by parameter.");
			}

			return (R)value;
		}

		private static FieldInfo GetConfigAttributeFieldInfo<T>(ConfigEntry<T> configEntry,
				ConfigurationManagerAttributes.ConfigAttributes configAttribute,
				out ConfigurationManagerAttributes configManagerAttribInstance) {

			configManagerAttribInstance = null;

			//Should be in the 1º index, but search though all tags just in case.
			foreach (object tag in configEntry.Description.Tags) {
				if (tag is ConfigurationManagerAttributes) {
					configManagerAttribInstance = (ConfigurationManagerAttributes)tag;
					break;
				}
			}
			if (configManagerAttribInstance == null) {
				return null;
			}

			FieldInfo fieldInfoAttribute = AccessTools.Field(typeof(ConfigurationManagerAttributes), configAttribute.ToString());
			if (fieldInfoAttribute == null) {
				return null;
			}

			return fieldInfoAttribute;
		}

	}

}
