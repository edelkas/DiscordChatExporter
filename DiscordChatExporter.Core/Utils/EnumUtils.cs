using System;
using System.Collections.Generic;
using System.Globalization;

namespace DiscordChatExporter.Core.Utils;

internal static class EnumUtils
{
    // A [Flags] enum's own ToString collapses to the raw number as soon as one bit is unknown,
    // which would hide the flags that *are* recognized. This keeps both.
    public static IEnumerable<string> GetFlagNames<T>(T flags)
        where T : struct, Enum
    {
        var remaining = Convert.ToInt32(flags, CultureInfo.InvariantCulture);

        foreach (var value in Enum.GetValues<T>())
        {
            var bit = Convert.ToInt32(value, CultureInfo.InvariantCulture);

            if (bit == 0 || (remaining & bit) == 0)
                continue;

            remaining &= ~bit;
            yield return value.ToString();
        }

        if (remaining != 0)
            yield return remaining.ToString(CultureInfo.InvariantCulture);
    }
}
