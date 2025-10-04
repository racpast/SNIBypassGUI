using System;
using System.Collections.ObjectModel;
using System.Linq;
using SNIBypassGUI.Common;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Interfaces;

namespace SNIBypassGUI.Models
{
    /// <summary>
    /// 表示一个域名映射表。
    /// </summary>
    public class DnsMappingTable : NotifyPropertyChangedBase, IStorable
    {
        #region Fields
        private Guid _id;
        private string _tableName;
        private bool _isBuiltIn;
        private ObservableCollection<DnsMappingGroup> _mappingGroups = [];
        #endregion

        #region Properties
        /// <summary>
        /// 此映射表的唯一标识符。
        /// </summary>
        public Guid Id
        {
            get => _id;
            internal set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 此映射表的名称。
        /// </summary>
        public string TableName
        {
            get => _tableName;
            set => SetProperty(ref _tableName, value);
        }

        /// <summary>
        /// 此映射表是否为内置的。
        /// </summary>
        public bool IsBuiltIn
        {
            get => _isBuiltIn;
            set => SetProperty(ref _isBuiltIn, value);
        }

        /// <summary>
        /// 此映射表的映射组列表。
        /// </summary>
        public ObservableCollection<DnsMappingGroup> MappingGroups
        {
            get => _mappingGroups;
            set => SetProperty(ref _mappingGroups, value);
        }
        #endregion

        #region Methods
        /// <summary>
        /// 创建当前 <see cref="DnsMappingTable"/> 实例的完整副本。
        /// </summary>
        /// <returns>当前对象的一个完整副本。</returns>
        public DnsMappingTable Clone()
        {
            var clone = new DnsMappingTable
            {
                Id = Id,
                TableName = TableName,
                IsBuiltIn = IsBuiltIn,
                MappingGroups = [.. MappingGroups.Select(group => group.Clone())]
            };

            return clone;
        }

        /// <summary>
        /// 使用指定的 <see cref="DnsMappingTable"/> 实例的属性值更新当前实例的内容。
        /// </summary>
        public void UpdateFrom(DnsMappingTable source)
        {
            if (source == null) return;
            TableName = source.TableName;
            IsBuiltIn = source.IsBuiltIn;
            MappingGroups = [.. source.MappingGroups?.Select(w => w.Clone()).OrEmpty()];
        }
        #endregion
    }
}
