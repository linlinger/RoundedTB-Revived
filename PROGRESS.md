# RoundedTB Revived — 项目进度与维护记录

> 维护者:接续原项目(torchgm / RoundedTB,GPL v3)。详细的原版分析见
> `ANALYSIS-R3.1-to-HEAD.md`。本文件是交接文档,记录已完成修复、已知问题、
> 已移植的外部修复(见 "Gniang fork 移植记录")与维护原则。

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

## 已完成修复

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
- **移植 gniang Phase 1 关键修复**(2026-08,`2730cc2`+`dc31f04`,来源
  https://github.com/Gniang/RoundedTB `09850fe`):
  - 副任务栏 tray rect 误用主句柄(Taskbar.cs)。
  - hover 状态不再污染持久化配置(`ShallowCopy()` + `effectiveSettings`)。
  - 配置原子写入(temp+File.Replace)+ 坏/空配置回退默认 + 缺 key 保持首启默认值 +
    pre-3.0 迁移(`CornerRadius`/`MarginBasic`/`ShowTrayOnHover`)。
  - worker 循环不静默死:catch 扩到 Exception + `RunWorkerCompleted` 重启(限频 5 次/分)。
  - Explorer 崩溃时任务栏重建指数退避(100ms→5s),托盘提示降级状态。

## 已知问题 / 待接手(低优先级)

- **启动仍有一闪而过的窗口**(低优先级):已去掉 StartupUri、OnStartup 手动 `new
  MainWindow()`,但 MainWindow.xaml 根元素 `Visibility="Visible"` 在
  `OnSourceInitialized` 置 Hidden 前仍可能短暂显示。方向:把该属性改 `Hidden` 或
  在构造里更早隐藏。

## Gniang fork 移植记录

另一个 fork `E:\claude\projects\roundedtb-revived\RoundedTB-Gniang`(作者
**gniang <jing.art@gmail.com>**,https://github.com/Gniang/RoundedTB)修复了与本项目
高度重合的问题。**移植时已保留 gniang 作者信息**(代码注释署名 + commit
`Co-Authored-By: gniang <jing.art@gmail.com>`),README Credits 亦有致谢。

整体 cherry-pick `09850fe` 冲突大(依赖其 .NET 10 重构与 `DynamicSecondaryClockLayout`
等字段),故**按手工方式逐文件移植**:

| 项目 | 状态 | 说明 / 位置 |
|---|---|---|
| ① 副任务栏 tray rect 读主句柄 | ✅ 已移植(`2730cc2`) | Taskbar.cs:540,句柄缺失回退主句柄 |
| ② hover 状态不持久化 | ✅ 已移植(`dc31f04`) | Background.cs 用 `hoverShow*` + `Settings.ShallowCopy()` + effectiveSettings |
| ③ 配置原子写入 + 坏/空回退 + 缺 key 保默认 + pre-3.0 迁移 | ✅ 已移植(`dc31f04`) | Interaction.cs:ReadJSON/WriteJSON/FileSystem/CreateDefaultSettings/MigrateLegacySettings |
| ④ worker 循环不静默死 | ✅ 已移植(`dc31f04`) | catch→Exception + MainWindow `RunWorkerCompleted` 重启(限频 5 次/分) |
| ⑤ Explorer 重启退避 | ✅ 已移植(`dc31f04`) | Background.cs 指数退避 100ms→5s + 托盘降级提示 |
| `a42de5f` About 链接崩溃 | ✅ 本项目此前已修(OpenWithDefaultApp) | 等价,无需再移植 |
| ⑧ ShellLink 替代 dynamic WScript.Shell | ⏳ TODO | 见 TODO 理由 |
| ⑨ 配置移到 %LOCALAPPDATA%\RoundedTB\ | ⏳ TODO | 见 TODO 理由 |

参考命令(在项目仓库):`git remote add gniang E:/claude/projects/roundedtb-revived/RoundedTB-Gniang`、
`git fetch gniang`、`git show gniang/master:<file>` 取单个文件对比。

## TODO / 待规划

- [ ] **⑧ ShellLink 替代 dynamic WScript.Shell**(gniang `56e12f9` 的做法)。理由:dynamic
  后期绑定走 IDispatch 反射调用,无编译期类型检查(typo 运行时才炸)、依赖 `WScript.Shell`
  COM 在注册表中存在(被精简/禁用脚本宿主的环境会失败);ShellLink 是 `[ComImport]` 直接
  COM vtable 直调,编译期接口固定、不依赖脚本宿主,Win95 起所有 Windows 都有。当前
  dynamic 方案能编译能跑、兼容优先,故暂不换;换时顺带清理 csproj 无关引用。
- [ ] **⑨ 配置移到 %LOCALAPPDATA%\RoundedTB\**(gniang `09850fe` 的做法:旧文件**复制**而非
  移动,降级仍能找到;已存在的新位置文件优先)。**现在不做**:用户明确要兼容老版本,
  rtb.json 保持原位、老版本/降级都兼容;等我们与老配置 schema 差异变大(需要正式迁移)再搬。
- [ ] **.NET 10 迁移**(gniang `56e12f9`):net8 LTS 至 2026-11 仍支持,当前不动;待 net8 EOL
  临近时一次性做(纯 TFM 改动 + 冒烟测试,参考 gniang 的 csproj diff,不照搬其死代码清理/
  ShellLink 重构)。
- [ ] **self-contained 发布**(对老用户更友好):net8 桌面程序要求装 .NET 8 Desktop Runtime,
  老机器大概率没有 → 发布时 `dotnet publish -r win-x64 --self-contained true`,runtime
  打进 exe 双击即用。
- [ ] i18n 进一步完善(语言切换 UI、新增语言自动识别)——已实施基础保留,整体待规划。
- [ ] 分段隐藏不同任务栏段(用户明确本轮不做)。
- [ ] 启动闪窗(低优先级,见上)。

## Git 提交规范

- message 用 `fix:` / `feat:` / `docs:` 前缀;结尾 `Co-Authored-By: Claude <noreply@anthropic.com>`。
- 移植 Gniang 改动时保留其作者署名。
