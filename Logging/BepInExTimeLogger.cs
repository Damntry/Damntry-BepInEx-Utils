using System;
using BepInExLog = BepInEx.Logging;
using Damntry.Utils.Logging;

namespace Damntry.UtilsBepInEx.Logging {

	public sealed class BepInExTimeLogger : TimeLoggerBase {


		private static BepInExLog.ManualLogSource bepinexLogger;

		public static BepInExTimeLogger Logger {
			get {
				return (BepInExTimeLogger)GetLogInstance(nameof(BepInExTimeLogger));
			}
		}

		private BepInExTimeLogger() { }

		public static void InitializeTimeLogger(string sourceNamePrefix, bool debugEnabled = false) {
			Lazy<TimeLoggerBase> instance = initLogger(sourceNamePrefix);

			TimeLoggerBase.InitializeTimeLogger(instance, debugEnabled);
		}

		public static void InitializeTimeLoggerWithGameNotifications(string sourceNamePrefix, Action<string, LogTier> notificationAction,
				string notificationMsgPrefix, bool debugEnabled = false) {
			Lazy<TimeLoggerBase> instance = initLogger(sourceNamePrefix);

			TimeLoggerBase.InitializeTimeLoggerWithGameNotifications(instance, notificationAction, notificationMsgPrefix, debugEnabled);
		}

		private static Lazy<TimeLoggerBase> initLogger(string sourceNamePrefix) {
			bepinexLogger = BepInExLog.Logger.CreateLogSource(sourceNamePrefix);
			return new Lazy<TimeLoggerBase>(() => new BepInExTimeLogger());
		}

		protected override void Log(string message, TimeLoggerBase.LogTier logLevel) {
			bepinexLogger.Log(convertLogLevel(logLevel), message);
		}

		private BepInExLog.LogLevel convertLogLevel(TimeLoggerBase.LogTier logLevel) {
			//All enum bits are the same except "All"
			if (logLevel == LogTier.All) {
				return BepInExLog.LogLevel.All;
			} else {
				return (BepInExLog.LogLevel)logLevel;
			}
		}

	}
}
