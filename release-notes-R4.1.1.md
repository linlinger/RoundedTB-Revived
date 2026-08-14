# RoundedTB Revived R4.1.1(Master 正式版)

## 本次改动(相对 R4.1)

- **构建通道**:引入 Canary / Dev / Master 三通道构建选项(`-p:Channel=...` 或 `build.bat [canary|dev|master]`)。本版本为 **Master(正式版)** 标识——正式图标、正式横幅、About 副标题 "R4.1",不再显示 Canary。
- **开机自启项名称**:任务管理器"启动"页显示的是快捷方式文件名,已改用 `RoundedTB Revived.lnk`(并自动迁移旧的 `RoundedTB.lnk`)——现在启动项显示 "RoundedTB Revived",不再是 "RoundedTB"。
- 修复三通道构建的启动崩溃(XAML 图标类型转换 + 资源声明冲突)。
- 日志策略:正式版(Master)精简高频诊断日志,Canary/Dev 保留完整日志。

## 已知问题

- **动态模式**:新程序图标出现时,任务栏可能短暂把图标显示一半,随即恢复(仅视觉,**下个版本修复**)。
- **Windows 11 26H1**:已实测可用,但**偶发闪退**(可能伴随 Explorer 崩溃),暂未稳定复现,正在排查中。
- 由于多语言处理方式,部分按钮和场景下的文字可能显示不完全。
- AutoHide 仍在测试中,不保证所有环境下都能正常工作。

## 下载(多架构)

### 自包含版(内置 .NET runtime,解压双击 RoundedTB.exe 即用)

- **`RoundedTB-R4.1.1-win-x64.zip`**(77 MB)— x64 推荐
- **`RoundedTB-R4.1.1-win-x86.zip`**(72 MB)— 32 位
- **`RoundedTB-R4.1.1-win-arm64.zip`**(73 MB)— Arm64

### Framework-dependent 版(体积小,需自行安装 .NET 8 Desktop Runtime)

- **`RoundedTB-R4.1.1-win-x64-framework-dependent.zip`**(16 MB)
- **`RoundedTB-R4.1.1-win-x86-framework-dependent.zip`**(16 MB)
- **`RoundedTB-R4.1.1-win-arm64-framework-dependent.zip`**(16 MB)

**所需运行时**:.NET 8 **Desktop** Runtime
下载地址:https://dotnet.microsoft.com/download/dotnet/8.0
(进入页面选 **Windows → .NET Desktop Runtime 8.0.x**,按架构下载 x64 / x86 / arm64 对应安装包)

> 注:arm32(win-arm)不被 .NET 8 支持(NETSDK1083),未提供。

## AI 生成代码说明

本项目近期兼容性与稳定性代码由 **DeepSeek V4** 模型(经 Claude Code)协助编写,按 GPL v3 同条款授权,详见 README "AI-generated code" 与 Credits。
