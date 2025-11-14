using System;
using System.Collections.Generic;
using System.Globalization;

namespace CutTheRope.ios
{
    internal static class StringExtensions
    {
        public static int Length(this string value)
        {
            return value?.Length ?? 0;
        }

        public static bool IsEqualToString(this string value, string other)
        {
            return string.Equals(value, other, StringComparison.Ordinal);
        }

        public static NSRange RangeOfString(this string value, string other)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(other))
            {
                return default;
            }

            int index = value.IndexOf(other, StringComparison.Ordinal);
            if (index < 0)
            {
                return default;
            }

            return new NSRange
            {
                location = (uint)index,
                length = (uint)other.Length
            };
        }

        public static char CharacterAtIndex(this string value, int index)
        {
            return value[index];
        }

        public static string SubstringWithRange(this string value, NSRange range)
        {
            return value.Substring((int)range.location, (int)range.length);
        }

        public static string SubstringFromIndex(this string value, int index)
        {
            return value[index..];
        }

        public static string SubstringToIndex(this string value, int index)
        {
            return value[..index];
        }

        public static int IntValue(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                return result;
            }

            int sign = 1;
            int index = 0;
            string trimmed = value.Trim();
            if (trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                sign = -1;
                index = 1;
            }

            int accumulator = 0;
            while (index < trimmed.Length)
            {
                char c = trimmed[index++];
                if (c < '0' || c > '9')
                {
                    continue;
                }

                accumulator = (accumulator * 10) + (c - '0');
            }

            return accumulator * sign;
        }

        public static bool BoolValue(this string value)
        {
            return !string.IsNullOrEmpty(value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static float FloatValue(this string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0f;
            }

            string normalised = value.Trim().Replace(',', '.');
            if (float.TryParse(normalised, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return 0f;
        }

        public static List<string> ComponentsSeparatedByString(this string value, char separator)
        {
            List<string> list = new();
            if (value == null)
            {
                return list;
            }

            foreach (string part in value.Split(separator))
            {
                list.Add(part);
            }

            return list;
        }

        public static bool HasPrefix(this string value, string prefix)
        {
            return value?.StartsWith(prefix, StringComparison.Ordinal) ?? false;
        }

        public static bool HasSuffix(this string value, string suffix)
        {
            return value?.EndsWith(suffix, StringComparison.Ordinal) ?? false;
        }

        public static char[] GetCharacters(this string value)
        {
            int length = value?.Length ?? 0;
            char[] buffer = new char[length + 1];
            if (!string.IsNullOrEmpty(value))
            {
                value.CopyTo(0, buffer, 0, length);
            }

            buffer[length] = '\0';
            return buffer;
        }

        public static void GetCharacters(this string value, char[] destination)
        {
            if (destination == null || destination.Length == 0)
            {
                return;
            }

            int length = Math.Min((value?.Length ?? 0), destination.Length - 1);
            if (!string.IsNullOrEmpty(value) && length > 0)
            {
                value.CopyTo(0, destination, 0, length);
            }

            destination[length] = '\0';
        }

        public static string Copy(this string value)
        {
            return value == null ? null : string.Concat(value);
        }
    }
}
