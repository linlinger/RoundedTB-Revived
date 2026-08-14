# RoundedTB Revived

#### 给你的任务栏添加边距、圆角和分段!

[English](./README.md) | **简体中文**

RoundedTB Revived 是 RoundedTB 的社区维护续作(RoundedTB 原作者为 torchgm,采用 GNU 通用公共许可证 v3.0)。它为 Windows 10 / 11 的任务栏添加边距、圆角和可配置的分段。

![image](https://user-images.githubusercontent.com/31840547/134795141-76349eaf-12da-40f8-b2a0-d7b7c268d152.png)

## 如何获取

从 [Releases](https://github.com/linlinger/RoundedTB-Revived/releases) 下载最新版本,解压后运行 `RoundedTB.exe`。你也可以自己从源码编译(运行 `build.bat`)。

## 持续构建(GitHub Actions)

每次推送到本仓库都会触发 [GitHub Actions](https://github.com/linlinger/RoundedTB-Revived/actions) 的 Windows Release 构建。产物以 `rtb-artifacts` 上传到该次运行的页面——想测试最新提交又不想本地编译,直接下载即可。详见 `.github/workflows/ci.yml`。

## 使用方法

### 基础选项
最简单的用法是直接输入边距和圆角半径。
 - **边距** - 控制从任务栏四周各移除多少像素,形成一圈可见、可点击穿透的边距。
 - **圆角半径** - 控制任务栏圆角的圆润程度。

### 高级选项
高级选项提供更多自定义能力,但牺牲了一些易用性。
- **独立边距** - 在高级设置中,边距输入框旁会出现一个 <kbd>...</kbd> 按钮。点击后可为任务栏每一边单独指定边距。你也可以使用负值隐藏某些边的圆角,让任务栏"贴"到显示器的不同边缘。
- **动态模式(Windows 11)** - 动态模式会根据任务栏中的图标数量自动调整任务栏大小,让任务栏更像 macOS 的 Dock。
- **拆分模式(Windows 10)** - 拆分模式是动态模式在 Windows 10 上的简化版本。由于任务栏限制较多,无法动态调整大小;但经过一些设置后,拆分模式可以把任务栏与系统托盘分开,并按需调整大小。设置方法见本文档底部。
- **显示系统托盘** - 切换动态/拆分模式下是否显示系统托盘、时钟等。可随时按 <kbd>Win</kbd>+<kbd>F2</kbd> 切换。
- **TranslucentTB 兼容** - 由于 Windows 的某个 Bug,修改任务栏外观的应用不会自动让 RoundedTB Revived 的改动生效。启用此选项可与 [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB)(当前 v4 版本;旧版"需要 2021.5"的说明已不适用)配合使用。注意:在 Windows 10 上,同时使用 AutoHide 与 TranslucentTB 时任务栏淡出可能闪烁(Explorer 会重组任务栏);建议不要同时使用 AutoHide 与 TranslucentTB。
- **关于 RoundedTB Revived** - 提供当前版本信息。"调试"部分可以打开配置文件和日志文件。

## 支持的系统

- **Windows 11** 22H2+ — 动态模式;已在 24H2(build 26100)验证。Windows 11 26H1 预期可用,待实测确认。
- **Windows 10**(1607+) — 拆分模式。

## 已知问题
 - **自动隐藏(AutoHide)**:请使用内置的 AutoHide 选项。若同时开启 Windows 自带的自动隐藏可能导致闪烁。AutoHide 目前仍在测试中,不保证在所有环境下都能正常工作。
 - 由于当前的多语言处理方式,部分按钮和场景下的文字可能显示不完全。
 - 圆角没有抗锯齿,这是 Windows 的限制。
 - 动态/拆分模式仅当任务栏位于屏幕顶部或底部(水平放置)时正常工作。
 - Windows 10 的拆分模式仅支持主任务栏,副任务栏不会拆分。
 - 除 TranslucentTB 外的任务栏修改工具兼容性不保证。

## AI 生成代码说明

本项目是 RoundedTB 的社区维护续作。近期代码中相当一部分——Windows 11 22H2+/24H2 兼容性修复、本地化(i18n)、设置健壮性和后台循环恢复——由 AI(**DeepSeek V4** 模型,通过 Claude Code)协助编写。任何 AI 协助代码均按与项目其余部分相同的 **GNU GPL v3** 条款发布。

## 致谢

- **torchgm** — [RoundedTB](https://github.com/torchgm/RoundedTB) 原作者(GPL v3)。
- **gniang**([Gniang/RoundedTB](https://github.com/Gniang/RoundedTB)) — 本版本中的多项稳定性修复移植自其 fork:副任务栏 tray 几何、悬停状态持久化、配置原子写入与旧配置迁移、后台循环恢复、Explorer 重启退避。感谢出色的工作!
- **DeepSeek(V4)** — 协助完成兼容性与稳定性工作的模型(见上方"AI 生成代码说明")。

## 其他信息
如果出现严重故障,按 <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>Esc</kbd> 打开任务管理器,结束 RoundedTB Revived,然后重启 Explorer。最坏情况下重启电脑即可。RoundedTB Revived 不会做永久更改(除非你从托盘开启开机自启),所以重启应该能清除所有问题。

Bug 报告和功能请求可以通过本仓库的 issue 跟踪器提交(请附上 `%LOCALAPPDATA%\rtb.log` 日志,详见 issue 模板)。

### 在 Windows 10 上配置拆分模式
拆分模式有一些限制,需要少量设置才能正常工作。
#### 限制
- 拆分模式不会自动调整大小。该功能未来会提供给 Windows 10 的 RoundedTB Revived。
- 拆分模式目前与工具栏不兼容,除一个外需全部禁用(工具栏用于标记任务栏的"空白"区域)。
- 拆分模式仅当任务栏水平位于屏幕顶部或底部、且在主显示器上时有效。
#### 设置
1. 右键任务栏,关闭"锁定任务栏"。
2. 再次右键,关闭所有现有工具栏。
3. 第三次右键,选择 工具栏 > 桌面。
4. 用小的 <kbd>||</kbd> 手柄按你的喜好调整任务栏大小。

观看以下视频了解拆分模式设置指南:

https://user-images.githubusercontent.com/31840547/134795022-1312d011-40f2-4641-8c8d-3d6c0e752747.mp4
