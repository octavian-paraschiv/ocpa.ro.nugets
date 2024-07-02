using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThorusCommon.IO
{
    public class Range<T> where T : IFormattable, IConvertible, IComparable<T>, IEquatable<T>
    {
        public T Min { get; set; }
        public T Max { get; set; }

        public T Delta
        {
            get
            {
                double fMin = (double)Convert.ChangeType(Min, typeof(double));
                double fMax = (double)Convert.ChangeType(Max, typeof(double));

                return (T)Convert.ChangeType((fMax - fMin), typeof(T));
            }
        }

        public Range(T min, T max)
        {
            if (max.CompareTo(min) < 0)
                throw new ArgumentException("Range Minimum cannot be greater than Maximum.");

            this.Min = min;
            this.Max = max;
        }
    }
}
