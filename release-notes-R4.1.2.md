# RoundedTB Revived R4.1.2

> ⚠️ **这是最后一个使用 .NET 8 构建的版本**。后续版本将迁移到 .NET 10。
> 请据此评估升级计划(自包含版不受系统 runtime 影响,迁移对用户基本无感)。

## 本次改动(相对 R4.1.1)

- **托盘图标彻底修复**:自实现标准 `Shell_NotifyIcon`(不再依赖 WPFUI 的 NotifyIcon,后者在 Win11 26H1 上图标不显示/错误)。修复了初始化(Hidden 窗口不触发事件)、图标资源 URI 大小写等层层根因——本机与 Win11 26H1 均正常显示,并随系统亮暗切换黑白图标。
- **托盘右键菜单**:
  - WPFUI 质感(圆角、hover 高亮、开机自启勾选标记)
  - **按系统亮/暗模式自动配色**
  - **点击菜单外自动关闭**(鼠标移出 + 外部点击)
  - **启动即用**(启动时初始化窗口一次,代价是启动瞬间一闪而过)
- **26H1 OOM 崩溃修复**:`UpdateDynamicTaskbar` 的 GDI region 句柄泄漏(CombineRgn 临时 region 从不释放)→ 长时间运行内存耗尽。已全部释放。
- **CPU 优化**:UIA 内容边界刷新降频(1s)、顶层窗口枚举降频(3s)、UIA 仅动态模式需要。
- **任务管理器结束任务/退出恢复任务栏** + 启动时清理上次残留(尽力修复,个别场景仍可能不完美)。
- **启动不再弹 TranslucentTB 兼容窗口**(恢复配置不再触发弹窗,仅手动勾选时显示说明)。
- **托盘点击循环修复**:左键开设置、再点隐藏,托盘图标不再消失。
- **默认圆角 20 + 动态模式边缘 padding 增大**(防止调大圆角时裁切程序图标)。

## 已知问题

- **动态模式**:新程序图标出现时任务栏可能短暂把图标显示一半,随即恢复(仅视觉,缓解中)。
- **Windows 11 26H1**:已实测可用,偶发闪退(可能伴随 Explorer 崩溃)在排查中;全局未处理异常会写入 `%LOCALAPPDATA%\rtb.log`。
- 多语言处理下部分按钮/场景文字可能显示不完全。
- AutoHide 仍在测试,不保证所有环境正常。
- 圆角无抗锯齿(Windows region 固有限制)。

## 下载(多架构)

### 自包含版(内置 .NET 8 runtime,解压双击 RoundedTB.exe 即用)

- **`RoundedTB-R4.1.2-win-x64.zip`**
- **`RoundedTB-R4.1.2-win-x86.zip`**
- **`RoundedTB-R4.1.2-win-arm64.zip`**

### Framework-dependent 版(体积小,需自行安装 .NET 8 Desktop Runtime)

- **`RoundedTB-R4.1.2-win-x64-framework-dependent.zip`**
- **`RoundedTB-R4.1.2-win-x86-framework-dependent.zip`**
- **`RoundedTB-R4.1.2-win-arm64-framework-dependent.zip`**

**所需运行时**:.NET 8 **Desktop** Runtime
下载地址:https://dotnet.microsoft.com/download/dotnet/8.0
(进入页面选 **Windows → .NET Desktop Runtime 8.0.x**,按架构下载 x64 / x86 / arm64)

> 注:arm32(win-arm)不被 .NET 8 支持,未提供。

## AI 生成代码说明

本项目近期兼容性与稳定性代码由 **DeepSeek V4** 模型(经 Claude Code)协助编写,按 GPL v3 同条款授权,详见 README "AI-generated code" 与 Credits。
