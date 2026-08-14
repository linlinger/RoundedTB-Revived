# RoundedTB Revived — 程序流程图 + R5 计划(审阅稿)

> 目的:完整描述程序如何工作,供维护者审阅后微调行为、精准提出需求。
> 审阅后据此定 R5(.NET 10 + WPF-UI 4.x)的具体实施。

---

## 1. 程序总览(模块)

```
[App.xaml.cs]  入口:单实例 / 本地化 / 全局异常 / 主题 / 托盘初始化
   └─ new MainWindow()
[MainWindow]   设置窗口 + 托盘 + 主循环宿主
   ├─ [TrayIcon.cs]     托盘图标(自实现 Shell_NotifyIcon)
   ├─ [Background.cs]   主循环(BackgroundWorker,每 100ms)
   ├─ [Taskbar.cs]      任务栏枚举 + region 渲染(Simple/Dynamic)
   ├─ [Interaction.cs]  配置读写(JSON 原子写)/ 日志 / 工具
   ├─ [Localization]    手写 JSON i18n(可热编辑)
   └─ [LocalPInvoke.cs] Win32 P/Invoke(user32/gdi32/shell32)
```

---

## 2. 启动流程

```
App.OnStartup
 ├─ 1. Mutex 单实例:已有实例?
 │     └─ 是 → 通知已有实例显示设置窗口(改窗口标题 RoundedTB_SettingsRequest)→ 本实例退出
 ├─ 2. Localization.Init(按系统语言加载 en/zh-Hans/zh-Hant)
 ├─ 3. 语言文件格式错?→ 弹窗提示 + 回退英文
 ├─ 4. WPFUI Theme.Watcher.Start(跟随系统主题)
 └─ 5. new MainWindow()
      ├─ 5a. InitializeComponent(加载 XAML 控件)
      ├─ 5b. 提取托盘菜单控件(Window.Resources ContextMenu 的 Items,按顺序)
      ├─ 5c. CloseActionOverride(点叉 = 隐藏到托盘,不退出)
      ├─ 5d. OS 检测:注册表 CurrentBuild ≥21996 → Win11(否则 Win10)
      ├─ 5e. 创建 Background / Interaction
      ├─ 5f. 托盘初始化:EnsureHandle(强建 HWND,Hidden 窗口不自动建)
      │        → HwndSource.AddHook(收托盘消息/热键)
      │        → new TrayIcon(Shell_NotifyIcon)→ Show()
      │        → TrayIconCheck(按主题设图标)
      │        → RegisterHotKey(Win+F2 切换显示托盘段)
      │        → Visibility=Hidden
      ├─ 5g. 检查 Startup 快捷方式是否存在(设置勾选框状态)
      ├─ 5h. 配置 worker(DoWork / RunWorkerCompleted 自动重启)
      ├─ 5i. Interaction.FileSystem(确保 rtb.json / rtb.log)
      ├─ 5j. ReadJSON:读配置(填充默认值 + pre-3.0 迁移)→ activeSettings
      ├─ 5k. 空值兜底 + version 迁移(升级触发首次启动)
      ├─ 5l. GenerateTaskbarInfo(枚举所有任务栏)
      │        → 启动时先 ResetTaskbar 清理上次残留 region(防强杀残留)
      ├─ 5m. 恢复 UI 控件值(勾选框/边距/圆角,屏蔽事件避免弹窗)
      ├─ 5n. ApplyButton_Click(把配置应用到任务栏 + 启动 worker)
      └─ 5o. Show()+Hide()(初始化窗口,让托盘右键菜单可用;代价启动闪一下)
 App.OnStartup 返回 → Application.Run(Dispatcher 消息循环)
```

---

## 3. 主循环(Background.DoWork,每 100ms 一次)

```
while(true)
 ├─ 取消请求? → break
 ├─ [低频段,每 10 tick ≈1s]:
 │   ├─ 单实例"设置请求"检查(每 3 次低频 ≈3s):
 │   │    枚举顶层窗口,找标题=RoundedTB_SettingsRequest → 显示设置窗口 + 改回标题
 │   ├─ TrayIconCheck(托盘图标主题,缓存 _lastTrayLight 防重复刷新)
 │   └─ 动态模式? → 每任务栏 RefreshContentBounds(UIA 刷新 content 缓存)
 ├─ CheckIfCentred(注册表 TaskbarAl)→ activeSettings.IsCentred
 ├─ 任务栏数量/主句柄变了? → 重新枚举(Explorer 重启恢复)
 ├─ for 每个任务栏:
 │   ├─ 句柄无效(Explorer 挂了)? → 指数退避重试(100ms→5s),托盘提示降级
 │   ├─ GetQuickTaskbarRects(取当前任务栏/托盘/应用列表 rect)
 │   ├─ TaskbarShouldBeFilled?
 │   │    窗口最大化(FillOnMaximise)/ Alt+Tab(FillOnTaskSwitch)→
 │   │    ResetTaskbar(填满任务栏)→ 跳过本 tick
 │   ├─ ShowSegmentsOnHover(悬停显示分段)?
 │   │    鼠标在托盘/组件段区域? → 临时显示(写 effectiveSettings,不落盘)
 │   ├─ AutoHide>0?(自动隐藏)
 │   │    → 纯 OS ABM 自动隐藏(任务栏滑出/滑回由系统管,RTB 不干预)
 │   └─ region 需要重放?(rect 变了 / Ignored / 强制)
 │        ├─ 动态模式 → RefreshContentBounds(同步 UIA,防新图标半截)
 │        └─ UpdateSimpleTaskbar 或 UpdateDynamicTaskbar(SetWindowRgn)
 └─ Sleep(100ms)
```

---

## 4. 任务栏渲染(核心)

```
Simple(拆分/简单模式):
  CreateRoundRectRgn(按 Margin 各边 + CornerRadius)→ SetWindowRgn(整条圆角矩形)

Dynamic(动态模式):
  content bounds = UIA 枚举任务栏按钮
    (StartButton / SearchButton / TaskViewButton / Appid:*)
    → 取最小左缘 minLeft、最大右缘 maxRight
  → 结构性约束:
      右边界 ≤ 托盘左缘 - 1(防溢出)
      左边界 ≥ 任务栏左缘;异常值回退 legacy AppListRect
      (sanity: contentLeft < 托盘左,防止 UIA 瞬态把左边界推右)
  → 居中? x1=content左-marginLeft-padding, x2=content右+marginRight+padding
     左对齐? x1=marginLeft(贴边), x2=content右+marginRight+padding
  → CreateRoundRectRgn(应用段)
  → 显示托盘/组件段? → 各自 CreateRoundRectRgn + CombineRgn 合并
  → SetWindowRgn(交给系统)+ DeleteObject 释放临时 region(防 OOM 泄漏)
```

---

## 5. 托盘交互

```
托盘图标(Shell_NotifyIcon)
 ├─ 左键点击 → 显示/隐藏设置窗口(ShowMenuItem_Click)
 ├─ 右键点击 → 弹菜单:
 │    ├─ 读系统主题(AppsUseLightTheme)→ 设菜单配色(DynamicResource 刷子,亮/暗)
 │    ├─ PlacementTarget=窗口, Placement=鼠标点, IsOpen=true
 │    └─ DispatcherTimer(100ms)监控:鼠标移出菜单 + 外部点击 → 关闭
 ├─ 系统主题变化 → TrayIconCheck:亮→黑图标(TrayLight.ico), 暗→白图标(TrayDark.ico)
 └─ Explorer 重启(TaskbarCreated)→ 重新添加图标
```

---

## 6. 设置窗口 / Apply

```
UI 控件(边距×4 / 圆角 / 动态模式 / 显示托盘段 / 悬停 / 最大化填充 / Alt+Tab填充 / TTB兼容 / AutoHide / 还原默认)
ApplyButton_Click:
 ├─ 读所有 UI 控件 → activeSettings
 ├─ 应用到所有任务栏(Simple/Dynamic)
 ├─ 重启 worker
 ├─ AutoHide 按配置设/清(ABM_SETSTATE)
 ├─ WriteJSON(原子写:temp + File.Replace)
 ├─ TrayIconCheck(刷新图标)
 └─ UpdateUi(更新预览矩形)

"还原默认设置" → CreateDefaultSettings(Win11 圆角 20)→ 填回 UI → Apply
```

---

## 7. 配置 / 单实例 / 退出

```
配置:
  ReadJSON:PopulateObject 填充默认(缺 key 保留默认)+ pre-3.0 迁移
           (CornerRadius/MarginBasic sentinel -384 / ShowTrayOnHover)
  WriteJSON:temp 文件 + File.Replace(原子,防崩溃截断)

单实例:
  Mutex(创建即持锁);第二实例发现 Mutex 已存在
  → 把已有实例窗口标题改成 "RoundedTB_SettingsRequest"
  → 已有实例主循环每 ~3s 枚举顶层窗口发现 → 显示设置窗口 → 改回标题
  (3s 延迟,可改事件通知优化)

退出:
  托盘 Exit 菜单 → Close()+Shutdown → OnClosing(取消 worker + 恢复任务栏)→ OnExit(兜底再恢复)
  任务管理器结束任务 → 系统发 WM_CLOSE → OnClosing(恢复任务栏 + 退出)
  强制结束(TerminateProcess)→ 进程被杀,无法清理 → 下次启动时清理残留 region
```

---

## 8. 关键行为决策点(供微调)

1. **点叉** = 隐藏到托盘(不退出);托盘 Exit 菜单是唯一退出口
2. **Alt+F4 / 任务管理器结束** = 恢复任务栏 + 退出(OnClosing 区分)
3. **托盘左键** = 显示/隐藏设置窗口;**右键** = 菜单(主题配色 + 外点关闭)
4. **动态模式** 应用段:UIA 内容边界 ± margin ± 6px padding;居中/左对齐不同
5. **悬停显示分段**:托盘/组件段默认隐藏,悬停临时显示(不落盘)
6. **AutoHide**:纯 OS 自动隐藏(任务栏滑出/滑回由系统管)
7. **窗口最大化 / Alt+Tab** → 任务栏填满(ResetTaskbar)
8. **托盘图标主题**:注册表读,亮黑/暗白;WPFUI GetSystemTheme 反向不可用
9. **启动闪窗**:为托盘菜单可用,Show+Hide 一次(已知问题)
10. **单实例唤醒**:3s 轮询(可改事件通知)

---

# 开发策略与 R5 计划

**开发策略(已确认)**:.NET 10 迁移**拉独立分支开发**(如 `net10` / `r5-net10`),master 保持 R4.1.2 稳定;分支完成、验证 OK 后**再合并回 master**。

## R5 计划(.NET 10 + WPF-UI 4.x + Hardcodet 托盘 + 清理)

### Part 1 — .NET 10 + WPF-UI 4.x 迁移
- csproj:TFM → `net10.0-windows10.0.19041`;`WPF-UI` 1.2.1 → **4.3.0**(3.x 全系废弃,4.3.0 支持 net10,2026-05 更新)
- API 迁移 `WPFUI` → `Wpf.Ui`:XAML 命名空间、`Theme.Watcher`→`Wpf.Ui.Appearance.Watcher`、`Background.Manager(Mica)`→4.x Mica、`Common.Appearance`、Button/TitleBar

### Part 2 — 托盘换 Hardcodet.NotifyIcon.Wpf(先试,不行 fallback 手写)
- 删自实现 `TrayIcon.cs`,改 Hardcodet `TaskbarIcon`(图标/右键菜单/左键)
- 图标主题切换用现有 TrayIconCheck 逻辑,换设 TaskbarIcon.Icon
- **26H1 实测**;不行 → 保留手写 TrayIcon.cs 切回

### Part 3 — 全清理
- LocalPInvoke 死码(`// mystery` SetWindowCompositionAttribute/AccentPolicy + 未用巨型枚举)
- 去 WinForms `DoEvents`(2 处)→ async 取消+await
- 单实例:窗口标题+3s 轮询 → 事件通知(EventWaitHandle/命名管道)
- 残留:MainWindow.xaml:7 Hardcodet xmlns、Infobox.xaml:10 ModernWpf xmlns、AboutWindow NotifyIconTooltip
- 修订过期 CLAUDE.md

### Part 4 — 保持不动
- 配置 Newtonsoft JSON + 原子写 + 迁移;本地化手写 JSON i18n;任务栏 P/Invoke

### 风险与验证
- WPF-UI 4.x API 变化大(用量小,可控);Hardcodet 26H1 需实测;.NET 10 回归(自包含屏蔽 runtime)
- 验证:三通道 build 0 错误、托盘(26H1+本机)/菜单/主题/左键循环、单实例实时、无 OOM、多架构 publish

### 提交分组
1. `feat: bump .NET 10 + WPF-UI 4.3.0`
2. `refactor: tray icon via Hardcodet.NotifyIcon`(或 fallback)
3. `refactor: single-instance event + drop DoEvents + dead code + leftovers`
4. `docs: CLAUDE.md 修订 + PROGRESS 更新`
