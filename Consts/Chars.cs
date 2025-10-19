using System;
using System.Linq;

namespace SNIBypassGUI.Consts
{
    public static class Chars
    {
        private static readonly Lazy<char[]> _whitespaces = new(() =>
            [.. Enumerable.Range(0, char.MaxValue + 1)
            .Select(i => (char)i).Where(char.IsWhiteSpace)]);

        public static char[] Whitespaces => _whitespaces.Value;
    }
}
