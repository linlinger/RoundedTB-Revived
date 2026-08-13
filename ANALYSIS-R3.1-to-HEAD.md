# RoundedTB Revived — 原版 R3.1 → 最后一次提交变更分析

> 排查日期:2026-08-14。基于仓库 git 历史(R3.1 tag = `89f4acf`;HEAD = `caa616e`,
> 2023-08-13 原维护者最后一次提交)。R3.1 → HEAD 共 **19 次提交**。

## 结论(一句话)

原维护者在 R3.1 之后做了一次**未完成的 ModernWpf → WPFUI 框架迁移**(`c781cb3
"UI framework switch"`)和界面重构,迁移中把原本正确的托盘图标主题逻辑注释掉、主题
硬编码暗色、关闭按钮行为被 WPFUI 接管;再叠加 net6 迁移、实验性新功能、以及 Windows
11 22H2+ 任务栏 XAML 化 —— 这些问题叠加,就是接手维护时一堆 bug 的根源。HEAD 不是
稳定的 R3.1,而是"迁移中途的快照"。**修复方向应与原版(R3.1)行为对齐。**

## 原版(R3.1)托盘图标是怎么处理的

R3.1 用 **ModernWpf** 框架 + **Hardcodet NotifyIcon**(`tb:TaskbarIcon`),
`MainWindow.xaml.cs` 里有真正生效的 `TrayIconCheck()`,是 ModernWpf 的主题事件处理器:

```csharp
public TypedEventHandler<ThemeManager, object> TrayIconCheck()
{
    Uri resLight = new Uri("pack://application:,,,/res/traylight.ico");
    Uri resDark  = new Uri("pack://application:,,,/res/traydark.ico");
    if (ThemeManager.Current.ActualApplicationTheme == ApplicationTheme.Light)
        TrayIcon.Icon = trayLight.ico;   // 亮色 → 黑色图标
    else
        TrayIcon.Icon = traydark.ico;    // 暗色 → 白色图标
}
```

图标文件实际内容(已解码):`traylight.ico` = **黑色**图标、`traydark.ico` = **白色**
图标。文件名语义是"亮/暗主题时使用",不是"图标颜色"。**原版逻辑(亮→黑、暗→白)正确。**

## 19 次提交的破坏性变更

| 提交 | 内容 | 影响 |
|---|---|---|
| `c781cb3` **UI framework switch** | ModernWpf → WPFUI 框架迁移 | **最大的破坏源** |
| `cb90fd4` UI_1 / `d918d9a` UI overhaul stage 0 | 设置界面重构 | 设置窗口行为/结构变化 |
| `c74b186` widget option + 圆角滑块 | 新功能 | 组件段、设置 schema 变化 |
| `dd57482`/`47b56a9`/`98d8ca8` | autohide(自动隐藏) | 新功能(原版已标注 buggy) |
| `dd71ffe` 副任务栏时钟、`96dad06` fix tray sticking | 修复/功能 | 次要 |
| `e851811` fix about dialog | 修复 | 次要 |
| `88422f8`/`4c8cb26`/`caa616e` | CI/actions 升级 | 与行为无关 |
| `9b11332`/`842b634` | "idk"、"fuck knows what i did" | 开发状态不稳定 |

## 框架迁移留下的具体半成品(HEAD = 接手时的问题源头)

1. **托盘图标主题逻辑被注释成空壳**:`TrayIconCheck()` 改为 WPFUI 版但整体注释
   (用 `WPFUI.Theme.Manager.GetSystemTheme()`),图标固定为 XAML 里 `traydark.ico`
   (白色),不再随主题变 → **图标亮暗不随主题/反**。
2. **App.xaml 主题从 ModernWpf `ui:ThemeResources`(跟随系统)换成硬编码
   WPFUI `Dark.xaml`** → 应用固定暗色。
3. **关闭按钮行为变化**:R3.1(ModernWpf)关闭按钮走标准 `Window.Close` → `OnClosing`
   的 `e.Cancel=true` 拦得住 → 只隐藏;HEAD(WPFUI)关闭按钮走 WPFUI `TitleBar.CloseWindow()`
   → `OnClosing` 拦不住 → **点叉退出整个程序**。
4. 目标框架 .NET Framework 4.8 → net6.0-windows(Upgrade Assistant 迁移)。
5. 新实验功能(widget / 圆角滑块 / autohide)+ 不稳定提交 → HEAD 是迁移中途快照。

## 附:本项目后续修复(与 R3.1 对齐)

- **点叉退出**:`mainTitleBar.CloseActionOverride = (tb, win) => win.Hide();`
  (WPFUI TitleBar 的关闭按钮用 `CloseActionOverride` 接管,恢复 R3.1 的"只隐藏")。
- **托盘图标**:`TrayIconCheck()` 恢复"亮→trayLight(黑)、暗→trayDark(白)",判断改用
  注册表 `AppsUseLightTheme`(WPFUI `GetSystemTheme()` 实测与系统相反,ModernWpf 原版
  API 已不可用)。**不对调 ico 文件**(正确映射即"反向"结果,对调反而要连带改代码)。
- **右侧任务栏偶发偏长**:`GetTrueTaskbarContentBounds` 过滤 `IsOffscreen` 按钮;
  `UpdateDynamicTaskbar` 给应用段右边界加结构性上限(≤ 托盘左边界 - 1)。
