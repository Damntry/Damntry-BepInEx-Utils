using Damntry.Utils.Logging;

namespace Damntry.UtilsBepInEx.Logging {

	public class LOG {

		public static void DEBUG(string message, bool onlyIfTrue = true) {
			if (onlyIfTrue) {
				TimeLogger.Logger.LogTimeWarning(message, TimeLogger.LogCategories.TempTest);
			}
		}

	}
}
