using System;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using Damntry.Utils.Reflection;
using Damntry.UtilsBepInEx.Logging;
using Damntry.UtilsBepInEx.ConfigurationManager.SettingAttributes;
using Damntry.UtilsBepInEx.HarmonyPatching.AutoPatching.Interfaces;

namespace Damntry.UtilsBepInEx.ConfigurationManager
{

    public class ConfigManagerController {

		internal const string ConfigMngFullTypeName = "ConfigurationManager.ConfigurationManager";

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
			if (configFile == null) {
				throw new ArgumentNullException("Argument configFile cannot be null");
			}
			

			this.configFile = configFile;

			currentSectionOrder = 0;
			currentConfigOrder = int.MaxValue;

			//Check that the ConfigurationManager plugin has been installed.
			bool configManagerLoaded = AssemblyUtils.GetTypeFromLoadedAssemblies(ConfigMngFullTypeName) != null;

			if (configManagerLoaded) {
				//Enable patch to get an instance of the ConfigurationManager object in the assembly.
				ConfigurationManagerPatch.Harmony.Value.PatchAll(typeof(ConfigurationManagerPatch));
			}
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
		/// thus it may fail at any moment in the future, but it avoids throwing errors.
		/// </summary>
		public bool RefreshGUI() {
			if (ConfigurationManagerPatch.ConfigMngInstance != null) {
				string refreshMethodName = "BuildSettingList";

				try {
					ReflectionHelper.CallMethod(ConfigurationManagerPatch.ConfigMngInstance, refreshMethodName);
					return true;
				} catch (Exception e) {
					BepInExTimeLogger.Logger.LogTimeExceptionWithMessage($"Error when trying to call the method to refresh ConfigurationManager GUI.", e, Utils.Logging.TimeLoggerBase.LogCategories.Config);
				}
			}
			return false;
		}


		public ConfigEntry<bool> AddSectionNote(string sectionText, string description = null, 
			IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool isAdvanced = false) {

			//HACK Global 4 - Even though this string looks empty, there is a zero-width space character in it so Bepinex doesnt
			//whine about it having whitespace text. If they add a check for it in the future, this would fail.
			string key = "​";

			return AddConfig(sectionText, key, true, description, patchInstanceDependency, hidden, isAdvanced: isAdvanced,
				disabled: true, hideDefaultButton: true, acceptableVal: null, skipSectionIncrease: true);
		}

		/// <summary>
		/// This is just a read-only setting with a textbox that shows the message. The closest
		/// thing I ve found to show a note without patching ConfigurationManager code.
		/// The format is as follows in the config file:
		/// 
		/// [{sectionName}]
		/// ## {description}
		/// # Setting type: String
		/// # Default value: {textboxMessage}
		/// {key} = {textboxMessage}
		/// </summary>
		public ConfigEntry<string> AddQuasiNote(string sectionName, string key, string textboxMessage, 
				string description = null, IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool isAdvanced = false) {
			return AddQuasiNote(sectionName, key, textboxMessage, description, patchInstanceDependency, hidden, isAdvanced, skipSectionIncrease: false);
		}

		/// <summary>AddQuasiNote
		/// This is a read-only setting that only shows in the config file and not on ConfigurationManager.
		/// The format is as follows in the file:
		/// 
		/// [{sectionName}]
		/// ## {description}
		/// # Setting type: String
		/// # Default value:
		/// {key} = 
		/// </summary>
		public ConfigEntry<string> AddGUIHiddenNote(string sectionName, string key,
				string description = null, IConfigPatchDependence patchInstanceDependency = null, bool isAdvanced = false) {
			return AddQuasiNote(sectionName, key, null, description, patchInstanceDependency, hidden: true, isAdvanced, skipSectionIncrease: true);
		}

		/// <param name="skipSectionIncrease">
		/// If the note is meant to always be hidden, and its hidden attribute will never change in the future.
		/// This is intended to be used for notes exclusively used for showing in the config file, and not in ConfigurationManager.
		/// It will skip assigning a category number to this section, which means it wont "use up" a number section counter,
		/// so it doesnt look visually as if a section is missing.
		/// For example, if this note were to be added between sections 01 and 02, and skipSectionIncrease is false, the note
		/// would take section 02, and the previous section 02 would become 03. This doesnt happen if skipSectionIncrease is true.
		/// </param>
		private ConfigEntry<string> AddQuasiNote(string sectionName, string key, string textboxMessage,
				string description = null, IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool isAdvanced = false, bool skipSectionIncrease = false) {
			if (textboxMessage == null) {
				textboxMessage = "";    //If the default value is null, it outputs the key text twice
			}
			return AddConfig(sectionName, key, textboxMessage, description, patchInstanceDependency, hidden, isAdvanced: isAdvanced,
				disabled: true, hideDefaultButton: true, acceptableVal: null, skipSectionIncrease: skipSectionIncrease);
		}

		/// <param name="patchInstanceDependency">Instance of a class that implements or derives from an IConfigPatchDependence interface.</param>
		public ConfigEntry<T> AddConfig<T>(string sectionName, string key, T defaultValue, string description = null,
				IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool disabled = false, bool hideDefaultButton = false, bool isAdvanced = false) {
			return AddConfig(sectionName, key, defaultValue, description, patchInstanceDependency, hidden, disabled, null, false, hideDefaultButton, isAdvanced);
		}

		public ConfigEntry<T> AddConfigWithAcceptableValues<T>(string sectionName, string key, T defaultValue, string description = null,
				IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool disabled = false, AcceptableValueList<T> acceptableValueList = null,
				bool hideDefaultButton = false, bool isAdvanced = false)
			where T : IEquatable<T> {

			return AddConfig(sectionName, key, defaultValue, description, patchInstanceDependency, hidden, disabled, acceptableVal: acceptableValueList, false, hideDefaultButton, isAdvanced);
		}

		public ConfigEntry<T> AddConfigWithAcceptableValues<T>(string sectionName, string key, T defaultValue, string description = null,
				IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool disabled = false, AcceptableValueRange<T> acceptableValueRange = null,
				bool showRangeAsPercent = false, bool hideDefaultButton = false, bool isAdvanced = false)
			where T : IComparable {

			return AddConfig(sectionName, key, defaultValue, description, patchInstanceDependency, hidden, disabled, acceptableVal: acceptableValueRange, showRangeAsPercent, hideDefaultButton, isAdvanced);
		}

		private ConfigEntry<T> AddConfig<T>(string sectionName, string key, T defaultValue, string description = null, IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool disabled = false, 
				AcceptableValueBase acceptableVal = null, bool showRangeAsPercent = false, bool hideDefaultButton = false, bool isAdvanced = false, bool skipSectionIncrease = false) {
			SetSection(sectionName, skipSectionIncrease);

			return AddConfig(key, defaultValue, description, patchInstanceDependency, hidden, disabled, acceptableVal, showRangeAsPercent, hideDefaultButton, isAdvanced);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
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
		/// <param name="hidden">Hides the setting from view. This doesnt affect the config file.</param>
		/// <param name="disabled">Sets the setting so its value cant be changed. This doesnt affect the config file.</param>
		/// <param name="acceptableVal">Acceptable values that the setting can have.</param>
		/// <param name="showRangeAsPercent">Shows the range of acceptable values as a percent. Basically it adds a % after the value.</param>
		/// <param name="hideDefaultButton">Hides the button to reset the setting to default values.</param>
		/// <param name="isAdvanced">Flags the setting as advanced, so it will only be shown if the "Advanced" checkbox option is enabled.</param>
		private ConfigEntry<T> AddConfig<T>(string key, T defaultValue, string description = null, IConfigPatchDependence patchInstanceDependency = null, bool hidden = false, bool disabled = false,
				AcceptableValueBase acceptableVal = null, bool showRangeAsPercent = false, bool hideDefaultButton = false, bool isAdvanced = false) {

			//ConfigDescription through an error if description is null, even though description is explicitly allowed to be null deeper into the code.
			if (description == null) {
				description = "";
			}

			if (patchInstanceDependency != null) {
				hidden = true;	//If it depends on a patch, hide by default so the patch unhides it later on in its own event.
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
				patchInstanceDependency.SetSettingPatchDependence(this, configEntry);
			}

			return configEntry;
		}

		/// <summary>
		/// Sets the value of any of the config attributes present in <see cref="ConfigManagerController"/>
		/// </summary>
		/// <typeparam name="T1">Type of the config value.</typeparam>
		/// <typeparam name="T2">Type of the attribute value.</typeparam>
		/// <param name="configEntry">A config entry added previously.</param>
		/// <param name="configAttribute">The type of attribute to change.</param>
		/// <param name="attrValue">The value of the attribute.</param>
		/// <returns>True if the attribute was set successfully</returns>
		public bool SetConfigAttribute<T1, T2>(ConfigEntry<T1> configEntry, ConfigurationManagerAttributes.ConfigAttributes configAttribute, T2 attrValue) {
			return SetConfigAttribute<T1, T2>(configEntry.Definition.Section, configEntry.Definition.Key, configAttribute, attrValue);
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
			FieldInfo fieldInfoAttribute = GetConfigAttributeFieldInfo<T1>(section, key, configAttribute, out ConfigurationManagerAttributes configManagerAttribInstance);

			if (fieldInfoAttribute == null) {
				return false;
			}

			fieldInfoAttribute.SetValue(configManagerAttribInstance, attrValue);

			return true;
		}

		public R GetConfigAttribute<T, R>(ConfigEntry<T> configEntry, ConfigurationManagerAttributes.ConfigAttributes configAttribute) {
			return GetConfigAttribute<T, R>(configEntry.Definition.Section, configEntry.Definition.Key, configAttribute);
		}

		public R GetConfigAttribute<T, R>(string section, string key, ConfigurationManagerAttributes.ConfigAttributes configAttribute) {
			FieldInfo fieldInfoAttribute = GetConfigAttributeFieldInfo<T>(section, key, configAttribute, out ConfigurationManagerAttributes configManagerAttribInstance);

			if (fieldInfoAttribute == null) {
				throw new InvalidOperationException($"The attribute \"{configAttribute.ToString()}\" could not be found.");
			}

			object value = fieldInfoAttribute.GetValue(configManagerAttribInstance);
			if (value != null && !(value is R)) {
				throw new InvalidOperationException($"The attribute value is of type \"{value.GetType().FullName}\" and is not compatible with the type \"{typeof(R).FullName}\" passed by parameter.");
			}

			return (R)value;
		}

		private FieldInfo GetConfigAttributeFieldInfo<T>(string section, string key, ConfigurationManagerAttributes.ConfigAttributes configAttribute, out ConfigurationManagerAttributes configManagerAttribInstance) {
			configManagerAttribInstance = null;

			bool exists = configFile.TryGetEntry(new ConfigDefinition(section, key), out ConfigEntry<T> configEntry);
			if (!exists || configEntry.Description == null || configEntry.Description.Tags == null) {
				return null;
			}

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
