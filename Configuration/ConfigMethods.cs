using BepInEx;
using BepInEx.Bootstrap;
using System.Linq;

namespace Damntry.UtilsBepInEx.Configuration {
    public class ConfigMethods {

        /// <summary>
        /// Returns if the config value from another BepInEx mod exists
        /// in the assembly. If it exists, returns its value in the out parameter.
        /// </summary>
        /// <param name="ModGUID">GUID of the bepinex mod.</param>
        /// <param name="configKey">Key name of the value we are searching for.</param>
        /// <param name="value"></param>
        /// <returns>True if the configuration is currently binded to a ConfigFile in the assembly.</returns>
        public static bool GetExternalConfigValue<T>(string ModGUID, string configKey, out T value) {
            value = default;

            if (Chainloader.PluginInfos.TryGetValue(ModGUID, out PluginInfo ModPluginInfo)) {
                var configEntry = ModPluginInfo.Instance.Config
                    .Where(c => c.Key.Key == configKey)
                    .Select(c => c.Value)
                    .FirstOrDefault();

                if (configEntry != null) {
                    value = (T)configEntry.BoxedValue;
                    return true;
                }
            }

            return false;
        }

    }
}
