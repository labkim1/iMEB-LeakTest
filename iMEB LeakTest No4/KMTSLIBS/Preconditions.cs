// -------------------------------------------------------------------------------------
// Alex Wiese
// Copyright (c) 2014
// 
// Assembly:	LiveLogViewer4
// Filename:	Preconditions.cs
// Created:	29/10/2014 8:58 AM
// Author:	Alex Wiese
// 
// -------------------------------------------------------------------------------------

using System;
using TLog.Properties;

namespace TLog
{
    /// <summary>
    ///     Provides static helper methods for validating preconditions.
    /// </summary>
    public static class Preconditions
    {
        public static T CheckNotNull<T>(T value, [InvokerParameterName] string paramName = null)
            where T : class
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName ?? "value");
            }

            return value;
        }

        public static string CheckNotEmptyOrNull(string value, [InvokerParameterName] string paramName = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(paramName ?? "value");
            }

            return value;
        }

        public static void CheckArgumentRange([InvokerParameterName] string paramName, long value, long minInclusive, long maxInclusive)
        {
            if (value < minInclusive || value > maxInclusive)
            {
                throw new ArgumentOutOfRangeException(paramName, string.Format(iMEB_LeakTest_No4.Properties.Resources.Preconditions_CheckArgumentRange, minInclusive, maxInclusive));
            }
        }

        public static void CheckArgumentRange([InvokerParameterName] string paramName, int value, int minInclusive, int maxInclusive)
        {
            if (value < minInclusive || value > maxInclusive)
            {
                throw new ArgumentOutOfRangeException(paramName, string.Format(iMEB_LeakTest_No4.Properties.Resources.Preconditions_CheckArgumentRange, minInclusive, maxInclusive));
            }
        }

        public static void CheckArgument(bool expression, [InvokerParameterName] string parameter, string message)
        {
            if (!expression)
            {
                throw new ArgumentException(message, parameter);
            }
        }

        public static void CheckNotDefault<T>(T value, [InvokerParameterName] string paramName = null)
        {
            if (Equals(value, default(T)))
            {
                throw new ArgumentException(iMEB_LeakTest_No4.Properties.Resources.Preconditions_CheckNotDefault_DefaultValue, paramName ?? "value");
            }
        }
    }
}