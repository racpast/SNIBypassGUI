using System;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Factories
{
    class UpstreamGroupFactory : IFactory<UpstreamGroup>
    {
        /// <summary>
        /// 新建上游组。
        /// </summary>
        public UpstreamGroup CreateDefault()
        {
            return new UpstreamGroup
            {
                Id = Guid.NewGuid(),
                GroupName = "新上游组",
                IsBuiltIn = false,
                ServerSources = []
            };
        }
    }
}
