# Sinmai-Assist
Mods for maimai DX (Sinmai.exe). Patch with MelonLoader.  

This is a cheat Mod, using by your own risk.

> [!WARNING] 
> Please use MelonLoader version 0.6.4 or lower. 
> Higher versions will crash.

## 此 fork 的使用说明

原作者：WYH2004 (Slim) & Error063。此 fork 修改者：**Modified by WkJ01N**；DLL 加载信息、启动横幅与作者相关元数据保留原作者并附加此修改者署名。

本分支保留原项目功能，并加入倍速、配置恢复、随机评级跳歌、手动保存与预设、统一快捷键、A/B 循环练习、正常播放评级控分及连续刷成绩／国服分批上传。当前适配检查以 **SDGB 25500（SDGB156E）** 的本地程序集为基准；其他游戏版本需要重新验证。新增功能提供构建和离线检查，音画、音符表现、游戏流程及实际服务端保存仍需要游戏内验收。

### 设置保存与预设

- 在 **Settings** 面板点击 **Save settings, presets and shortcuts**，保存当前运行设置、预设库与快捷键。临时调整立即生效，退出或重启不会自动保存；启动恢复最后一次手动保存的设置。
- 输入名称后可创建预设；选择预设后可应用、覆盖、重命名或删除。这些库编辑也先保留在内存，统一点击保存才写入文件。应用预设不会替换启动时恢复的快照，直到再次保存。
- 预设包含倍速开关／倍率／全局倍速、自动游玩模式及同步开关、跳歌模式／自定义值／强制 Miss／评级条件、独立的自动游玩评级条件、Show Info／FPS／主面板／服务器遮罩、循环次数／预播放时间／等待间隔／独立练习倍率／保持原音高选项，以及 Batch 的范围、数量、目标与各显示等级概率。概率配置深复制，预设之间独立。A/B 点、当前轮数和判定计划不保存，切歌清除；批次进度另行自动记录。
- 运行设置与预设使用 `Sinmai-Assist/RuntimeSettings.yml`，全局快捷键继续使用 `KeyBindConfig.yml`。首次没有已保存快照时沿用原 `Config.yml`；以后已保存的运行快照优先于其中对应的运行值。启动模块开关仍需修改 `Config.yml` 并重启，预设不启用未加载的模块。
- 保存先写临时文件再替换；同时保存两份文件时发生错误会尝试回退已经替换的文件，错误显示在面板和日志中。损坏的运行设置单独备份，不覆盖主配置和快捷键配置。
- 本次将运行设置升级到 `formatVersion: 3`，添加批次参数。只有版本 1 的旧快照及预设会将 0 秒循环间隔一次性迁移为 5 秒，非零间隔保留；**版本 2 已保存的 0 秒不会再次迁移**。迁移先在内存生效并提示未保存，点击保存才写入新版本标记。缺失的练习倍率默认为 1.00×，保持原音高默认开启。

### 快捷键设置

进入 **Hotkeys** 面板，所有运行期操作按功能分组，包括原有操作、保存与预设应用、A/B、评级控分及 Batch 的开始／暂停／停止／续跑等操作。每个命名预设都能单独绑定快捷键，绑定不会随预设切换。

**新配置的所有快捷键默认均为 `None`。** 已有 `KeyBindConfig.yml` 的绑定保留，缺失项补为未绑定；原先写死的空格跳歌取消，需要自行绑定 **FastSkip → Skip song**。没有面板快捷键且面板隐藏时，左下角提供 **Open Sinmai-Assist** 入口。

1. 点击 `None` 后按一个键盘按键完成绑定。
2. 等待按键时再次点击同一按钮，或按 Escape，取消录入。
3. 点击已经绑定的按键，将它清空为 `None`；重新绑定需再点击一次。
4. 修改立即生效，点击 Settings 或 Hotkeys 页的保存按钮才持久化。

首版不支持组合键、鼠标键和手柄键。新增重复绑定会被拒绝；旧文件中的冲突键保留显示但暂停派发，调整后恢复。输入文本、录入按键及录入后的按键释放阶段不会触发修改器快捷键，Escape 可退出文本编辑。统一快捷键直接读取 Unity 键盘输入，不依赖 `fix.fixDebugInput`。连续定位保留长按行为，其余操作每次按下执行一次；模块关闭或场景不适用时提示原因。

### A/B 循环练习

在 `Config.yml` 中启用 `cheat.chartController: true` 并重启，在 **ChartController** 面板设置 A、B（秒），或使用 **Set A now / Set B now**，然后启动练习。默认无限循环（次数 0）、3 秒谱面预播放、每轮后休息 **5 秒**；正整数次数表示完成对应轮数后暂停并保留最后一轮表现。`Interval (real seconds)` 可设为非负秒数，设为 0 则立即准备下一轮。休息时谱面、音乐和视频冻结，并显示倒计时；暂停／继续练习会冻结／恢复倒计时。预播放倒数使用谱面时间，受练习倍率影响。

**独立练习倍率**默认 1.00×，支持 **0.50×～2.00×**，可输入数值，或用按钮每次调整 0.05×。练习期间谱面、音乐和视频统一使用此倍率，覆盖 SpeedHack，不与其相乘。例如 SpeedHack 1.50×、练习 0.75×，实际练习按 0.75×播放；SpeedHack 关闭时也可使用练习倍率。休息倒计时不受任何倍率影响。修改倍率、应用预设和切换音高选项从**下一轮**生效，面板分别显示当前值与下一轮值；停止后重新开始也会应用新值。

`Preserve original pitch` 默认开启，通过练习音乐专用的 CRI 时间伸缩音频池尽量保持原调；关闭时音高随速度变化。判定音、语音及其他声音不路由到此音频池。接口不兼容或音频准备失败时暂停并说明原因，不会偷偷改变音高模式；可关闭保持音高后重新开始。退出练习通过原生整首重开恢复 SpeedHack 设置，练习期间对 SpeedHack 的修改不影响当前练习速度。专用音频资源在停止相关音乐后释放。

定位后会等待原生音乐准备完成再恢复谱面与视频，准备期间显示提示；此时手动暂停会取消自动恢复。超过 5 秒仍未准备完成则保持暂停并报告错误，可重新开始练习、关闭保持音高或整首重开。

Hotkeys 的 **Practice** 分组新增练习倍率增加／减少／恢复 1.00×和保持原音高切换，默认均为 `None`；保存规则与其他快捷键一致。

边界跨越 Hold、Touch Hold 或连接 Slide 时向外扩展，并为判定窗口预留时间；面板显示实际播放区间。每轮回收活动音符、恢复 Touch 组和 Slide 状态、同步音画，并清空本轮判定／连击／共享连锁。只有选定音符区间内的判定计入本轮；预播放与区间外音符不计入统计。当前轮数与已完成次数分别显示，达到指定次数后保留最后一轮表现。

修复了进入练习和每轮循环时音乐略微领先谱面的问题：原生开曲先启动谱面时钟，等待超过 91 毫秒后才开始音乐。现在记录本首歌曲实际音乐启动时的谱面时间（包含开曲倍速与帧调度影响），循环和普通手动定位均减去该偏移后定位音乐，不再将音乐与谱面设为同一个毫秒值。偏移不随循环累积，中途变速也不重新缩放；视频继续使用独立时间轴，玩家判定偏移不变。预播放回到歌曲开头时，音乐保持在零点暂停，直到谱面到达原生音乐启动点；用户暂停期间同样不会提前播放。无法取得音乐启动时间时拒绝定位并提示整首重开，不使用猜测的补偿值。

进一步修复了零点预播放越过音乐起点后、音乐仍从零开始造成的最多一帧滞后；现在按越过起点的残余时间重新定位再播放。音乐定位使用最近毫秒取整，原点采集前刷新谱面时钟。倍速时钟改为分段累积，切回 1×或关闭 SpeedHack 时保留已经积累的时间，音频计时也使用定位锚点，不将整段历史重新乘以新倍率。没有加入主观固定补偿；真实设备或时间伸缩带来的缓冲差异仍需游戏内试听。

**练习场次不进入正常结算，不保存或上传歌曲成绩。** 停止练习会暂停；可再次启动练习，或使用 **Exit practice and restart whole song** 走原生重试流程，重新开始整首歌曲后恢复正常计分。练习时不能普通定位、跳歌结算或启用评级控分；普通自动游玩、暂停及倍速可用。

支持普通谱面单人及本机双人，双人共用区间、分别统计。宴会、跨机组队、试玩、段位、挑战及特殊生命模式不启用。重置接口不兼容时停用该功能；重置发生错误时暂停，使用整首重开恢复。

### 正常播放按评级自动游玩

启用 `cheat.autoPlay: true` 并重启，在 **AutoPlay** 面板选择独立的目标评级和最低 **Any / FC / FC+ / AP / AP+** 条件，点击 **Enable / replan rank control**。开曲前选择会为下一首待命，歌曲中途选择会保留已有判定，仅规划剩余音符；目标与随机跳歌互不影响。手动选择或恢复的修改器模式优先于游戏开曲初始化的模式重置。

规划期间短暂停住谱面、音频和视频，完成后恢复原播放／暂停状态；每位玩家限制 500,000 步／1 秒。原有判定已经违反连击条件、目标不可达、搜索超限或接口缺失时，说明原因并保留原模式／原有效计划，不降低要求。面板分别显示待应用目标、当前生效目标和预计最终达成率；编辑目标后需要重新规划才替换当前计划。

接管后按音符索引执行计划，手动击打不改变音符判定；可随时切回手动或其他自动游玩模式。暂停与倍速保留计划，定位、重试、切歌、关闭接管或进入练习会取消当前计划。保存与预设只记录模式及条件，不保存歌曲判定计划。

支持范围与随机评级跳歌相同。本机双人共用条件、各自生成，全部成功后才统一接管。最终评级与连击条件以**计划完整执行且未中断**为前提；结算前检查实际判定及达成率，发现偏差时报告错误，不直接改写成绩数字。旧的 Random / 随机 AP / FC+ / FC 模式仍按原概率工作。

### 随机评级跳歌

1. 在游戏目录的 `Sinmai-Assist/Config.yml` 中设置 `cheat.fastSkip: true`，然后重启游戏。
2. 打开主面板，进入 **FastSkip**，选择 **Random**。面板隐藏且未绑定快捷键时使用左下角入口。
3. 选择目标评级（D～SSS+），以及最低连击条件 **Any / FC / FC+ / AP / AP+**。
4. 在歌曲播放期间点击 **Skip**，或使用自行设置的 **Skip song** 快捷键。

评级范围来自游戏当前加载的数据。例如通常 SS 对应 `[99.0000%, 99.5000%)`，SSS 对应 `[100.0000%, 100.5000%)`；最高评级包含谱面的理论上限。面板中的分数指**达成率百分比**，不是 DX 分数。

跳歌会重新生成**整首谱面**的判定，替换已经打出的部分。本机双人共享所选条件，但各自独立生成。无需启用 AutoPlay，正常播放时的随机自动游玩概率也不受此设置影响。

- **Any**：允许 Critical、Perfect、Great、Good 和 Miss。
- **FC**：不出现 Miss；允许最终获得 FC+、AP 或 AP+。
- **FC+**：不出现 Good、Miss；允许最终获得 AP 或 AP+。
- **AP**：只出现 Perfect 及以上；允许最终获得 AP+。
- **AP+**：按原生理论分和连击标记验证，普通音符可出现不损失理论分的 Perfect；BREAK 必须取得完整额外分，并非简单强制所有音符 Critical。条件与评级区间冲突时不生成成绩。

生成器使用游戏计分表，同时考虑 BREAK 基础分和额外分；EX 音符使用 Critical 或 Miss。Touch、Touch Hold 和连接 Slide 均按对应计分类型处理，连接段不重复计分。判定按结束时间提交，由游戏重新计算达成率、DX 分数、判定统计、最大连击和连击标记，不直接覆盖达成率数字。

**支持范围：普通谱面的单人及本机双人。** 宴会谱面、跨机组队、试玩、段位、挑战及特殊生命模式不支持生成成绩。Default 仍使用普通跳过流程。Custom 保留整数达成率输入及 Force Add 1 miss，并使用同一受验证的普通谱面计分路径。

判定是离散的，一些“评级 + 最低连击条件”组合不可达，例如 AP 无法产生大量失分。目标不可达、谱面不兼容或后台搜索超限时，面板显示原因并继续当前游玩，不修改成绩或降低条件。每位玩家的搜索上限为 500,000 步 / 1 秒；退出歌曲或重试会取消待处理的计划。

Custom 按谱面可达粒度近似输入值，结果不高于输入值，容差为本谱面最大的单音符损失；无 BREAK 谱面的上限为 100%。Force Add 1 miss 保证至少一个 Miss，并非严格只有一个。

可选配置（保持已有的 `cheat.fastSkip` 布尔键）：

```yaml
cheat:
  fastSkip: true
  randomSkip:
    targetRank: SSS       # D / C / B / BB / BBB / A / AA / AAA / S / S+ / SS / SS+ / SSS / SSS+
    minimumCombo: None    # None / FC / FCPlus / AP
```

旧配置无需迁移；缺失配置使用默认值。没有手动保存的运行快照时，启动默认选择 Default 跳歌模式；保存后恢复已保存的模式及条件。面板修改不自动写回 YAML。

### 连续刷成绩（Batch）

启用 `cheat.fastSkip: true` 并重启，在普通已登录玩家的歌曲列表打开 **Batch**。仅 1P、仅 2P 和本机双人均可使用；不支持游客、试玩、跨机组队、宴会、段位、挑战、任务／联动／大会等特殊分类与模式。遇到接口不兼容、登录身份改变或未知场景会暂停批次并显示原因。

1. 选择基础评级与最低 **Any / FC / FC+ / AP / AP+**，默认 SSS／Any，严格使用评级区间。SSS／AP+ 等显式冲突会拒绝启动，需手动修改。历史更好成绩由原生逻辑保留。
2. 选择 **当前难度**或 **All available difficulties**。普通列表保留当前 DX／标准类型及双方难度；等级列表采用条目对应难度。全部难度按 BASIC→Re:MASTER，排除不存在或不可正常选择的谱面。
3. 默认从当前歌向右处理 **10 首**，分类末尾停止。可开启跨右侧分类，或改为按分类数量（默认 **1 类**）；分类数包含当前分类，第一类只处理当前歌到末尾。最右侧停止，不绕回。
4. 可先 **Preview queue** 查看范围，再点击 **Start batch**。启动时冻结队列、参数与双方各谱面的目标，不受随后列表排序或预设编辑影响。批次期间保留 SpeedHack 调整，其余修改成绩的操作禁用，停止收尾后恢复原自动游玩模式。

一首歌以“歌曲 ID＋谱面类型”去重并合并难度任务，全部选中难度处理完才计一首。成功、已达标、部分失败和全部失败分别统计，均计入已处理歌曲数；没有可选难度的歌曲计失败。每个谱面最多尝试三次，重试不重新抽取目标、不重复计歌曲数。三次生成失败后，走原生重试清理路径返回选曲，仅移除本次尚未提交的日志，不制造空成绩或 Miss 成绩推进批次。接口、原生结果验证等异常暂停整批。

歌曲开始后暂停并在后台生成判定，每位玩家最多 500,000 步／1 秒；双人全部生成成功后统一提交，原生验证实际评级与连击标记后立即跳歌、结算并进入下一任务。默认 **Skip already achieved charts**，按本次抽取目标比较历史达成率和标记；双人只有双方都达标才跳过整次游玩，一方未达标时双方正常结算。批次控制依赖原生场景状态，超时、重试及上传等待使用真实时间，兼容 SpeedHack 和全局倍速。选择界面的原生倒计时在接管期间停止计时，避免自行开曲。

**低等级提升默认关闭**，开启后默认包含显示等级 **12+** 及以下，使用显示等级而非谱面定数。面板可逐等级编辑“评级＋最低标记”的联合权重，权重自动归一化；`Base` 表示基础要求，低于基础要求的候选向上合并，等效候选的概率相加。12+ 默认：原目标 60%、仅升 SSS+ 5%、SSS+／至少 FC+ 5%、SSS+／至少 AP 10%、AP+ 20%。每降低一个显示等级档位 `d`，AP+ 为 `min(80,20+5d)%`；剩余概率按 `max(0,60−10d)`、`5+2d`、`5+3d`、`10+5d` 的比例分配给前四项。各玩家依据自己谱面等级独立抽取一次，重试保持目标。AP+ 是独立抽取结果，普通评级候选排除理论满分；FC／FC+／AP 为最低要求，仍可取得更高标记。只有启用且符合阈值的提升才能超出基础评级区间。

**国服分批上传与 track 重置：** 启动时读取普通模式曲目上限 N，临时显示为 N+1。例如在 2／4 启动后为 2／5，第 4 曲完成后先上传尚未确认保存的日志，包括启动前已有日志。上传按实际游玩曲数计，与“歌曲数量”独立；刷多个难度可以跨批。上传逻辑参考 [AquaMai ImmediateSave](https://github.com/MewoLab/AquaMai/blob/c64077a2e2ecaa4f557f09fec84028a058940944/AquaMai.Mods/UX/ImmediateSave.cs)，实际使用本机已核对的国服构造函数、曲目数量参数及原生数据转换，分别记录每位玩家的每条日志和 UserAll 回执。请求数据在发送前固定；上传期间不开下一曲。

只有全部阶段明确成功，且原生下一曲流程完成必要读取后，才清理本批已确认日志并返回 **1／N+1**。明确失败或确认未发送的请求最多自动重试两次，真实间隔 1 秒、2 秒；已成功阶段不重复发送。超时或传输结果不明时暂停，不自动重发、清理日志或重置 track。面板显示未确认阶段；应先检查服务端记录，再明确确认已保存，或选择显式重试（可能重复日志），然后恢复。发现已加载的 ImmediateSave／其他可识别自动上传补丁时拒绝接管并提示，不修改外部 Mod 配置。如果游戏实际处于自由模式，仅冻结原生自由模式倒计时，不为国服强行开启该模式。

**Pause / resume batch** 暂停后不开始新任务或新上传阶段，已发出的请求仍记录回执。**Stop batch and save pending scores** 会在安全场景收尾、上传剩余成绩；成功后恢复原上限，保持登录并停留在 **1／N** 选曲界面。存在未确认上传时继续保留暂停和数据保护，解决后才能收尾。主面板隐藏时右上角仍有批次状态及暂停／停止入口。所有新增快捷键默认 `None`，在 Hotkeys 的 Batch 分组设置。

Batch 参数和概率库遵循手动保存／预设规则，运行状态不保存为用户设置。队列、已抽取目标、尝试次数和上传回执另自动记录到 `Sinmai-Assist/BatchProgress.yml`，不记录登录凭据；完成或正常停止后在 `BatchReports/批次ID.txt` 输出结果、重试和回执清单。写入进度失败会暂停，修复目录后用 **Save progress again** 重试。

重启后不会自动运行或重发旧请求。用相同玩家重新登录，在选曲界面点击 **Resume saved job**；上次存在未确认的成绩／请求时会要求先核对服务端历史，再点击 **Reviewed server history: resume after restart**。此复核入口要求当前没有待上传的原生日志，仅将上次未确认任务退回历史成绩核对，保留原目标，**不会重建或重发旧上传数据**。当前游戏仍在运行且上传未确认时，应在当前事务中解决，不能用重启复核入口清理数据。

### 倍速与快捷键

在 Debug 面板启用 SpeedHack；使用面板或 Hotkeys 中的增加 0.1 倍、减少 0.1 倍及重置操作。已有配置的小键盘绑定保留，新配置默认未绑定；也可手动修改 `KeyBindConfig.yml` 的 `speedHack` 节点。Enable Global Speed 控制非谱面阶段的全局倍速。关闭或退出时只恢复本功能接管过的全局时间倍率。

已设置的自动游玩快捷键可在隐藏面板时使用。旧配置的 F／G／H／J／K／O／P／N 等绑定保留，新配置全部为 None。概率随机模式不保证最终评级，需要保证评级时选择独立的评级控分模式。

### Show Info 信息显示

在 Debug 面板点击 **Toggle Show Info**，或设置 `modSetting.showInfo: true`，可在左上角显示修改器版本、游戏和数据版本、服务器地址、Keychip ID 及两位玩家的 ID。服务器地址继续遵循 `modSetting.maskTitleServerUrl` 设置。

信息按真实时间每秒刷新一次，不受倍速影响；重新开启后在下一个更新周期立即采集。各字段独立读取，尚未就绪或不可用的字段显示占位文字，未入场玩家显示 `Not entered`，游客显示 `Guest`。初始化完成后自动更新，无需重启或反复开关。文本按实际行数和可用宽度计算显示高度，并支持换行。

修复了当前中国版 Show Info 的 `BadImageFormatException: Expected reference type but got type kind 17`：旧编译依赖将 `AMDaemon.SerialId` 定义为 class，游戏实际将其定义为 struct。Keychip ID 现从运行时缓存的反射 getter 读取，不再把旧返回类型签名编译进 Mod。信息采集与绘制分离，相同字段的相同错误只记录一次，不再按绘制帧刷屏，也不再因累计失败十次永久停止显示。

### 稳定性修复及已知限制

- 跳歌和谱面控制按钮由更新流程消费，避免 IMGUI 多次绘制丢失或重复处理点击。
- 缓存自动游玩、跳歌中的高频反射接口；修复 Touch 判定时间检查、ButtonId 属性访问及重复判定音。
- 配置损坏时只备份并恢复对应文件，备份命名为 `.broken.时间.唯一标识`；空配置及显式 null 节点补齐默认值。
- 修复数据尚未初始化时的界面访问、安全模式标题重复追加、收藏补丁参数类型；网络日志按当前请求关联响应，并在异常退出时清理上下文。
- 用户备份使用 JSON 序列化，保留原导出结构，支持用户名中的引号、换行等字符。
- 修复 Windows 构建脚本的输出路径及无 Git 回退，清理构建警告。

玩家 ID 登录本轮未修复。当前中国版补丁只设置读卡结果，没有满足登录流程 `AnyRead()` 所需条件，同时在 `TryAime.Execute()` 每次执行后过早清除请求标志；后续还需要结合服务端验证登录凭据流程。二维码登录路径保留。

本地既有日志曾显示 AquaMai 的 `PacketUpsertUserAll` 构造函数适配失败；Batch 使用独立的国服适配，不修补外部 Mod，不能与其 ImmediateSave 同时接管。Quick Boot 在当前入口中因资源加载问题保持禁用。

### 验证

修改器目标框架仍为 .NET Framework 4.7.2。离线测试工具使用 .NET 10，不是游戏运行依赖：

```powershell
dotnet run --project Tests/ScorePlanner.Tests.csproj
```

如已安装 `ilspycmd`，可附加游戏程序集和 StreamingAssets 路径，检查实际计分表、私有接口及真实谱面的音符构成：

```powershell
dotnet run --project Tests/ScorePlanner.Tests.csproj -- "游戏目录/Sinmai_Data/Managed/Assembly-CSharp.dll" "游戏目录/Sinmai_Data/StreamingAssets"
```

测试包括小谱面穷举可达性对照、评级边界、最低连击条件、EX / BREAK、随机种子、强制 Miss、搜索预算与取消、配置恢复及原配置保留。提供本地游戏路径时另检查 90 个计分表条目、必要原生接口和 12 张普通谱面的音符构成。游戏源码、DLL 和谱面不随测试提交。

Show Info 回归测试另覆盖 class / struct getter、getter 缓存、部分字段失败、连续失败后的恢复、重复开关、错误去重与多行布局边界。可增加第三个参数指定新构建的 Mod，检查其中不存在旧 `SerialId` 类型引用和直接 Keychip getter 调用，并读取游戏 `AMDaemon.NET.dll` 的实际类型定义：

```powershell
dotnet run --project Tests/ScorePlanner.Tests.csproj -- "游戏目录/Sinmai_Data/Managed/Assembly-CSharp.dll" "游戏目录/Sinmai_Data/StreamingAssets" "Output/Sinmai-Assist.dll"
```

上述兼容性检查仅读取程序集元数据，不启动游戏。Show Info 的游戏内验收还需确认左上角各行完整可见、未就绪字段能恢复，以及多次开关后控制台不再持续报错。

新增离线检查直接链接设置存储、快捷键捕获／派发、区间策略和判定规划的生产代码，使用输入及游戏状态适配替身验证保存恢复、预设隔离、写入回退、绑定迁移／冲突／释放抑制、循环策略，以及中途接管的固定判定和完整谱面分母。它们不模拟 Unity 渲染、CRI 音频或真实设备输入。

另提供直接链接时间轴、练习和评级接管生产代码的流程检查，使用游戏边界替身验证仅 2P／双人、音符池与点击音重置、等待暂停、练习重试、规划失败回退及任务取消。音乐同步回归另覆盖实际启动偏移、开曲与中途倍速、首次进入和连续循环、停止后再开始、暂停定位／继续、歌曲零点预播放及缺失启动时间的拒绝处理。新增检查覆盖 5 秒真实等待、设置版本迁移、独立倍率优先级、下一轮应用、两种音高模式的参数／路由顺序、DSP 准备失败和资源释放，以及返回 1×、毫秒取整和跨帧残差：

```powershell
dotnet run --project Tests/Flows/Flows.csproj
```

提供第三个 Mod 路径参数时，还检查 DLL 中游戏和 AMDaemon 类型的值类型／引用类型签名与本地运行时元数据一致。

Batch 离线检查直接链接实际队列、概率策略、上传回执状态机及运行流程，覆盖冻结快照、分类边界、不绕回、全难度歌曲计数、跨分类去重、仅 2P／双人、目标固定、已有成绩跳过、AP+ 与普通候选排除理论分、深复制／脏状态、版本 2 的 0 秒保留、三次失败且无空成绩、暂停／停止、四曲分批、双人部分成功、明确失败重试、未知响应及迟到回执、持久化失败、日志清理顺序和重启复核。原生元数据检查另覆盖选曲／结算／重试／下一曲接口、国服上传构造函数和曲目数量参数、连击标记及场景常量。

Batch 游戏内验收建议先用当前难度、2 首、不提升概率，分别测试仅 1P、仅 2P 和双人；再测试 10 首跨过至少两个上传批次、全部难度、跨分类、SSS+／AP+、低等级提升及 SpeedHack／全局倍速组合。重点核对：第 N 曲后仍回选曲、两位玩家成绩实际保存、日志不漏不重、track 回到 1／N+1、暂停／停止与断网保护、收尾后 1／N 且仍登录。**离线测试没有发送实际上传请求；实际服务端保存、多批次重置及最终成绩记录必须在游戏中验证。**

新增功能游戏内验收：保存后重启恢复、预设切换和每项快捷键；A/B 开头／中段／尾段及跨界长音符／Touch 组／连接 Slide、仅 2P 入场和本机双人、倍速与暂停／等待、反复循环后整首重开及成绩记录隔离；评级控分的开曲／中途接管、不可达提示、关闭与重新规划、全部音符类型的判定／音效一致性和最终评级。

音乐同步验收请对比正常播放和循环播放时的音乐节拍、判定音与谱面位置，覆盖首次进入、连续多轮、暂停后继续和预播放从歌曲零点开始，确认音乐不再提前且没有逐轮漂移；另分别检查开曲即倍速和歌曲中途变速。离线边界替身不模拟 CRI 播放缓冲或实际音频设备延迟，这部分仍需在游戏中确认。

独立练习倍率请在 0.50×／1.00×／2.00×及两种音高模式下试听，检查预播放、每轮 5 秒休息、休息暂停、下一轮应用、仅 2P／本机双人及退出后 SpeedHack 恢复。时间伸缩的实际音质、CPU 开销和缓冲延迟不能由离线测试证明。

游戏内验收仍需覆盖：开头／中途／临近结束跳歌、仅 1P 或 2P 入场、本机双人、连续歌曲、暂停／定位／重试／倍速组合，以及结算成绩和判定总数。替换游戏中的 Mod 前请先保留原 DLL；本次工作不会自动写入游戏目录。

## Feature
- Cheat
	- Auto Play
	- Rank-controlled Auto Play (including mid-song takeover)
	- Fast Skip
	- Random Rank Skip (minimum FC / FC+ / AP)
	- Batch Rank Farming (frozen queue, low-level probabilities, CN chunked save and track reset)
	- Chart Controller
	- A/B Loop Practice (per-round judgement and combo reset)
    - Unlock Music
    - Unlock Master
	- Unlock Event
    - Unlock Utage
    - Force Unlock Double Player Utage
    - Speed Hack
	- Force Current ls Best
	- Reset Login Bonus Record
    - Set All Character As Same And Lock
- Common
    - Manual Runtime Settings / Named Presets
    - Configurable Hotkeys (unbound by default)
    - Dummy Login
    - Custom Camera ID
	- Block Coin
	- Custom Version Text
	- Disable Mask
    - Disable Background
	- Forward Touch Region To Button
    - Force Quick Retry
	- Network Logger
	- Print User Information
    - Export User Data
	- Quick Boot (currently disabled)
	- Show FPS
	- Single Player Mode
	- Skip Warning Screen
    - Skip Fade
    - Ignore Information
    - Change Default Option
    - Change Game Settings
- Fix
    - Disable Environment Check(For versions after 1.50)
	- Disable Encryption
    - Disable Ini Clear
	- Disable Reboot
    - Fix Check Auth
	- Fix DebugInput
    - Force As Server
    - Restore Certificate Validation
	- Rewrite Judge Timing
	- Skip Version Check
    - Skip Cake Hash Check
    - Skip SpecialNum Check


## Development

1. Install [.NET Framework 4.7.2 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net472)
2. Configure library. (tips: Please use SDGB version library)
3. Build the solution.
4. Copy `Sinmai-Assist.dll` to `Mods` folder.


## Relevant Links

* [MelonLoader Wiki](https://melonwiki.xyz/#/modders/quickstart)
* [Harmony Docs](https://harmony.pardeike.net/articles/patching-prefix.html)
