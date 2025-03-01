using System;
using Damntry.Utils.Logging;
using BepInExLog = BepInEx.Logging;

namespace Damntry.UtilsBepInEx.Logging {

	public sealed class BepInExTimeLogger : TimeLogger {


		private static BepInExLog.ManualLogSource bepinexLogger;


		protected override void InitializeLogger(params object[] args) {
			if (args == null) {
				throw new ArgumentNullException("args");
			}
			if (args.Length == 0 || args[0] is not string) {
				throw new ArgumentException("The argument is empty or its first index is not a string");
			}

			bepinexLogger = BepInExLog.Logger.CreateLogSource((string)args[0]);
		}


		protected override void LogMessage(string message, LogTier logLevel) {
			bepinexLogger.Log(convertLogLevel(logLevel), message);
		}

		private BepInExLog.LogLevel convertLogLevel(LogTier logLevel) {
			//All enum bits are the same except "All"
			if (logLevel == LogTier.All) {
				return BepInExLog.LogLevel.All;
			} else {
				return (BepInExLog.LogLevel)logLevel;
			}
		}

	}
}
