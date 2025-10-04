using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Common.Extensions;
using SNIBypassGUI.Common.Results;
using SNIBypassGUI.Enums;
using SNIBypassGUI.Interfaces;
using SNIBypassGUI.Models;

namespace SNIBypassGUI.Common.Mapper
{
    public class TargetIpSourceMapper(IMapper<FallbackAddress> fallbackAddressMapper) : IMapper<TargetIpSource>
    {
        /// <summary>
        /// 将 <see cref="TargetIpSource"/> 类型的 <paramref name="targetIpSource"/> 实例转换为 JSON 对象。
        /// </summary>
        public JObject ToJObject(TargetIpSource targetIpSource)
        {
            var jObject = new JObject { ["sourceType"] = targetIpSource.SourceType.ToString() };

            if (targetIpSource.SourceType == IpAddressSourceType.Static)
                jObject["addresses"] = new JArray(targetIpSource.Addresses.OrEmpty());
            else
            {
                jObject["queryDomains"] = new JArray(targetIpSource.QueryDomains.OrEmpty());
                jObject["resolverId"] = targetIpSource.ResolverId.ToString();
                jObject["ipAddressType"] = targetIpSource.IpAddressType.ToString();
                jObject["enableFallbackAutoUpdate"] = targetIpSource.EnableFallbackAutoUpdate;
                jObject["fallbackIpAddresses"] = new JArray(targetIpSource.FallbackIpAddresses?.Select(fallbackAddressMapper.ToJObject).OrEmpty());
            }

            return jObject;
        }

        /// <summary>
        /// 从 JSON 对象创建一个新的 <see cref="TargetIpSource"/> 实例。
        /// </summary>
        public ParseResult<TargetIpSource> FromJObject(JObject jObject)
        {
            if (jObject == null)
                return ParseResult<TargetIpSource>.Failure("JSON 对象为空。");

            if (!jObject.TryGetEnum("sourceType", out IpAddressSourceType sourceType))
                return ParseResult<TargetIpSource>.Failure("一个或多个通用字段缺失或类型错误。");

            var source = new TargetIpSource { SourceType = sourceType };

            if (sourceType == IpAddressSourceType.Static)
            {
                if (!jObject.TryGetArray("addresses", out IReadOnlyList<string> addresses))
                    return ParseResult<TargetIpSource>.Failure("直接指定模式所需的字段缺失或类型错误。");

                source.Addresses = [.. addresses];
            }
            else
            {
                if (!jObject.TryGetArray("queryDomains", out IReadOnlyList<string> queryDomains) ||
                    !jObject.TryGetNullableGuid("resolverId", out Guid? resolverId) ||
                    !jObject.TryGetEnum("ipAddressType", out IpAddressType ipAddressType) ||
                    !jObject.TryGetBool("enableFallbackAutoUpdate", out bool enableFallbackAutoUpdate) ||
                    !jObject.TryGetArray("fallbackIpAddresses", out IReadOnlyList<JObject> fallbackIpAddressObjects))
                    return ParseResult<TargetIpSource>.Failure("解析获取模式所需的字段缺失或类型错误。");
                ObservableCollection<FallbackAddress> fallbackIpAddresses = [];
                foreach (var item in fallbackIpAddressObjects.OfType<JObject>())
                {
                    var parsed = fallbackAddressMapper.FromJObject(item);
                    if (!parsed.IsSuccess)
                        return ParseResult<TargetIpSource>.Failure($"解析 fallbackIpAddresses 时出错：{parsed.ErrorMessage}");
                    fallbackIpAddresses.Add(parsed.Value);
                }
                source.QueryDomains = [.. queryDomains];
                source.ResolverId = resolverId;
                source.IpAddressType = ipAddressType;
                source.FallbackIpAddresses = fallbackIpAddresses;
                source.EnableFallbackAutoUpdate = enableFallbackAutoUpdate;
            }

            return ParseResult<TargetIpSource>.Success(source);
        }
    }
}
