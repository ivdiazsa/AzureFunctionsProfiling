using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionsColdStartProfileAnalyzer
{
    public static class StringExtensions
    {
        const int MaxStringSizeForEtwColumn = 2000;

        public static string WithMaxLength(this string value)
        {
            if (value == null)
            {
                return null;
            }

            return value.Substring(0, Math.Min(value.Length, MaxStringSizeForEtwColumn));
        }
    }
}
