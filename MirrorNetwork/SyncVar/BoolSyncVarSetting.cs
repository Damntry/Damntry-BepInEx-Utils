using BepInEx.Configuration;
using Mirror;

namespace Damntry.UtilsBepInEx.MirrorNetwork.SyncVar {

    public enum EnableStatus {
        AllDisabled,
        LocallyOnly,
        RemotelyOnly,
        AllEnabled
    }

    public class BoolSyncVarSetting(bool defaultValue, ConfigEntry<bool> configEntry) : 
            SyncVarSetting<bool>(defaultValue, configEntry) {

        public override bool Value {
            get => ConfigEntry.Value && (!IsSynced || NetworkServer.active || base.Value);
            set => SetValue(value, true);
        }

        public EnableStatus Status {
            get {
                if (ConfigEntry.Value) {
                    return Value ? EnableStatus.AllEnabled : EnableStatus.LocallyOnly;
                } else {
                    return Value ? EnableStatus.RemotelyOnly : EnableStatus.AllDisabled;
                }
            }
        }

        public bool IsEnabledLocally => ConfigEntry.Value;

    }
}
