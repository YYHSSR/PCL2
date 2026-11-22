<div align="center">

<h1>PCL-YYH</h1>

<p><b>基于 PCL2 的个人游玩分支 · 更轻、更安静，只为自己的整合包服务。</b></p>

<p>Windows x64 · .NET Framework 4.8 · VB.NET / WPF · 非官方分支</p>

[![Upstream](https://img.shields.io/badge/upstream-PCL%202.14.0.0-2F80ED?style=flat-square)](https://github.com/Meloong-Git/PCL)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D4?style=flat-square&logo=windows&logoColor=white)](https://github.com/YYHSSR/PCL2)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-Non--Commercial%20%7C%20ShareAlike-8B5CF6?style=flat-square)](LICENSE)
[![Fork](https://img.shields.io/badge/type-personal%20play%20fork-F59E0B?style=flat-square)](https://github.com/YYHSSR/PCL2)

[介绍](#这是什么) · [快速开始](#快速开始) · [改动](#本分支做了什么) · [构建](#构建) · [来源与贡献者](#来源与贡献者) · [许可](#许可与上游协议)

</div>

## 这是什么

PCL-YYH 是 YYH 基于龙腾猫跃（LTCat）的 PCL2 源代码制作的个人游玩分支，不是 PCL 官方版本，也未获得官方认可或背书。

PCL-YYH 是面向个人使用的 Minecraft: Java Edition 启动器分支，基于 **PCL 2.14.0.0** 源码继续开发。

它保留了 PCL2 的启动、下载、实例管理、Java 检测、账号登录、Mod 管理与整合包安装能力，同时做减法：固定简体中文、精简顶栏、移除联机与个性化、关闭自动更新与遥测，并把下载引擎和界面性能收紧。

目标很简单：**打开就能玩自己的整合包，少一点干扰，少一点后台动静。**

## 来源与贡献者

PCL-YYH 不是从零开始的项目。它的启动、下载、实例、Java、Mod 与整合包能力来自上游 PCL2；本分支只在此基础上做个人化裁剪和修复。

| 角色 | 贡献 |
|:---|:---|
| **龙腾猫跃（LTCat）** | PCL / PCL2 原作者；上游代码与设计的作者 |
| **PCL / MeloongCore 贡献者** | 上游启动器与基础库的开发者、测试者、文档与资源贡献者 |
| **YYH** | 本分支维护者：方向、功能取舍、测试与发布 |
| **Claude（Anthropic）** | AI 协作：代码审查、下载引擎性能与并发修复 |
| **ChatGPT / Codex（OpenAI）** | AI 协作：实现与问题排查 |

上游项目：[PCL](https://github.com/Meloong-Git/PCL) · [MeloongCore](https://github.com/Meloong-Git/MeloongCore)

> 上游作者与贡献者始终是本项目的前置来源。PCL-YYH 与龙腾猫跃、PCL 团队、Anthropic、OpenAI 均无隶属或官方背书关系；AI 工具仅作为开发协作致谢，不作为著作权主体。

## 快速开始

仓库根目录的 `PCL-YYH.exe` 是打包好的启动器。双击运行即可，无需解压，也不需要在旁边放任何项目 DLL。内置资源按需释放，缓存和配置在运行时生成。

| 项目 | 说明 |
|:---|:---|
| 系统 | 64 位 Windows，需 .NET Framework 4.8 |
| 游戏 | 与目标 Minecraft 版本兼容的 Java；启动器可自动检测和管理 Java |
| 微软登录 | 为启动器进程设置你自己的 `PCL_MS_CLIENT_ID` 环境变量后重启 |
| CurseForge | 在「设置 → 其他 → 社区资源」保存你自己的 API Key，或设置 `PCL_CURSEFORGE_API_KEY`（环境变量优先）。保存的 Key 使用 DPAPI 加密。Modrinth 无需 Key |
| 配置 | 使用 `PCL2Local` 标识；账号凭据通过 DPAPI 绑定到当前 Windows 用户 |

> 不要在公共电脑上保存微软账号。把程序复制给其他 Windows 用户时，账号凭据不会跟着迁移，也无法被对方解密。

## 本分支做了什么

| 方向 | 改动 |
|:---|:---|
| **界面** | 固定为简体中文，没有语言切换；顶栏只保留「启动」「下载」「设置」；移除「更多」页（帮助、关于、反馈、投票、百宝箱），上游署名与链接集中放在 README 顶部和许可证中 |
| **联机** | 完全移除联机（Link）：相关页面、设置、邀请码处理以及「加入房间」自定义事件 |
| **个性化** | 移除主题切换、自定义主页、背景图片、背景音乐和功能隐藏，界面使用固定样式 |
| **设置** | 「其他」设置页只保留「下载」和「社区资源」；移除无障碍、启动器与调试选项、自动更新、公告、设置导入导出、遥测和愚人节功能 |
| **安全** | 移除「始终校验证书」开关；HTTPS 始终使用 .NET 默认的证书校验 |
| **性能** | 收紧下载队列、动画、列表刷新、内存回收与计时器释放，详见下一节 |

## Minecraft 1.20.1 + Forge

优先使用 64 位 Java 17。全局 Java 列表按兼容性排序，实例设置中强制指定的 Java 优先。默认开启 G1；G1 停顿目标调优、堆预触和字符串去重默认关闭。你手动保存过的选项不会因升级被重置。

24 GB 内存的电脑，中等规模 Mod 建议先分配约 6 GB，重型整合包可按实际情况尝试 8 GB，并给 Windows 和游戏的堆外内存留出余量。内存给得越多不代表越流畅。

## 性能与稳定性

- 动画跟随渲染帧运行，空闲时停止；列表视口检测批量进行；内存设置页隐藏时停止刷新；Mod 数量来自后台快照。
- 下载管理线程在没有任务时休眠，有新任务时才唤醒，不再空转轮询。
- 下载读取的超时计时器在读取完成后立即释放，大文件下载不会堆积计时器。
- 已结束的下载任务会被及时清理，长时间浏览资源页不再导致内存持续增长。
- 限速额度最多累积 1 秒，空闲后恢复下载不会突破限速；下载管理器只会启动一次。
- 启动时获取的 Minecraft 版本列表会被下载页复用，不再重复请求。

## 目录结构

| 路径 | 内容 |
|:---|:---|
| `SKILL/` | 开发约定 |
| `src/Plain Craft Launcher 2/` | 主程序（VB.NET / WPF） |
| `src/PCLCS/` | C# 业务模型与辅助代码 |
| `src/MeloongCore/` | 基础库源码 |
| `src/Tests/` | 冒烟测试与性能工具 |
| `src/Plain Craft Launcher 2.sln` | 解决方案 |
| `src/build.ps1` | 构建入口 |
| `ysm/` | YSM（Yes Steve Model）玩家模型文件 |
| `PCL-YYH.exe` | 打包好的启动器 |
| `README.md` / `LICENSE` | 文档与许可 |

## 构建

安装 .NET 9 SDK，在仓库根目录运行：

```powershell
& ./src/build.ps1 -Configuration Release -RunTests -LaunchCheck
```

| 参数 | 作用 |
|:---|:---|
| `-DotnetPath` | 指定 `dotnet.exe`，例如便携安装的 `E:\dotnet-sdk\dotnet.exe` |
| `-RunTests` | 运行冒烟测试 |
| `-LaunchCheck` | 在隔离的配置下启动构建出的 EXE；窗口未出现或几秒内退出即判定失败 |

主程序目标框架为 .NET Framework 4.8，引用程序集和其他 NuGet 包会自动还原，开发工具不需要分发给最终用户。

构建产物位于 `src/Plain Craft Launcher 2/bin/`。分发时复制主 EXE 并重命名为 `PCL-YYH.exe`。所有项目依赖都已嵌入 EXE，外部只依赖系统的 .NET Framework 和游戏用 Java。

## 许可与上游协议

PCL-YYH **不是**一个重新授权整个 PCL2 的项目。它分成三层：

| 内容 | 适用协议 |
|:---|:---|
| 上游 PCL / PCL2 源代码、文档与资源 | [《PCL 分发有限许可》](https://shimo.im/docs/rGrd8pY8xWkt6ryW#anchor-X0bo) + [《PCL 存储库合理使用指南》](https://github.com/Meloong-Git/PCL/blob/5ef81e490ae46b1100d61e33d0183094c7a80ca8/LICENCE) |
| MeloongCore 基础库 | [Apache License 2.0](https://github.com/Meloong-Git/MeloongCore/blob/c774ae4c85fd94e0eaa171db21e78a65870df19b/LICENSE.txt) |
| YYH 新增或修改的内容 | **PCL-YYH 个人游玩分支许可协议 1.0**（非商业、署名、相同方式共享） |
| Minecraft 与 Mojang / Microsoft 相关内容 | 各自的权利与条款 |

- 本分支是第三方个人修改版，**不是 PCL 官方版本**，也未获得官方认可或背书。
- 上游代码仍归龙腾猫跃与 PCL / MeloongCore 贡献者所有；本分支不改变其归属，也不授予任何商业权利。
- 使用、修改、分发本分支时，必须**同时**遵守本许可证和所有适用的上游/第三方协议；冲突时，就上游内容以上游协议为准。
- 再分发时必须保留龙腾猫跃署名、PCL 项目链接、本许可证与上游指南，公开源代码，并明确标注修改。
- 商业使用需同时获得 YYH 与相关上游权利人的书面许可；**YYH 无权就上游内容授予商业许可**。

完整条款见 [LICENSE](LICENSE)。

### 推荐署名格式

```text
PCL-YYH 是 YYH 基于龙腾猫跃（LTCat）的 PCL2 源代码制作的个人游玩分支，非 PCL 官方版本。
上游：https://github.com/Meloong-Git/PCL
本分支：https://github.com/YYHSSR/PCL2
```

## 免责声明

本项目不是 Minecraft 官方产品，与 Mojang 或 Microsoft 无关，也未获得其认可。

启动器会写入配置、下载资源、管理 Java / Mod / 整合包；请在使用前备份重要数据，使用自己的正版账号，并自行承担使用风险。软件按“现状”提供，不附带任何明示或暗示的担保。

---

<div align="center">

如果这个分支让你少折腾一点，请给上游 [PCL](https://github.com/Meloong-Git/PCL) 点个 Star。

Made for personal use by [YYHSSR](https://github.com/YYHSSR)

</div>
