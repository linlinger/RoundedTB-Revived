# RoundedTB Revived — 项目进度与维护记录

> 维护者:接续原项目(torchgm / RoundedTB,GPL v3)。详细的原版分析见
> `ANALYSIS-R3.1-to-HEAD.md`。本文件是交接文档,记录已完成修复、已知问题、
> 待移植的外部修复(见 "Gniang fork 移植清单")与维护原则。

## 维护原则

1. **兼容新老 Windows**:支持 Windows 10(1607+)+ Windows 11(21H2+);Win11 22H2+
   任务栏 XAML 化需要 UIA 兼容修复,24H2 已实测。
2. **不改变原设计行为**:行为修复对齐原版 R3.1;内部程序集/exe/命名空间保持
   `RoundedTB` 不动;显示名 "RoundedTB Revived"。
3. **bug 与稳定性改进优先**于新功能。
4. 所有改动通过 Git 提交管理。

## 兼容的 Windows 版本

| 系统 | 支持情况 |
|---|---|
| Windows 10(1607+) | 拆分模式;任务栏 Win32 结构稳定 |
| Windows 11 21H2+(build ≥21996) | 动态模式;isWindows11 判定阈值 |
| Windows 11 22H2+/23H2/24H2 | 任务栏 XAML 化 → 依赖 UIA 几何修复;24H2(26100)已验证 |

## 已完成修复(截至 compat tag)

- **Win11 22H2+ 动态分段几何**:UIA 取真实图标边界(`GetTrueTaskbarContentBounds`),
  修复徽标左侧留白/运行应用被裁。
- **居中检测**:`CheckIfCentred` 用注册表真实 build 号(Win11 默认居中);`IsWindows11`
  同理——**.NET Core 下 `Environment.OSVersion` 返回兼容版本(9600),已全部改注册表**。
- **fill 误判修复**:Alt+Tab 检测改为"覆盖任务栏的窗口才算"(不再 WindowFromPoint(0,0));
  **最大化恢复只检测前台窗口**(后台最大化的终端窗口曾让 fill 恒 true、悬停/动态全失效)。
- **悬停显示托盘**:`TrayNotifyWnd` 的 rect 在 Win11 22H2+ 上 Y 偏移,改用任务栏自身
  Y 范围 + 托盘 X 范围检测。
- **动态区域右边界**:UIA 过滤 `IsOffscreen`,应用段右边界 ≤ 托盘左边界 - 1;
  clamp 绝不反向拉小左边界(否则左侧露空白)。
- **点叉退出**:`CloseActionOverride`(MainWindow + About)null 防御;ShutdownMode 设
  OnExplicitShutdown;托盘 Exit 菜单是唯一退出口。
- **托盘图标主题**:注册表 `AppsUseLightTheme` 判断,亮→trayLight(黑)/暗→trayDark(白);
  设置 `NotifyIconImage` 后必须 `ResetIcon()`(该 DP 无变更回调);左键点击托盘图标打开设置。
- **net6 → net8**:移除 `IWshRuntimeLibrary` COMReference(dotnet build 可编译);
  `EnableStartup()` 用 late-bound `WScript.Shell`。
- **AutoHide**:重写为"自动隐藏任务栏"(原生 appbar),**且只在配置 `AutoHide>0` 时生效**
  (此前构造里无条件 `AutoHide(true)` 导致启动即隐藏任务栏、还像"配置没读取")。
- **本地化**:JSON `Strings/{en,zh-Hans,zh-Hant}.json` 候选加载 + 错误对话框;显示名 Revived。
- **清理原维护者信息**;GPL v3 保留。
- **单实例**:App 层 Mutex(第二个实例通知已有实例显示设置后退出);启动无闪窗(手动
  `new MainWindow()`,不再 StartupUri)。
- **打开配置/日志**:ShellExecute + try/catch,不再闪退。
- **build.bat**:一键构建(msbuild/dotnet),末尾 pause。
- 诊断日志:`Interaction.AddLog` 已启用(写 `%LOCALAPPDATA%\rtb.log`;配置/日志仍在
  `%LOCALAPPDATA%` 根目录,未像 Gniang 那样移到 `RoundedTB` 子目录),Background 有节流
  `bw[...]` 与 `hover:` 诊断日志(排查完可清理或保留)。

## 已知问题 / 待接手(低优先级)

- **启动仍有一闪而过的窗口**(低优先级):已去掉 StartupUri、OnStartup 手动 `new
  MainWindow()`,但 MainWindow.xaml 根元素 `Visibility="Visible"` 在
  `OnSourceInitialized` 置 Hidden 前仍可能短暂显示。方向:把该属性改 `Hidden` 或
  在构造里更早隐藏。
- **悬停状态持久化**:鼠标悬停时 `ShowTray/ShowWidgets` 被临时改写,若此时保存设置
  (Apply 或退出),会把临时值写进配置。Gniang 有修复(见下),未移植。
- **配置健壮性**:坏/空配置、旧 schema(pre-3.0 `CornerRadius`/`MarginBasic`)目前靠
  MainWindow 的空值兜底,不如 Gniang 的原子写入+迁移完整。未移植。
- **worker 循环**:Background.DoWork 若抛未捕获异常会静默停止。Gniang 有修复,未移植。

## Gniang fork 移植清单(重要,交其他 AI)

另一个 fork `E:\claude\projects\roundedtb-revived\RoundedTB-Gniang`(作者
**gniang <jing.art@gmail.com>**,https://github.com/Gniang/RoundedTB)修复了与本项目
高度重合的问题。**如需移植,务必保留 gniang 作者信息**(代码注释署名 + commit 里
`Co-Authored-By` 或注明来源)。

整体 cherry-pick `09850fe` 冲突大(4 文件,且依赖其 .NET 10 重构与
`DynamicSecondaryClockLayout` 字段),**建议选择性手动移植**:

| Gniang 提交 | 值得移植的修复 | 位置 |
|---|---|---|
| `09850fe` Phase 1 | ① 副任务栏 tray rect 读主任务栏 tray 的 bug | Taskbar.cs |
| | ② worker 循环不静默死:catch 扩到 Exception + `RunWorkerCompleted` 自动重启(限频) | Background.cs / MainWindow.xaml.cs |
| | ③ **hover 状态不持久化**:保存设置不覆盖 ShowTray/ShowWidgets | Interaction.cs / Background.cs |
| | ④ **配置原子写入**(temp + File.Replace)、坏/空配置回退默认、**pre-3.0 配置迁移** | Interaction.cs |
| | ⑤ Explorer 重启时退避任务栏重建 | Background.cs |
| `a42de5f` | About 链接崩溃修复 | AboutWindow.xaml.cs |

参考命令(在项目仓库):`git remote add gniang E:/claude/projects/roundedtb-revived/RoundedTB-Gniang`、
`git fetch gniang`、`git show gniang/master:<file>` 取单个文件对比。

## TODO / 待规划

- [ ] 上述 Gniang 移植清单(见上)。
- [ ] i18n 进一步完善(语言切换 UI、新增语言自动识别)——已实施基础保留,整体待规划。
- [ ] 分段隐藏不同任务栏段(用户明确本轮不做)。
- [ ] 启动闪窗(低优先级,见上)。

## Git 提交规范

- message 用 `fix:` / `feat:` / `docs:` 前缀;结尾 `Co-Authored-By: Claude <noreply@anthropic.com>`。
- 移植 Gniang 改动时保留其作者署名。
