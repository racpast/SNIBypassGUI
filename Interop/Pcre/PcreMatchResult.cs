using System.Collections.Generic;

namespace SNIBypassGUI.Interop.Pcre
{
    /// <summary>
    /// 表示一次 PCRE 匹配的结果。
    /// </summary>
    public class PcreMatchResult
    {
        private readonly int _matchCount;
        private readonly byte[] _subjectBytes;
        private readonly int[] _ovector;

        public bool Success => _matchCount >= 0;
        public IReadOnlyList<PcreGroup> Groups { get; }

        internal PcreMatchResult(int matchCount, byte[] subjectBytes, int[] ovector)
        {
            _matchCount = matchCount;
            _subjectBytes = subjectBytes;
            _ovector = ovector;

            var groups = new List<PcreGroup>();
            int groupsToRead = 0;
            if (matchCount > 0) groupsToRead = matchCount;
            else if (matchCount == 0) groupsToRead = ovector.Length / 3;

            for (int i = 0; i < groupsToRead; i++)
                if (_ovector[i * 2] >= 0)
                    groups.Add(new PcreGroup(i, _subjectBytes, _ovector[i * 2], _ovector[i * 2 + 1]));
            Groups = groups.AsReadOnly();
        }
    }
}
