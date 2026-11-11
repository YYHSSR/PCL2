# PCL YYH

> **这是 YYH 基于官方 PCL2 精简、修改的个人自用版本，并非 PCL 官方发布。**
> 禁止商用；允许非商业的修改与二次开发，详见[许可协议](#许可协议)。

面向 Windows 的 Minecraft: Java Edition 启动器，提供实例管理、Java 检测、账号登录、资源下载、Mod 管理和整合包安装。本分支基于 PCL 2.14.0.0 源码继续开发。

## 使用

直接运行 `PCL-YYH.exe` 即可：无需解压，也不用在旁边放任何项目 DLL。内置资源按需释放，缓存和配置文件在运行时生成。

| 项目 | 说明 |
| :--- | :--- |
| 系统 | 64 位 Windows，需 .NET Framework 4.8 |
| 游戏 | 与目标 Minecraft 版本兼容的 Java；启动器可自动检测和管理 Java |
| 微软登录 | 为启动器进程设置你自己的 `PCL_MS_CLIENT_ID` 环境变量后重启 |
| CurseForge | 在「设置 → 其他 → 社区资源」保存你自己的 API Key，或设置 `PCL_CURSEFORGE_API_KEY`（环境变量优先）。保存的 Key 用 DPAPI 加密。Modrinth 无需 Key |

配置使用 `PCL2Local` 标识，账号凭据通过 DPAPI 绑定到当前 Windows 用户。把程序复制给其他 Windows 用户时，账号凭据不会跟着迁移。

## 与官方 PCL2 的区别

- 界面固定为简体中文，没有语言切换。
- 顶栏只保留「启动」「下载」「设置」；移除了「更多」页（帮助、关于、反馈、投票、百宝箱）。
- 完全移除联机（Link）：相关页面、设置、邀请码处理以及「加入房间」自定义事件。
- 移除个性化：主题切换、自定义主页、背景图片、背景音乐和功能隐藏，界面使用固定样式。
- 「其他」设置页只保留「下载」和「社区资源」；移除无障碍、启动器与调试选项、自动更新、公告、设置导入导出、遥测和愚人节功能。
- 移除「始终校验证书」开关，HTTPS 始终使用 .NET 默认的证书校验。

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

```text
SKILL/                          开发约定
src/Plain Craft Launcher 2/     主程序（VB.NET / WPF）
src/PCLCS/                      C# 业务模型与辅助代码
src/MeloongCore/                基础库源码
src/Tests/                      冒烟测试与性能工具
src/Plain Craft Launcher 2.sln  解决方案
src/build.ps1                   构建入口
PCL-YYH.exe                     打包好的启动器
README.md                       本文件
LICENSE                         YYH 非商业衍生许可协议
```

## 构建

安装 .NET 9 SDK，在仓库根目录运行：

```powershell
& ./src/build.ps1 -Configuration Release -RunTests -LaunchCheck
```

| 参数 | 作用 |
| :--- | :--- |
| `-DotnetPath` | 指定 `dotnet.exe`，例如便携安装的 `E:\dotnet-sdk\dotnet.exe` |
| `-RunTests` | 运行冒烟测试 |
| `-LaunchCheck` | 在隔离的配置下启动构建出的 EXE；窗口未出现或几秒内退出即判定失败 |

主程序目标框架为 .NET Framework 4.8，引用程序集和其他 NuGet 包会自动还原，开发工具不需要分发给最终用户。

构建产物位于 `src/Plain Craft Launcher 2/bin/`。分发时复制主 EXE 并重命名为 `PCL-YYH.exe`。所有项目依赖都已嵌入 EXE，外部只依赖系统的 .NET Framework 和游戏用的 Java。

## 贡献者

| 贡献者 | 角色 |
| :--- | :--- |
| **YYH** | 维护者：分支方向、功能取舍、测试与发布 |
| **[Claude](https://www.anthropic.com/claude)**（Anthropic） | AI 协作开发：代码审查、下载引擎的性能与并发修复 |
| **[ChatGPT](https://openai.com/chatgpt)**（OpenAI） | AI 协作开发 |

同时感谢官方 [PCL](https://github.com/Meloong-Git/PCL) 及 [MeloongCore](https://github.com/Meloong-Git/MeloongCore) 的作者与贡献者，本项目建立在他们的工作之上。

## 许可协议

YYH 的新增和修改内容允许学习、非商业使用、修改和二次开发，**未经授权禁止商用**。再分发时须保留 YYH 的署名、修改说明和本许可协议，且不得以允许商用的条款重新授权这些修改。完整条款见 [LICENSE](LICENSE)。

官方 PCL 源码和第三方组件仍遵循各自的许可协议。YYH 的许可协议不改变其归属，也不授予任何商业权利。上游许可：[PCL 许可协议](https://github.com/Meloong-Git/PCL/blob/5ef81e490ae46b1100d61e33d0183094c7a80ca8/LICENCE)；基础库许可：[MeloongCore 许可协议](https://github.com/Meloong-Git/MeloongCore/blob/c774ae4c85fd94e0eaa171db21e78a65870df19b/LICENSE.txt)。

本项目不是 Minecraft 官方产品，与 Mojang 或 Microsoft 无关，也未获得其认可。
