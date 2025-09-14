namespace SNIBypassGUI.Interop.Pcre
{
    /// <summary>
    /// 表示单个捕获组。
    /// </summary>
    public readonly struct PcreGroup
    {
        public int Index { get; }
        public int Start { get; }
        public int End { get; }
        public int Length => End - Start;
        public string Value { get; }

        internal PcreGroup(int index, byte[] subjectBytes, int start, int end)
        {
            Index = index;
            Start = start;
            End = end;
            Value = PcreRegex.Utf8NoBom.GetString(subjectBytes, start, end - start);
        }
    }
}
