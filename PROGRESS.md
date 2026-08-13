# RoundedTB Revived — 项目进度与维护记录

> 维护者:接续原项目(torchgm / RoundedTB,GPL v3)。本文件记录已完成的修复、
> 待办、维护原则与兼容性信息。详细的原版分析见 `ANALYSIS-R3.1-to-HEAD.md`。

## 维护原则

1. **兼容新老 Windows**:支持 Windows 10(1607+)+ Windows 11(21H2+);Win11 22H2+
   任务栏 XAML 化需要本仓库的 UIA 兼容修复,24H2 已实测。
2. **不改变原设计行为**:行为修复一律对齐原版 R3.1;内部程序集/exe/命名空间保持
   `RoundedTB` 不动;显示名 "RoundedTB Revived" 仅体现在用户可见文字。
3. **bug 与稳定性改进优先**于新功能。
4. 所有改动通过 **Git 提交**管理(见下)。

## 兼容的 Windows 版本(原版设计)

| 系统 | 支持情况 |
|---|---|
| Windows 10(1607+) | 拆分模式;任务栏 Win32 结构稳定,原版机制可用 |
| Windows 11 21H2+(build ≥21996) | 动态模式;isWindows11 判定阈值 |
| Windows 11 22H2+/23H2/24H2 | 任务栏 XAML 化 → 依赖 `Taskbar.GetTrueTaskbarContentBounds`(UIA)修复;24H2(26100)已验证 |

## 已完成修复

- [x] **Win11 22H2+ 动态分段几何**:改用 UI Automation 取真实图标边界
  (`Taskbar.GetTrueTaskbarContentBounds`),修复"徽标左侧留白未被裁"和"运行中的应用被裁掉"。
- [x] **居中检测**:`CheckIfCentred()` 容忍 `TaskbarAl` 注册表键缺失(Win11 默认居中)。
- [x] **net6 → net8 升级**;移除 `IWshRuntimeLibrary` COMReference(dotnet build 可编译),
  `EnableStartup()` 改用 `dynamic` 后期绑定 `WScript.Shell`。
- [x] **build.bat**:一键构建(自动检测 msbuild/dotnet)。
- [x] **本地化基础设施(JSON i18n)**:`Strings/{en,zh-Hans,zh-Hant}.json` 候选加载,
  `{l:Loc}` 标记扩展;显示名 "RoundedTB Revived"。(i18n 进一步工作见 TODO)
- [x] **清理原维护者信息**:README 重写、代码致谢注释移除、About 里 Discord/旧仓库链接移除;
  GPL v3 许可证保留。
- [x] **Bug① 点叉退出**:`mainTitleBar.CloseActionOverride = (tb, win) => win.Hide();`
  右上角叉只隐藏设置窗口,托盘 Exit 菜单是唯一退出口。
- [x] **Bug② 托盘图标亮/暗**:`TrayIconCheck()` 恢复原版映射(亮→trayLight 黑/暗→trayDark 白),
  判断改用注册表 `AppsUseLightTheme`(WPFUI GetSystemTheme 与系统相反,不可靠)。
- [x] **Bug③ 右侧任务栏偶发偏长**:UIA 过滤 `IsOffscreen` 按钮;应用段右边界 ≤ 托盘左边界 - 1。

## TODO / 待规划

- [ ] **i18n 完善(待规划)**:语言切换 UI、新增语言自动识别、错误提示优化等(已实施的基础保留)。
- [ ] 分段隐藏不同任务栏段(用户明确本轮不做,留作未来功能)。
- [ ] `Interaction.AddLog` 目前是 no-op(日志不落盘),如需排查可恢复。
- [ ] 关于窗口内嵌链接因本地化改纯文本而失效(可后续用可点击 TextBlock 恢复)。

## "仅当鼠标悬停时显示分段"(ShowSegmentsOnHover)机制说明

代码位置:`Background.cs`(轮询循环内,约 145-180 行)。

- **作用**:动态/拆分模式下,**托盘段(右下角,含时钟)和组件段(左下角,Win11
  widgets)默认隐藏**;鼠标悬停在对应区域时,该段临时显示(`ShowTray` / `ShowWidgets`
  动态切换并触发重绘)。
- **只影响两个"可选段"**:右下角托盘段(含时钟)和左下角组件段(widgets,若系统有该按钮)。
  主任务栏的应用列表段(app-list,含开始按钮和图标)**不受此开关控制,始终显示**。
- 用户观察正确:该开关实际只作用于右下角时钟/托盘区域(本机无 widgets 按钮,故左下段不存在)。
- 勾选该开关时,`showTrayCheckBox`/`showWidgetsCheckBox` 会被禁用并置为未勾选
  (由 hover 接管)。原版如此,未改变。

## Git 提交规范

- 每个修复/功能独立提交,message 用 `fix:` / `feat:` / `docs:` 前缀(中文或英文均可)。
- 提交信息末尾加 `Co-Authored-By: Claude <noreply@anthropic.com>`。
- 首次基线提交包含当前全部工作区(历史仓库仅含原版提交,我们的改动需从基线起提交)。
