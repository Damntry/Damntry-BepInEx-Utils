using Damntry.Utils.Logging;

namespace Damntry.UtilsBepInEx.Logging {

	public class LOG {

		public static void DEBUG(string message) {
			BepInExTimeLogger.Logger.LogTimeWarning(message, TimeLoggerBase.LogCategories.TempTest);
		}

	}
}
