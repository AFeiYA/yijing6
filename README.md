# 易·境 / Yijing

固定视角 2D／2.5D 的治愈系茶舍经营游戏。通过同物二合备茶待客，用人物故事、茶舍变化和每日易经反思卡形成循环。

## 开发基线

- Unity **6.6 / 6000.6.0f1**，C#、URP 2D、Input System、uGUI。
- 当前阶段：M1／M2 首段可玩版。已串通前三单、两条合成链、储物／回收、门灯、每日卦卡与本地存档；完整序章仍在开发。
- 0.4.0 改为茶舍主画面、场景对白、半量备料、放杯倒茶和自由听雨；保留引导与独立练习。参见 [多待一分钟](docs/CALM_EXPERIENCE.md)。
- 当前限制：Unity 本机授权未恢复；新代码已编译、独立规则检查通过，播放模式、声音画面和新 app 构建待完成。现有 app 为 0.3.0。
- 项目根目录就是 Unity 工程目录，包含 `Assets`、`Packages`、`ProjectSettings`。
- [GDD v2.0](docs/Yijing_Unity_GDD_v2.0.md) 规定玩法；[工程决策](docs/ENGINE_DECISION.md) 覆盖其中第 14.1 节的旧引擎版本建议。

## 打开项目

1. Unity Hub → Add → Add project from disk，选择此仓库根目录。
2. 指定 **6000.6.0f1**，等待包还原和资源导入。
3. 打开 `Assets/Yijing/Scenes/Bootstrap.unity`，点击 Play。
4. 配置通过校验后进入茶舍，跟随开场或点击“为她备一席茶”进入茶案。点击茶台生成物品，拖动相同物品合成；也可依次点击来源格和目标格。完成第一次合成后开放器架与静／行选择。

首轮试玩步骤和预期结果见 [试玩说明](docs/PLAYTEST_M1.md)。Game 场景已经装配好；重新装配可使用 **Yijing → Playable → Prepare Scene**。macOS 试玩包可通过 **Yijing → Playable → Build macOS Preview** 构建至 `Artifacts/Build/Yijing.app`。

已玩过的存档可通过右上角 **引导 → 从头练习 · 不影响当前进度** 重走教程。设计依据与故事结构见 [故事和新手引导](docs/STORY_ONBOARDING.md)。

`Bootstrap` 与 `Game` 已加入构建场景。不要从空场景判断游戏启动是否正常。

## 修改配置

`docs/prototype_config.json` 是现阶段策划配置的唯一编辑入口。修改后执行 **Yijing → Configuration → Import and Validate**，生成 `Assets/Yijing/Data/PrototypeConfig.asset`。两者应在同一次提交中更新。

校验覆盖物品升级链、产出引用、订单等价成本、奖励、故事与建设依赖、八张原型卦卡映射。导入资产记录源文件 SHA-256；启动时使用独立快照，不把玩家状态写回配置资产。当前是原型导入器，后续按 GDD 拆成专用配置资产和本地化表。

## 检查

先关闭占用此工程的 Unity 编辑器，然后在项目根目录运行：

```sh
bash scripts/unity-check.sh validate
bash scripts/unity-check.sh editmode
bash scripts/unity-check.sh playmode
```

其他安装路径通过 `YIJING_UNITY_EDITOR` 指定。报告位于 `Artifacts/`，不提交。编辑器内也可使用 Test Runner。Domain 程序集禁止引用 UnityEngine，便于规则独立验证。

无需 Unity 播放模式的规则检查（需要 .NET 8 SDK）可以运行 `dotnet run --project scripts/DomainCheck/DomainCheck.csproj -- .`，覆盖八种首幕路线、资金可达性、失败回滚和旧余额。它不替代 Unity 序列化、触控、画面和声音验收。

## 目录

| 路径 | 用途 |
| --- | --- |
| `Assets/Yijing/Scripts/Domain` | 普通 C# 规则与配置校验 |
| `Assets/Yijing/Scripts/Application` | 启动和后续用例编排 |
| `Assets/Yijing/Scripts/Infrastructure` | 配置资产、原子 JSON 存档及备份恢复 |
| `Assets/Yijing/Scripts/Presentation` | 茶案界面、指针交互、山房与每日手账 |
| `Assets/Yijing/Scripts/Editor` | 配置导入和工程工具 |
| `Assets/Yijing/Scenes` | Bootstrap、Game |
| `Assets/Yijing/Tests` | EditMode 与 PlayMode 检查 |
| `Assets/Settings` | 官方 2D 模板的 URP 与输入设置 |
| `docs` | 策划、配表、工程决策、阶段清单 |
| `scripts` | 本地检查入口 |

## Git 约定

提交 `Assets` 及 `.meta`、`Packages/manifest.json` 和 `packages-lock.json`、`ProjectSettings`、文档和脚本。缓存、构建包、IDE 设置、签名和密钥均不提交。场景和资产使用文本序列化；移动 Unity 资源时同时移动对应 `.meta`，最好在 Unity 中操作。

初始同步到 `main`；后续功能使用 `feature/<name>` 分支，完成相关验证后再合并。当前未配置云端 Unity 构建或 GitHub 分支保护。

## 下一阶段

见 [开发清单](docs/DEVELOPMENT_PLAN.md)。先人工验证当前三单循环的操作手感，再扩展 Elena 完整十八单、其余修葺和解锁、声音、英文和卡片导出。

初始化时本机已装 Mac、WebGL 模块，未装 Android、iOS Build Support。移动构建、签名和真机验证仍需完成；包标识 `com.afeiya.yijing` 暂用于开发，正式上架前确认。

## 首批美术资源

已生成 15 张 PNG 并建立 `Assets/Yijing/Data/ArtCatalog.asset`。在 Unity 使用 **Yijing → Art → Open Asset Preview** 浏览；资源清单、提示词和使用边界见 [美术说明](docs/art/README.md)。可直接打开 [浏览器预览](docs/art/preview.html)。物品、头像、山房与三张卡底均已用于可玩场景；独立修葺分层还未制作。
