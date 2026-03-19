using BepInEx.Configuration;
using Damntry.Utils.Logging;
using Damntry.UtilsBepInEx.Configuration.ConfigurationManager;
using Damntry.UtilsUnity.Components.InputManagement;
using Damntry.UtilsUnity.Components.InputManagement.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Damntry.Utils.Events.EventMethods;

namespace Damntry.UtilsBepInEx.Components {

    /// <summary>
    /// Keeps the same behaviour of InputDetection, but adds support
    /// to automatically handle BepInEx hotkey settings.
    /// </summary>
    public class InputBepInEx : InputDetection {

        public static new InputBepInEx Instance => _instance ??= new InputBepInEx();

        private static InputBepInEx _instance;


        private readonly Dictionary<string, ConfigEntry<KeyboardShortcut>> configKeyPresses;

        public InputBepInEx() : base(typeof(InputBepInEx), OnValidationError.Rollback, false, null) {
            configKeyPresses = new();
        }

        protected InputBepInEx(Type callingKeypressClass, OnValidationError onError, 
                bool restrictAllModifiers, params List<KeyBind> restrictedKeyBinds) 
                : base(callingKeypressClass, onError, restrictAllModifiers, restrictedKeyBinds) {

            configKeyPresses = new();
        }


        protected override void HotkeyValidationError(string hotkeyName, string message, KeyBind keyBind) {
            if (IsConfigManagedKeyPress(hotkeyName, out var configEntry)) {
                //TODO 2 - Rework this so I dont have to use this bypass method, neither
                //  some kind of "supression" static bool property.

                //Update value in the Bepinex Configuration Manager entry.
                //  We avoid triggering the SettingChanged event to avoid an infinite recursive loop.
                //  Internally, the correct value was already set, so only BepInEx needs to be updated.
                ExecuteBypassingEvent(
                    () => {
                        configEntry.Value = new KeyboardShortcut(keyBind.KeyCode, keyBind.Modifiers);
                    }, 
                    new EventData(configEntry.GetType(), configEntry, nameof(configEntry.SettingChanged))
                );

                ConfigManagerController.RefreshGUI();
            }

            base.HotkeyValidationError(hotkeyName, message, keyBind);
        }

        /// <summary>
        /// Adds a new automatically managed hotkey that is bound to the 
        /// value of the BepInEx ConfigEntry passed by parameter.
        /// </summary>
        /// <param name="hotkeyConfig">BepInEx hotkey config object</param>
        /// <param name="inputState">Type of keypress action</param>
        /// <param name="action">Action to execute on keypress</param>
        /// <param name="hotkeyContext">Activation condition that must pass before executing the keypress action</param>
        /// <param name="action">Name of the group the hotkey belongs to. Null if its not part of a group</param>
        public void AddHotkeyFromConfig(ConfigEntry<KeyboardShortcut> hotkeyConfig, InputState inputState, 
                HotkeyContext hotkeyContext, Action action) {

            string hotkeyName = GetHotkeyNameFromConfig(hotkeyConfig, inputState);

            AddHotkeyBepInExInternal(hotkeyName, hotkeyConfig.Value.MainKey, hotkeyConfig.Value.Modifiers.ToArray(), inputState, 
                hotkeyContext, DefaultKeyPressCooldown, action, groupName: null, hotkeyConfig);

            hotkeyConfig.SettingChanged += (ev, e) => {
                ChangeConfigHotkey(hotkeyConfig, hotkeyName, inputState, hotkeyContext, 
                    DefaultKeyPressCooldown, action);
            };
        }

        public void AddHotkeyFromConfig(ConfigEntry<KeyboardShortcut> hotkeyConfig, InputState inputState, 
                HotkeyContext hotkeyContext, int cooldownMillis, Action action) {

            string hotkeyName = GetHotkeyNameFromConfig(hotkeyConfig, inputState);

            AddHotkeyBepInExInternal(hotkeyName, hotkeyConfig.Value.MainKey, hotkeyConfig.Value.Modifiers.ToArray(), inputState,
                hotkeyContext, cooldownMillis, action, groupName: null, hotkeyConfig);

            hotkeyConfig.SettingChanged += (ev, e) => {
                ChangeConfigHotkey(hotkeyConfig, hotkeyName, inputState, 
                    hotkeyContext, cooldownMillis, action);
            };
        }

        private string GetHotkeyNameFromConfig(ConfigEntry<KeyboardShortcut> hotkeyConfig, InputState inputState) =>
            hotkeyConfig.Definition.Key + "-" + inputState.ToString();

        private void ChangeConfigHotkey(ConfigEntry<KeyboardShortcut> hotkeyConfig, string hotkeyName, 
                InputState inputState, HotkeyContext hotkeyContext, int cooldownMillis, Action action) {

            bool keyAlreadyExists = KeyPressActions.TryGetValue(hotkeyName, out InputData keyData);

            KeyboardShortcut keyShortcut = hotkeyConfig.Value;
            KeyCode[] modifiers = keyShortcut.Modifiers.ToArray();
            if (keyAlreadyExists) {
                ChangeHotkeyBepInExInternal(hotkeyName, keyShortcut.MainKey, modifiers, fromConfig: true);                
            } else {
                AddHotkeyBepInExInternal(hotkeyName, keyShortcut.MainKey, modifiers, inputState, hotkeyContext,
                    cooldownMillis, action, groupName: null, hotkeyConfig);
            }
        }


        public override bool TryAddHotkey(string hotkeyName, KeyCode keyCode, 
                KeyCode[] modifiers, InputState inputState, HotkeyContext hotkeyContext, 
                Action action) {
            return AddHotkeyBepInExInternal(hotkeyName, keyCode, modifiers, inputState, hotkeyContext, 
                DefaultKeyPressCooldown, action, groupName: null, hotkeyConfig: null);
        }

        public override bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, 
                InputState inputState, HotkeyContext hotkeyContext, int cooldownMillis, Action action) {
            return AddHotkeyBepInExInternal(hotkeyName, keyCode, modifiers, inputState, hotkeyContext, 
                cooldownMillis, action, groupName: null, hotkeyConfig: null);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, 
                InputState inputState, HotkeyContext hotkeyContext, Action action, string groupName) {
            return AddHotkeyBepInExInternal(hotkeyName, keyCode, modifiers, inputState, hotkeyContext,
                DefaultKeyPressCooldown, action, groupName, hotkeyConfig: null);
        }

        public bool TryAddHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers,
                InputState inputState, HotkeyContext hotkeyContext, int cooldownMillis, Action action,
                string groupName) {
            return AddHotkeyBepInExInternal(hotkeyName, keyCode, modifiers, inputState, hotkeyContext,
                cooldownMillis, action, groupName, hotkeyConfig: null);
        }

        protected bool AddHotkeyBepInExInternal(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, 
                InputState inputState, HotkeyContext hotkeyContext, int cooldownMillis, 
                Action action, string groupName, ConfigEntry<KeyboardShortcut> hotkeyConfig = null) {

            HotkeyAddResult res = AddHotkeyInternal(hotkeyName, keyCode, modifiers, 
                inputState, hotkeyContext, cooldownMillis, action, groupName);

            if (hotkeyConfig != null && res != HotkeyAddResult.Failure) {
                if (hotkeyConfig == null) {
                    throw new InvalidOperationException("Tried to add config hotkey ");
                }
                configKeyPresses.Add(hotkeyName, hotkeyConfig);
            }

            return res == HotkeyAddResult.Sucess;
        }

        public override bool ChangeHotkey(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers) {
            return ChangeHotkeyBepInExInternal(hotkeyName, keyCode, modifiers, fromConfig: false);
        }

        protected bool ChangeHotkeyBepInExInternal(string hotkeyName, KeyCode keyCode, KeyCode[] modifiers, bool fromConfig) {
            bool IsConfigManaged = IsConfigManagedKeyPress(hotkeyName, out _);
            if (IsConfigManaged && !fromConfig) {
                LogConfigManagedWarningMessage(hotkeyName);
                return false;
            }

            return ChangeHotkeyInternal(hotkeyName, keyCode, modifiers);
        }

        public override bool RemoveHotkey(string hotkeyName) {
            return RemoveHotkeyBepInExInternal(hotkeyName, groupName: null, fromConfig: false);
        }

        public override bool RemoveHotkeyGroup(string groupName) {
            return RemoveHotkeyBepInExInternal(hotkeyName: null, groupName, fromConfig: false);
        }

        protected bool RemoveHotkeyBepInExInternal(string hotkeyName, string groupName, bool fromConfig) {
            if (!string.IsNullOrEmpty(hotkeyName)) {
                return RemoveHotkeyFromName(hotkeyName, fromConfig);
            } else if (!string.IsNullOrEmpty(groupName)) {
                if (NamedGroupsCache.TryGetValue(groupName, out var group)) {

                    bool success = true;
                    //Iterate a copy of group so we dont touch the original enumeration
                    //  object while being deleted from NamedGroupsCache.
                    foreach (string hotkeyFromGroup in group.ToArray()) {
                        success &= RemoveHotkeyFromName(hotkeyFromGroup, fromConfig);
                    }

                    return success;
                }

                return false;
            }

            throw new ArgumentException($"Both {nameof(hotkeyName)} and {nameof(groupName)} cant be empty.");
        }

        private bool RemoveHotkeyFromName(string hotkeyName, bool fromConfig) {
            bool IsConfigManaged = IsConfigManagedKeyPress(hotkeyName, out _);

            if (IsConfigManaged && !fromConfig) {
                LogConfigManagedWarningMessage(hotkeyName);
                return false;
            }

            bool success = RemoveHotkeysInternal(hotkeyName, groupName : null);
            if (IsConfigManaged && success) {
                configKeyPresses.Remove(hotkeyName);
            }

            return success;
        }

        public bool IsConfigManagedKeyPress(string hotkeyName, out ConfigEntry<KeyboardShortcut> configEntry) {
            return configKeyPresses.TryGetValue(hotkeyName, out configEntry);
        }

        private void LogConfigManagedWarningMessage(string hotkeyName) {
            TimeLogger.Logger.LogWarning($"The hotkey '{hotkeyName}' is being automatically managed " +
                $"from the BepInEx settings and cant be manually handled.", LogCategories.KeyMouse);
        }

    }
}
