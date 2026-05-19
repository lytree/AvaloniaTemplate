using System;

namespace Avalonia.Controls.Utils
{
    /// <summary>
    /// Replacement for Avalonia's internal MathUtilities class.
    /// Provides math utility methods needed by TreeDataGrid.
    /// </summary>
    internal static class MathUtilities
    {
        private const double Epsilon = 0.000001;

        public static bool IsZero(double value)
        {
            return Math.Abs(value) < Epsilon;
        }

        public static bool AreClose(double value1, double value2)
        {
            if (value1 == value2)
                return true;
            double delta = value1 - value2;
            return (delta < Epsilon) && (delta > -Epsilon);
        }

        public static bool GreaterThan(double value1, double value2)
        {
            return value1 > value2 && !AreClose(value1, value2);
        }

        public static bool LessThan(double value1, double value2)
        {
            return value1 < value2 && !AreClose(value1, value2);
        }
    }
}
