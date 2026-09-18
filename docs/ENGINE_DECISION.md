# 工程决策 001：Unity 6.6 开发基线

日期：2026-09-19。状态：已采用，依据项目所有者明确选择。

使用本机 Unity **6000.6.0f1（6.6）**。这项决定覆盖 GDD v2.0 第 14.1 节建议的 Unity 6.3 LTS；旧版 Word 和原始 PDF 保留为设计历史，版本选择以本记录和 `ProjectSettings/ProjectVersion.txt` 为准。

工程从该编辑器自带的 **2D Cross Platform 7.0.0** 模板提取 Assets、Packages、ProjectSettings，保留 URP 2D 渲染设置，删除模板欢迎页和示例场景。按模板版本使用 URP 17.6.0、Input System 1.19.0、uGUI 2.6.0、Test Framework 1.8.0；实际依赖解析由提交的包锁文件记录。

暂不加入协作服务、Visual Scripting、Timeline、2D 骨骼动画、PSD/Aseprite 导入等未使用依赖。需要时按功能引入并检查兼容性，不批量升级编辑器与所有包。

目标为竖屏移动端；桌面窗口初始 540 × 960。当前 Game 只有正交相机与 Global Light 2D，正式 UI、安全区、拖拽和触控属于 M1。建立 Bootstrap → Game 启动链，配置在进入 Game 前校验。

没有 Android/iOS 模块时不宣称移动端已构建。安装构建模块、真机准备和 iOS 签名要在 M0 验收中补齐。

配置源当前为 `docs/prototype_config.json`，编辑器导入为 ScriptableObject。数据结构和校验属于纯 C# Domain；配置资产只承载设计数据，存档在后续阶段另建。此阶段不引入云服务、遥测 SDK、支付 SDK 或在线账户。

参考：[Unity 6.6 命令行](https://docs.unity3d.com/6000.6/Documentation/Manual/EditorCommandLineArguments.html)、[版本管理](https://docs.unity3d.com/6000.6/Documentation/Manual/Versioncontrolintegration.html)。包版本直接取自本机官方模板。
