using System;
using System.Linq;
using BepInEx.Configuration;
using Damntry.Utils.ExtensionMethods;

namespace Damntry.UtilsBepInEx.Configuration {

	public static class BepinexConfigExtensions {

		public static T GetMaxValue<T>(this AcceptableValueBase acceptableVal) where T : IComparable, IEquatable<T> {
			if (acceptableVal is AcceptableValueRange<T>) {
				return ((AcceptableValueRange<T>)acceptableVal).GetMaxNumericValue();
			} else if (acceptableVal is AcceptableValueList<T>) {
				return ((AcceptableValueList<T>)acceptableVal).GetMaxNumericValue();
			}
			return default(T);
		}

		public static T GetMaxNumericValue<T>(this AcceptableValueRange<T> acceptableVal) where T : IComparable {
			return acceptableVal.MaxValue;
		}

		public static T GetMaxNumericValue<T>(this AcceptableValueList<T> acceptableVal) where T : IEquatable<T>, IComparable {
			if (!typeof(T).IsNumeric()) {
				throw new InvalidOperationException($"Only numeric types are allowed, but received a {typeof(T).FullName}");
			}

			return acceptableVal.AcceptableValues.OrderBy(x => x).Last();
		}

	}

}
