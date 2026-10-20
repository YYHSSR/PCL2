# PCL YYH

本分支作者 / 维护者：**YYH**。

面向 Windows 的 Minecraft Java 版启动器，提供实例管理、Java 检测、账号登录、资源下载、Mod 管理与整合包安装等功能。本分支基于 PCL 2.14.0.0 源码继续开发。

## 使用

运行 `PCL-YYH.exe` 即可，无需解压 ZIP 或另外放置项目 DLL。程序运行时会按需释放内置资源并生成缓存和配置。

- 系统：64 位 Windows，需安装 .NET Framework 4.8。
- 游戏：需要与目标 Minecraft 版本兼容的 Java；启动器可检测和管理 Java。
- 微软登录：为启动器进程设置自己的 `PCL_MS_CLIENT_ID` 后重新启动。
- CurseForge：在“设置 → 其他 → 社区资源”保存自己的有效 API Key，或设置 `PCL_CURSEFORGE_API_KEY`；环境变量优先。本地 Key 通过 DPAPI 加密保存；未配置时可使用 Modrinth。
- 启动器界面固定为简体中文，不提供语言切换。
- 顶部仅保留启动、下载和设置；已移除“更多”及帮助、关于、反馈、投票和百宝箱相关功能。
- 已移除个性化页面及主题切换、自定义主页、背景图片、背景音乐和功能隐藏；界面使用固定样式。
- 设置的“其他”页仅保留下载、社区资源；已移除辅助功能、启动器和调试选项，自动更新、公告、设置导入导出及遥测代码一并清理。联机服务未配置。

默认配置使用 `PCL2Local` 标识，凭据由 Windows 当前用户范围的 DPAPI 保护。复制程序不会自动迁移其他 Windows 用户的账号凭据。

## Minecraft 1.20.1 + Forge

优先使用 64 位 Java 17；全局 Java 列表按兼容性与排序选择，版本独立设置中的强制选择优先。G1 默认开启，G1 停顿目标调优、预触碰内存和字符串去重默认关闭。现有手动保存的选项不会因升级被重置。

24 GB 内存的电脑可先给中等模组配置约 6 GB，较重整合包再根据实际占用试用 8 GB；同时给 Windows 和游戏堆外内存留余量。分配更多内存并不保证更流畅。

高级选项的适用条件、修复内容及验证范围见 [本次审查记录](docs/REVIEW-2026-10-20.md)。

## 目录

```text
SKILL/                         开发约定
src/Plain Craft Launcher 2/    VB.NET / WPF 主程序
src/PCLCS/                     C# 业务模型与辅助代码
src/MeloongCore/               基础库源码
src/Tests/                     回归检查
src/Plain Craft Launcher 2.sln 解决方案
src/build.ps1                 构建入口
README.md                     项目说明
LICENSE                       YYH 非商业二次开发许可
```

## 构建

安装 .NET 9 SDK，在项目目录执行：

```powershell
& ./src/build.ps1 -Configuration Release -RunTests
```

可以通过 `-DotnetPath` 指定 dotnet.exe。主程序保持 .NET Framework 4.8 目标，构建所需引用程序集及其他 NuGet 包会自动还原。开发工具不需要分发给最终用户。

构建结果位于 `src/Plain Craft Launcher 2/bin/`。分发时可将其中主程序 EXE 复制并命名为 `PCL-YYH.exe`；所需项目依赖已内置到 EXE，系统 .NET Framework 与游戏 Java 仍是外部运行环境。

## 许可

YYH 的新增与修改部分允许学习、非商业使用、修改和继续开发；**禁止未经授权的商业使用**。再次分发时须保留 YYH 署名、修改说明和本许可，并不得将这些修改改为允许商用的许可。完整条款见 [LICENSE](LICENSE)。

PCL 原有源码与第三方组件仍分别受其原有授权约束，YYH 的许可不改变这些部分的权利归属，也不额外授予其商业使用权。上游基线许可：[PCL 许可文件](https://github.com/Meloong-Git/PCL/blob/5ef81e490ae46b1100d61e33d0183094c7a80ca8/LICENCE)；基础库许可：[MeloongCore 许可文件](https://github.com/Meloong-Git/MeloongCore/blob/c774ae4c85fd94e0eaa171db21e78a65870df19b/LICENSE.txt)。

本项目不是 Minecraft 官方产品，与 Mojang 或 Microsoft 不存在从属或官方合作关系。
