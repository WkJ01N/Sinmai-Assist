# Sinmai-Assist
Mods for maimai DX (Sinmai.exe). Patch with MelonLoader.  

This is a cheat Mod, using by your own risk.

> [!WARNING] 
> Please use MelonLoader version 0.6.4 or lower. 
> Higher versions will crash.

## 此 fork 的使用说明

原作者：WYH2004 (Slim) & Error063。此 fork 修改者：**Modified by WkJ01N**；DLL 加载信息、启动横幅与作者相关元数据保留原作者并附加此修改者署名。

本分支保留原项目功能，并加入倍速、配置恢复与随机评级跳歌。当前适配检查以 **SDGB 25500（SDGB156E）** 的本地程序集为基准；其他游戏版本需要重新验证。下述新增功能已通过构建及离线测试，仍需要游戏内验收。

### 随机评级跳歌

1. 在游戏目录的 `Sinmai-Assist/Config.yml` 中设置 `cheat.fastSkip: true`，然后重启游戏。
2. 按 **Backspace** 显示面板，进入 **FastSkip**，选择 **Random**。
3. 选择目标评级（D～SSS+），以及最低连击条件 **Any / FC / FC+ / AP**。
4. 在歌曲播放期间点击 **Skip** 或按 **空格**。

评级范围来自游戏当前加载的数据。例如通常 SS 对应 `[99.0000%, 99.5000%)`，SSS 对应 `[100.0000%, 100.5000%)`；最高评级包含谱面的理论上限。面板中的分数指**达成率百分比**，不是 DX 分数。

跳歌会重新生成**整首谱面**的判定，替换已经打出的部分。本机双人共享所选条件，但各自独立生成。无需启用 AutoPlay，正常播放时的随机自动游玩概率也不受此设置影响。

- **Any**：允许 Critical、Perfect、Great、Good 和 Miss。
- **FC**：不出现 Miss；允许最终获得 FC+、AP 或 AP+。
- **FC+**：不出现 Good、Miss；允许最终获得 AP 或 AP+。
- **AP**：只出现 Perfect 及以上；允许最终获得 AP+。

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

旧配置无需迁移；缺失配置使用默认值。每次启动仍默认选择 Default 跳歌模式。面板修改仅在本次运行生效，不自动写回 YAML。

### 倍速与快捷键

在 Debug 面板启用 SpeedHack；小键盘 `+` / `-` / `*` 分别增加 0.1 倍、减少 0.1 倍和重置为 1 倍，按键可在 `KeyBindConfig.yml` 的 `speedHack` 节点修改。Enable Global Speed 控制非谱面阶段的全局倍速。关闭或退出时只恢复本功能接管过的全局时间倍率。

自动游玩快捷键可在隐藏面板时使用：F 为 Critical，G 为随机 AP，H 为随机 FC+，J 为随机 FC，K 为完全随机，O 为 Great，P 为 Good，N 为关闭。随机模式保持原有概率，不保证正常播放结束时的评级。

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

本地既有日志还显示 AquaMai 的 `PacketUpsertUserAll` 构造函数适配失败；该问题来自外部 Mod，未在本次修改中调整。Quick Boot 在当前入口中因资源加载问题保持禁用。

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

游戏内验收仍需覆盖：开头／中途／临近结束跳歌、仅 1P 或 2P 入场、本机双人、连续歌曲、暂停／定位／重试／倍速组合，以及结算成绩和判定总数。替换游戏中的 Mod 前请先保留原 DLL；本次工作不会自动写入游戏目录。

## Feature
- Cheat
	- Auto Play
	- Fast Skip
	- Random Rank Skip (minimum FC / FC+ / AP)
	- Chart Controller
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
