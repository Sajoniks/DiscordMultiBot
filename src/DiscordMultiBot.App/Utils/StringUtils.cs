using System.Collections;
using System.Text;

namespace DiscordMultiBot.App.Utils;

public static class StringUtils
{
    private static readonly string[] NumEmojis;

    
    static StringUtils()
    {
        NumEmojis = Enumerable.Range(0, 10)
            .Select(x => new char[]
            {
                (char)(0x0030 + x),
                (char)(0xFE0F),
                (char)(0x20E3)
            })
            .Select(x => new string(x))
            .ToArray();
    }

    public static string ConvertUInt32ToEmoji(uint num)
    {
        if (num < 10)
        {
            return NumEmojis[num];
        }
        else
        {
            var sb = new Stack<string>();
            while (num > 0)
            {
                uint x = num % 10;
                num /= 10;
                string emoji = NumEmojis[x];

                sb.Push(emoji);
            }

            return String.Join(Char.MinValue, sb);
        }
    }
}