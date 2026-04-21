# 关于 coolapijust/SniShaper 开发者长期寄生、窃取社群成果及恶意污蔑的严正声明

**声明人：Racpast（SNIBypassGUI 唯一开发者）**

**日期：2026 年 4 月 21 日**

### 一、关于本人 4 月 19 日言论的定性

2026 年 4 月 19 日，我在 SNIBypassGUI issue [#63](https://web.archive.org/web/20260420195050/https://github.com/racpast/SNIBypassGUI/issues/63)[^1] 中使用了“开盒”等威胁性措辞。该言论构成违法威胁，是错误的。

**但这不能改变一个事实：coolapijust 在此前长达三个月的时间里，对我项目实施了系统性寄生、欺诈性引流、以及对我社群未公开信息的窃取。**

他试图将我情绪失控的片段单独裁剪出来，包装成“受害者”形象四处散播，以此转移视线、回避对其自身行为的审查。这种行为在公关上或许有效，在事实上不堪一击。

以下证据链，全部基于 GitHub 公开提交记录、互联网档案馆存档及 QQ 群聊记录。每一个时间戳都不可篡改。
> 为防证据遭到销毁或篡改，本文正文中涉及代码与网页提交的所有超链接，均已指向受保护的互联网档案馆（Wayback Machine）及 Archive.ph 的网页快照。对应的原始出处链接已在文末脚注中完整列出，供全网审计。

### 二、完整事件时间线

| 时间 (UTC+8) | 事件 | 证据 |
| :--- | :--- | :--- |
| **2026.01.18 17:35** | coolapijust 使用 QQ `1175034206` 加入 SNIBypassGUI 交流群。 |  <details><summary>Q 群管家欢迎消息</summary><img width="390" height="453" alt="image" src="https://github.com/user-attachments/assets/95f982f2-5e28-43ee-90f0-7512700aef7e" /><img width="435" height="420" alt="image" src="https://github.com/user-attachments/assets/6510cf6e-9c35-4eea-9c74-804a14e485ed" /></details> |
| **2026.02.17 14:11** | mechrevo（即 coolapijust）提交文件 [`规划文档_SNIBypass上位替代.md`](https://web.archive.org/web/20260420201209/https://github.com/coolapijust/SniShaper/blob/5fa50c5316d7ccd86c101c28e7c7b642262ec90a/%E8%A7%84%E5%88%92%E6%96%87%E6%A1%A3_SNIBypass%E4%B8%8A%E4%BD%8D%E6%9B%BF%E4%BB%A3.md)[^2]。文件标题直接写明取代 SNIBypassGUI 的战略意图。 | Commit [`5fa50c5`](https://web.archive.org/web/20260420201612/https://github.com/coolapijust/SniShaper/commit/5fa50c5316d7ccd86c101c28e7c7b642262ec90a)[^3] |
| **2026.02.19 00:17** | mechrevo 提交 [`cmd/rules-convert-v2/main.go`](https://web.archive.org/web/20260420185840/https://github.com/coolapijust/SniShaper/blob/3e6b4d362cf54e4d015dd8c9ec9aa76dc8ef9e16/cmd/rules-convert-v2/main.go)[^4]。该代码专用于解析 SNIBypassGUI 的 [`nginx.conf`](https://web.archive.org/web/20260420190146/https://github.com/racpast/SNIBypassGUI/blob/735d43edf034f589673131c391dd2228d6be34a7/Resources/nginx/nginx.conf)[^5]，提取全部站点规则。 | Commit [`3e6b4d3`](https://web.archive.org/web/20260420202215/https://github.com/coolapijust/SniShaper/commit/3e6b4d362cf54e4d015dd8c9ec9aa76dc8ef9e16)[^6] |
| **2026.02.21 21:29** | 我在群内公开感谢 coolapijust，并授予头衔。**此时他已提交取代规划与配置窃取工具，而我毫不知情。** | <details><summary>群聊天记录</summary><img width="357" height="321" alt="image" src="https://github.com/user-attachments/assets/ecbee48e-402e-47a4-b71f-ce45aca2325f" /></details> |
| **2026.03.30 23:03** | SniShaper README 中，文字为 `SNIBypassGUI` 的链接被指向其自己的项目 `SniViewer`。 | Commit [`d98e990`](https://web.archive.org/web/20260420203504/https://github.com/coolapijust/SniShaper/commit/d98e9908239ebe2ad8c6e0729ee226b448f2fa78)[^7] 中的 [`README.md`](https://web.archive.org/web/20260420203100/https://github.com/coolapijust/SniShaper/blob/d98e9908239ebe2ad8c6e0729ee226b448f2fa78/README.md)[^8]、Commit [`07c27da`](https://web.archive.org/web/20260420204026/https://github.com/coolapijust/SniShaper/commit/07c27da3f2455ed5958f08787f49b28d95bd61f0)[^9] 中的 [`README.md`](https://web.archive.org/web/20260420203853/https://github.com/coolapijust/SniShaper/blob/07c27da3f2455ed5958f08787f49b28d95bd61f0/README.md)[^10] |
| **2026.04.02 12:37** | 多语言 README 推送，该错误链接被原样复制到英文、俄文版本。 | Commit [`4b1d386`](https://web.archive.org/web/20260420204240/https://github.com/coolapijust/SniShaper/commit/4b1d38640b27ad5f798214be5edbb37d42531955)[^11] |
| **2026.04.04 10:31** | 新 QQ `3989623715`（昵称“历史哲学经济学墙”）加入群聊，全程静默。 | <details><summary>Q 群管家欢迎消息</summary><img width="369" height="570" alt="image" src="https://github.com/user-attachments/assets/78f24294-1870-442d-8dbf-55ebce0d337d" /><img width="966" height="1434" alt="image" src="https://github.com/user-attachments/assets/7fa4d6e4-7921-40ad-b405-88094c9f3bb3" /></details> |
| **2026.04.12-15** | 我**仅在群内**（未在 GitHub 或任何公开渠道）发起站点需求征集，并两次发布确认图片。 | <details><summary>群聊天记录</summary><img width="1020" height="1682" alt="5719bb7df7e3cf53fe9b9734d8272a07" src="https://github.com/user-attachments/assets/95fe1914-ebfd-4d4b-adfa-3557842bbd0f" /><img width="1019" height="871" alt="8fd647ba82229822941ce686fe3a5baa" src="https://github.com/user-attachments/assets/2a82ed59-a188-4da8-9bb8-46022e01ad0d" /><img width="1019" height="881" alt="3625bac12e25f4c2127a13f248208e6d" src="https://github.com/user-attachments/assets/ecfcb8a1-c2f9-400f-a0a4-1947ba50281d" /><img width="577" height="949" alt="0795133f093a8c80c45df63010a56125" src="https://github.com/user-attachments/assets/603d6f97-e31a-40ca-9d91-02ed599bedc2" /></details> |
| **2026.04.17 04:21** | 我通宵调试后，发送第三张进度长图。长图底部手写了 `87. nicovideo.jp` 并划掉。由于转发失误，我未将该图片转发到他所在的群内，而是转发了两次到樱花庄的学术交流群①。**这导致 coolapijust 没有看到 `nicovideo.jp`**。 | <details><summary>群聊天记录</summary><img width="1547" height="1371" alt="1a27b25298fc3cfbcc49c5fee060b549" src="https://github.com/user-attachments/assets/82e7084c-ebff-454e-b17f-e7ebed3fff30" /></details> |
| **2026.04.18 20:11** | SniShaper v1.25 发布。压缩包内 `config.json` 包含群内征集的全部站点，**唯独缺少 `nicovideo.jp`**。 | [Release 资产 SHA256 存档](https://web.archive.org/web/20260420210906/https://release-assets.githubusercontent.com/github-production-release-asset/1159768927/343d3bc7-ffc2-4868-9fea-e0611efa9bf6?sp=r&sv=2018-11-09&sr=b&spr=https&se=2026-04-20T22%3A05%3A46Z&rscd=attachment%3B+filename%3DSniShaper-win-amd64.7z&rsct=application%2Foctet-stream&skoid=96c2d410-5711-43a1-aedd-ab1947aa7ab0&sktid=398a6654-997b-47e9-b12b-9515b896b4de&skt=2026-04-20T21%3A05%3A07Z&ske=2026-04-20T22%3A05%3A46Z&sks=b&skv=2018-11-09&sig=lFTjJ0wRRxtn0yl9lRu1dP7quCG6L6QfvkO6lr9I7vc%3D&jwt=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJnaXRodWIuY29tIiwiYXVkIjoicmVsZWFzZS1hc3NldHMuZ2l0aHVidXNlcmNvbnRlbnQuY29tIiwia2V5Ijoia2V5MSIsImV4cCI6MTc3NjcyMTEzNSwibmJmIjoxNzc2NzE5MzM1LCJwYXRoIjoicmVsZWFzZWFzc2V0cHJvZHVjdGlvbi5ibG9iLmNvcmUud2luZG93cy5uZXQifQ.GY5sIfgUlNQuFsih8uxuWjV65YXregD-O0uBrbkhqYg&response-content-disposition=attachment%3B%20filename%3DSniShaper-win-amd64.7z&response-content-type=application%2Foctet-stream)[^12] |
| **2026.04.19 13:58** | 我发现窃取行为，情绪失控发表威胁言论。 | issue [#63](https://web.archive.org/web/20260420195050/https://github.com/racpast/SNIBypassGUI/issues/63)[^1] |
| **2026.04.19 16:37** | 潜伏账号 `3989623715` 退出群聊。 | <details><summary>群后台退群记录</summary><img width="1344" height="267" alt="image" src="https://github.com/user-attachments/assets/ea1d5ea6-8198-4f5e-97e5-6fe5b926dfd0" /></details> |
| **2026.04.19 16:38** | coolapijust 在 issue [#63](https://web.archive.org/web/20260420195050/https://github.com/racpast/SNIBypassGUI/issues/63)[^1] 中确认退群，同时在 issue #10 中开始对我进行污名化攻击。 | issue [#10](https://web.archive.org/web/20260420211557/https://github.com/coolapijust/SniShaper/issues/10)[^13] |

### 三、五条证据

#### 证据一：专门用于窃取 SNIBypassGUI 规则的代码工具

文件 [`cmd/rules-convert-v2/main.go`](https://web.archive.org/web/20260420185840/https://github.com/coolapijust/SniShaper/blob/3e6b4d362cf54e4d015dd8c9ec9aa76dc8ef9e16/cmd/rules-convert-v2/main.go)[^4] 中的代码功能单一而明确——读取 SNIBypassGUI 格式的 [`nginx.conf`](https://web.archive.org/web/20260420190146/https://github.com/racpast/SNIBypassGUI/blob/735d43edf034f589673131c391dd2228d6be34a7/Resources/nginx/nginx.conf)[^5]，解析 `upstream` 与 `server` 块，输出 SniShaper 格式的规则 JSON。
这不是通用工具，这是为寄生 SNIBypassGUI 而量身定制的窃取器。coolapijust 从进入社群之初就准备好了这把钥匙。

#### 证据二：持续一月、跨越多轮提交的欺诈性引流链接

将 `SNIBypassGUI` 的超链接指向 `coolapijust/SniViewer`，自 3 月 26 日首次出现，至 4 月 19 日被我点破，**长达 24 天**。
在 `0bb8af9`、`f1cee68`、`b5a4de5`、`4b1d386`（中英俄三语版本）、`0996411`、`0d6973f`、`2aa0c35`、`a53a718` 等至少 8 次提交中，该“错误”被原样保留或复制扩散。
4 月 19 日，在我尚未指出链接具体位置时，他在 issue [#63](https://web.archive.org/web/20260420195050/https://github.com/racpast/SNIBypassGUI/issues/63)[^1] 中主动提及“readme 链接不小心写错了”。**一个声称“对此事没有印象”的人，绝无可能在毫无提示的情况下瞬间定位到那个唯一的错误链接。**
这不是手误，这是蓄意的 SEO 劫持。目的就是让搜索 SNIBypassGUI 的用户被误导至他的项目页面。

#### 证据三：潜伏小号——社群间谍行为

coolapijust 的大号 `1175034206` 退群后，其小号 `3989623715` 于 4 月 4 日加入并静默潜伏。
他在 issue 中承认“本人确实开了小号在群里潜水”。4 月 19 日 16:37，该小号退群；16:38，coolapijust 在 issue 中回复“我已经退出了本群”。
这是对社群的渗透。他所谓“不理解为什么这能让你恶语相向”，是在把间谍行为轻描淡写为“潜水”。

#### 证据四：未公开站点列表的精准窃取——与窃取者同步的“失误”

此条证据不需要任何代码对比，仅凭行为逻辑即可定罪。
2026 年 4 月 12 日至 15 日，我**仅在本人管理的两个私密 QQ 群内**发起站点需求征集（未在 GitHub 或任何公开渠道发布），并两次发布图片确认结果。此时 coolapijust 的潜伏小号 `3989623715` 正在 SNIBypassGUI 交流群内。
2026 年 4 月 17 日 04:21，我通宵调试完成后，制作了第三张进度长图。该图底部手写加入了 `87. nicovideo.jp` 并用红线划掉。**由于转发失误，我并未将该图片转发到他所在的 SNIBypassGUI 交流群，而是连续两次误发到了另一个他不在的群（樱花庄的学术交流群①）。**
2026 年 4 月 18 日，coolapijust 发布 SniShaper v1.25。其内置的 `config.json`（位于 v1.25 Release 资产 SHA256 存档中）**完整包含了我前两次在群内发布的全部新增站点**，但**精准地缺失了 `nicovideo.jp`**。
v1.25 新增的站点列表与我在群内前两次公示的列表高度重合，其中包含 `hanime1.me`、`loverslab.com` 等非大众热门站点。若非直接抄录群内信息，绝无可能出现如此一致的需求清单；如果他通过其他公开渠道或独立研究获取列表，不可能出现“我发到群里的他抄了，我漏发到群里的他也漏了”的情况。
唯一的解释是，他的信息源就是我发在 SNIBypassGUI 交流群内的图片。他没有看到第三张图，所以他抄不到 `nicovideo.jp`。**
且该窃取而来的 `config.json` **并未提交到其 GitHub 主分支**。截至 2026 年 4 月 21 日，其主分支的 [`config.json`](https://web.archive.org/web/20260420214106/https://github.com/coolapijust/SniShaper/blob/a53a718e4705932c7fa531588e8790421115545c/rules/config.json)[^14] 仍停留在早前版本。他将这份“见不得光”的配置文件直接打包进 Release 压缩包，而不敢推送到公开仓库。**若来源正当，何惧公开提交？**

#### 证据五：内部规划文档——取代 SNIBypassGUI 的明确意图

文件 [`规划文档_SNIBypass上位替代.md`](https://web.archive.org/web/20260420201209/https://github.com/coolapijust/SniShaper/blob/5fa50c5316d7ccd86c101c28e7c7b642262ec90a/%E8%A7%84%E5%88%92%E6%96%87%E6%A1%A3_SNIBypass%E4%B8%8A%E4%BD%8D%E6%9B%BF%E4%BB%A3.md)[^2] 提交于 **2 月 17 日**，配置窃取工具提交于 **2 月 19 日**，而我在群内公开感谢他是在 **2 月 21 日**。
在我对他表达善意与尊重的时候，他已经完成了对我的技术分析和取代路线图，并编写好了窃取我劳动成果的工具。

### 四、对其狡辩的逐条驳斥

**1. “IP/SNI 列表能否视为版权问题有待讨论”**

驳斥：核心指控不是版权，是**窃取手段**。你编写专用工具解析我的配置文件，你开小号潜伏我的社群，你抄袭我在群内收集的用户需求——这三件事与版权法无关，与你个人的道德底线有关。拿版权法当挡箭牌，只能证明你对事实本身无从辩解。

**2. “README 那里绝非刻意，我对此事没有印象”**

驳斥：你的行为与你的言辞自相矛盾。一个“没印象”的人，不会在 24 天内反复修改 README 却对错误视而不见；一个“没印象”的人，不会在被我点破的瞬间就准确找到那个链接并开始辩解。你的反应速度出卖了你的“没印象”。

**3. “这不是商业软件，搬代码又怎么样”**

驳斥：这是对开源许可证的公开藐视。SNIBypassGUI 采用 **AGPLv3** 协议。如果你将我的规则集或衍生代码用于你的 MIT 协议项目，你已构成**许可证违规**。开源不等于无主之地，AGPLv3 的法律效力不因你的蔑视而消失。

**4. “我只是共享了技术思路”**

驳斥：共享思路是事实，我感谢过你。但你随后做的不是继续共享，而是编写窃取工具、潜伏私密社群、劫持 SEO 链接、抄袭未公开需求。**思路共享是开源，系统窃取是盗窃。** 两者之间有一条你假装看不见的界线。

### 五、对其污蔑言论的反驳

coolapijust 在 issue [#10](https://web.archive.org/web/20260420211557/https://github.com/coolapijust/SniShaper/issues/10)[^13] 中声称：

- **“使用 SNIBypassGUI，享受开盒人生”**：这是将我的个人威胁言论偷换为软件功能，属于恶意造谣。
- **“开发者情绪失控，软件有热更新，用户请警惕”**：SNIBypassGUI 的更新服务器内容公开于 `racpast/SNIBypassGUITemp`，任何人可审查。将开源项目的常规更新机制暗示为“可能推送炸弹”，是典型的污名化手段。
- **“此人有大量对其它用户恶语相向的记录”**：他引用的所谓“证据”，仅仅是我对其他劣质项目的技术性批评，以及日常回复用户问题时的直言不讳，断章取义根本无法掩盖事实。关于我的人品以及对项目高度负责的态度，所有长期在交流群内互动的群友皆可为我作证。一个靠开小号潜伏在社群里窃取他人心血的人，没有任何资格对一位开源作者的品格指手画脚。

### 六、最终声明

我承认 4 月 19 日的威胁言论错误，我已为此道歉。

**但这不意味着 coolapijust 可以借此洗白他长达三个月的寄生、窃取与欺诈行为。**

我在此提出明确要求：

1. **立即停止欺诈性引流**。修正所有指向错误的链接，并向公众解释为何该“错误”能持续 24 天、跨越 8 次提交而“未被发现”。
2. **立即停止对我社群的渗透监控**。开源社区的交流在公开渠道进行，而不是靠小号潜伏。
3. **停止散布“软件不安全”、“开发者推送炸弹”等不实指控**。

本文所述全部事实均有 GitHub Commit 记录、互联网档案馆快照及 QQ 群聊记录为证。任何试图将此事简化为“两个开发者吵架”的叙事，都是对事实的歪曲。

**Racpast**

**SNIBypassGUI 项目地址：https://github.com/racpast/SNIBypassGUI**

[^1]: 原始链接为 https://github.com/racpast/SNIBypassGUI/issues/63
[^2]: 原始链接为 https://github.com/coolapijust/SniShaper/blob/5fa50c5316d7ccd86c101c28e7c7b642262ec90a/%E8%A7%84%E5%88%92%E6%96%87%E6%A1%A3_SNIBypass%E4%B8%8A%E4%BD%8D%E6%9B%BF%E4%BB%A3.md
[^3]: 原始链接为 https://github.com/coolapijust/SniShaper/commit/5fa50c5316d7ccd86c101c28e7c7b642262ec90a
[^4]: 原始链接为 https://github.com/coolapijust/SniShaper/blob/3e6b4d362cf54e4d015dd8c9ec9aa76dc8ef9e16/cmd/rules-convert-v2/main.go
[^5]: 原始链接为 https://github.com/racpast/SNIBypassGUI/blob/735d43edf034f589673131c391dd2228d6be34a7/Resources/nginx/nginx.conf
[^6]: 原始链接为 https://github.com/coolapijust/SniShaper/commit/3e6b4d362cf54e4d015dd8c9ec9aa76dc8ef9e16
[^7]: 原始链接为 https://github.com/coolapijust/SniShaper/commit/d98e9908239ebe2ad8c6e0729ee226b448f2fa78
[^8]: 原始链接为 https://github.com/coolapijust/SniShaper/blob/d98e9908239ebe2ad8c6e0729ee226b448f2fa78/README.md
[^9]: 原始链接为 https://github.com/coolapijust/SniShaper/commit/07c27da3f2455ed5958f08787f49b28d95bd61f0
[^10]: 原始链接为 https://github.com/coolapijust/SniShaper/blob/07c27da3f2455ed5958f08787f49b28d95bd61f0/README.md
[^11]: 原始链接为 https://github.com/coolapijust/SniShaper/commit/4b1d38640b27ad5f798214be5edbb37d42531955
[^12]: 原始链接为 <https://github.com/coolapijust/SniShaper/releases/download/v1.25/SniShaper-win-amd64.7z>，其哈希值可通过 <https://archive.ph/SWiEm>（原始链接为 <https://api.github.com/repos/coolapijust/SniShaper/releases/tags/v1.25>）确认为 `f7f0f74e588196e37d24e4c827408274fe55785981396d7d1f8714bd4f3ed17b`
[^13]: 原始链接为 https://github.com/coolapijust/SniShaper/issues/10
[^14]: 原始链接为 https://github.com/coolapijust/SniShaper/blob/a53a718e4705932c7fa531588e8790421115545c/rules/config.json
