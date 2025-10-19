using SNIBypassGUI.Common;
using SNIBypassGUI.Enums;

namespace SNIBypassGUI.Models
{
    public class AffinityRule : NotifyPropertyChangedBase
    {
        #region Fields
        private string _pattern;
        private AffinityRuleMatchMode _mode;
        #endregion

        #region Properties
        public string Pattern
        {
            get => _pattern;
            set => SetProperty(ref _pattern, value);
        }

        public AffinityRuleMatchMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }
        #endregion

        #region Methods
        public override string ToString() =>
            $"{(Mode == AffinityRuleMatchMode.Exclude ? "排除" : "包含")} “{Pattern}”";

        /// <summary>
        /// 创建当前 <see cref="AffinityRule"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public AffinityRule Clone()
        {
            var clone = new AffinityRule
            {
                Pattern = Pattern,
                Mode = Mode
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="AffinityRule"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(AffinityRule rule)
        {
            if (rule == null) return;
            Pattern = rule.Pattern;
            Mode = rule.Mode;
        }
        #endregion
    }
}
