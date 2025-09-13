using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SNIBypassGUI.Models;
using static SNIBypassGUI.Consts.PathConsts;
using SNIBypassGUI.Utils.Codecs;
using static SNIBypassGUI.Utils.LogManager;


namespace SNIBypassGUI.Repositories
{
    public class WebsitesRepository
    {
        public Website CreateDefaultWebsite(WebsiteManagementType type)
        {
            if (type == WebsiteManagementType.GuiManaged)
            {
                return new Website
                {
                    Id = new Guid(),
                    WebsiteName = "新网站",
                    Type = WebsiteManagementType.GuiManaged,
                    CertificateDomains = string.Empty,
                    IsBuiltIn = false,
                    ServerBlockRules = []
                };
            }
            else
            {
                return new Website
                {
                    Id = new Guid(),
                    WebsiteName = "新网站",
                    Type = WebsiteManagementType.SourceManaged,
                    CertificateDomains = string.Empty,
                    IsBuiltIn = false,
                    SourceCode = string.Empty
                };
            }

        }

        public ObservableCollection<Website> LoadAll(ObservableCollection<UpstreamGroup> profileCollection)
        {
            var allProfiles = new ObservableCollection<Website>();
            var idSet = profileCollection.Select(p => p.Id.ToString()).ToHashSet();
            LoadFromFolder(allProfiles, BuiltInWebsitesDirectory, idSet);
            LoadFromFolder(allProfiles, UserWebsitesDirectory, idSet);
            return allProfiles;
        }

        public Website LoadFromFile(string filePath, HashSet<string> idCollection)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                if (!Guid.TryParse(fileNameWithoutExt, out Guid profileId))
                {
                    WriteLog($"文件名 '{fileNameWithoutExt}' 不是一个有效的 GUID，已跳过。", LogLevel.Warning);
                    return null;
                }
                string jsonContent = File.ReadAllText(filePath);
                JObject jObject = JObject.Parse(jsonContent);

                if (!jObject.ContainsKey("WebsiteName") ||
                    !jObject.ContainsKey("IsBuiltIn") ||
                    !jObject.ContainsKey("Type") ||
                    !jObject.ContainsKey("CertificateDomains"))
                {
                    WriteLog($"文件 '{Path.GetFileName(filePath)}' 缺少一个或多个通用必填字段，已跳过。", LogLevel.Warning);
                    return null;
                }

                string typeStr = jObject.Value<string>("Type") ?? string.Empty;
                if (!Enum.TryParse<WebsiteManagementType>(typeStr, true, out WebsiteManagementType type))
                {
                    WriteLog($"文件 '{Path.GetFileName(filePath)}' 中的管理类型 '{typeStr}' 无效，已跳过。", LogLevel.Warning);
                    return null;
                }

                bool specificFieldsAreValid = true;
                switch (type)
                {
                    case WebsiteManagementType.SourceManaged:
                        if (!jObject.ContainsKey("SourceCode")) specificFieldsAreValid = false;
                        break;
                    case WebsiteManagementType.GuiManaged:
                        if (!jObject.ContainsKey("ServerBlockRules") || jObject["ServerBlockRules"]?.Type != JTokenType.Array)
                        {
                            specificFieldsAreValid = false;
                            break;
                        }
                        foreach (var entry in jObject["ServerBlockRules"])
                        {
                            if (entry?.Type != JTokenType.Object)
                            {
                                specificFieldsAreValid = false;
                                break;
                            }
                            JObject entryObj = (JObject)entry;
                            if (!entryObj.ContainsKey("MatchingDomains") || !entryObj.ContainsKey("UpstreamGroupId") || !entryObj.ContainsKey("SendCustomSni"))
                            {
                                specificFieldsAreValid = false;
                                break;
                            }
                        }
                        break;
                    default:
                        specificFieldsAreValid = false;
                        break;
                }

                if (!specificFieldsAreValid)
                {
                    WriteLog($"文件 '{Path.GetFileName(filePath)}' 缺少其类型 '{type}' 所需的特定字段，已跳过。", LogLevel.Warning);
                    return null;
                }

                var website = new Website
                {
                    Id = profileId,
                    WebsiteName = jObject.Value<string>("WebsiteName") ?? string.Empty,
                    IsBuiltIn = jObject.Value<bool>("IsBuiltIn"),
                    Type = type,
                    CertificateDomains = jObject.Value<string>("CertificateDomains") ?? string.Empty
                };

                switch (type)
                {
                    case WebsiteManagementType.SourceManaged:
                        string originCode = Base64Utils.DecodeString(jObject.Value<string>("SourceCode") ?? string.Empty);
                        if (originCode != null) website.SourceCode = originCode;
                        else
                        {
                            WriteLog($"文件 '{Path.GetFileName(filePath)}' 中 SourceCode 的值不是有效的 Base64 字符串，已跳过。", LogLevel.Warning);
                            return null;
                        }
                        break;
                    case WebsiteManagementType.GuiManaged:
                        var serverBlockRules = new ObservableCollection<ServerBlockRule>();
                        foreach (var entry in jObject["ServerBlockRules"])
                        {
                            JObject entryObj = (JObject)entry;
                            var serverBlockRule = new ServerBlockRule
                            {
                                MatchingDomains = entryObj.Value<string>("MatchingDomains") ?? string.Empty,
                                SendCustomSni = entryObj.Value<bool>("SendCustomSni"),
                                CustomSniValue = entryObj.Value<string>("CustomSniValue") ?? string.Empty
                            };
                            string guidStr = entryObj.Value<string>("UpstreamGroupId") ?? string.Empty;
                            serverBlockRule.UpstreamGroupId = idCollection.Contains(guidStr) && Guid.TryParse(guidStr, out Guid guid) ? guid : Guid.Empty;
                            serverBlockRules.Add(serverBlockRule);
                        }
                        website.ServerBlockRules = serverBlockRules;
                        break;
                }
                return website;
            }
            catch (Exception ex)
            {
                WriteLog($"加载文件 '{filePath}' 时发生错误。", LogLevel.Error, ex);
                return null;
            }
        }

        public void LoadFromFolder(ObservableCollection<Website> collection, string folderPath, HashSet<string> idCollection)
        {
            if (!Directory.Exists(folderPath)) return;
            foreach (var profile in Directory.GetFiles(folderPath, "*.json").Select(file => LoadFromFile(file, idCollection)).Where(p => p != null)) collection.Add(profile!);
        }

        public void SaveToFolder(Website website, string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                var jObject = new JObject
                {
                    ["WebsiteName"] = website.WebsiteName,
                    ["IsBuiltIn"] = website.IsBuiltIn,
                    ["Type"] = website.Type.ToString(),
                    ["CertificateDomains"] = website.CertificateDomains
                };
                switch (website.Type)
                {
                    case WebsiteManagementType.SourceManaged:
                        jObject["SourceCode"] = Base64Utils.EncodeString(website.SourceCode ?? string.Empty);
                        break;
                    case WebsiteManagementType.GuiManaged:
                        var rulesArray = new JArray();
                        foreach (var rule in website.ServerBlockRules ?? Enumerable.Empty<ServerBlockRule>())
                        {
                            var ruleObj = new JObject
                            {
                                ["Address"] = rule.MatchingDomains,
                                ["SendCustomSni"] = rule.SendCustomSni,
                                ["CustomSniValue"] = rule.CustomSniValue,
                                ["UpstreamGroupId"] = rule.UpstreamGroupId.ToString()
                            };
                            rulesArray.Add(ruleObj);
                        }
                        jObject["ServerBlockRules"] = rulesArray;
                        break;
                    default:
                        WriteLog($"无法保存文件 '{website.Id}.json'，其中包含未知的类型。", LogLevel.Warning);
                        return;
                }
                string filePath = Path.Combine(folderPath, $"{website.Id}.json");
                File.WriteAllText(filePath, jObject.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                WriteLog($"保存文件 '{website.Id}.json' 时发生错误。", LogLevel.Error, ex);
            }
        }

        public void SaveToFolder(IEnumerable<Website> profiles, string folderPath)
        {
            foreach (var profile in profiles) SaveToFolder(profile, folderPath);
        }
    }
}
