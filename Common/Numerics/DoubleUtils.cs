using System;

namespace SNIBypassGUI.Common.Numerics
{
    public static class DoubleUtil
    {
        private const double Epsilon = 1e-6;

        public static bool AreClose(double value1, double value2)
        {
            if (value1 == value2) return true;
            double diff = Math.Abs(value1 - value2);
            return diff < Epsilon;
        }

        public static bool IsZero(double value) =>
            Math.Abs(value) < Epsilon;

        public static bool LessThan(double value1, double value2) =>
            (value1 < value2) && !AreClose(value1, value2);

        public static bool GreaterThan(double value1, double value2)
            => (value1 > value2) && !AreClose(value1, value2);
        
        public static bool LessThanOrClose(double value1, double value2)
            => (value1 < value2) || AreClose(value1, value2);

        public static bool GreaterThanOrClose(double value1, double value2) 
            => (value1 > value2) || AreClose(value1, value2);
    }
}
