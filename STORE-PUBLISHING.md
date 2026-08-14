# 微软商店上架调研(RoundedTB Revived)

> 调研日期:2026-08-14
> 说明:撰写时网络受限(learn.microsoft.com / partner.microsoft.com 无法访问),以下基于
> **一般性知识 + 项目现有打包文件**整理。**具体政策、费用、审核细则请以微软官方最新文档为准**。

## 一、上架硬性要求(一般知识,需联网核实)

### 1. 开发者账户
- 微软 **Partner Center** 开发者账户(个人或公司均可)。
- 历史上个人注册一次性费用约 **$19**(近年政策可能调整,需核实是否仍收费/是否免费)。
- 需 Microsoft 账号 + 身份验证;账户须处于激活状态才能提交应用。

### 2. 应用打包:必须 MSIX
- 桌面(Win32)应用通过 **MSIX** 格式上架,用 **Windows Application Packaging Project(WAP)** 打包。
- MSIX 单包可含 **多架构**(x86 / x64 / arm64),商店按设备架构自动分发——比 GitHub 分 zip 更省事。
- 包必须**签名**(提交商店时由商店证书或你的证书签名)。

### 3. 提交内容(Partner Center 后台)
- 应用名称、短/详细描述(商店文案,建议中英文)。
- 图标 + **截图**(至少 1 张,建议多尺寸)。
- **隐私政策 URL(必须)**。
- 定价(免费/付费)、分类、**年龄分级问卷**。
- 支持的系统最低版本声明(本项目 Windows 10 14393+ 已在 manifest 声明)。
- 版本号(Major.Minor.Build 格式)。
- **AI 内容/生成披露**(近年新增要求,需核实)。

### 4. 认证 / 审核
- 微软自动 + 人工审核;应用必须能正常安装运行、无违规内容。
- 桌面应用需说明其功能与权限。

## 二、项目现状

### 已有(可直接用)
- `PackagingProject/RoundedTB.Package.wapproj` — WAP 项目,已含 x86/x64/ARM/ARM64/AnyCPU 配置。
- `PackagingProject/Package.appxmanifest` — 已声明 `runFullTrust` + `internetClient` 能力、
  `windows.startupTask` 开机自启扩展、全套磁贴图。
- `PackagingProject/Images/` — 商店磁贴/Logo 图齐全。

### 必须改(打包/商店元数据,不是代码)
| 项 | 当前(原版遗留) | 需改为 |
|---|---|---|
| manifest `Identity Name` | `14082CryzenTechnologies.RoundedTB` | 新的全局唯一包名(如 `xxxxxx.RoundedTBRevived`) |
| manifest `Publisher` | `CN=26093D81-…`(原 torchgm 证书指纹) | **新开发者账户的证书指纹(必须)** |
| `DisplayName` / `PublisherDisplayName` | RoundedTB / TorchGM | RoundedTB Revived / 你的名字 |
| `Version` | 1.3.1.0 | 对应 R4.1(如 4.1.x) |
| 商店展示文案/截图 | — | 需重新准备 |

> 桌面版(非商店)的 `AssemblyInfo` 已是 "RoundedTB Revived";商店展示名在 appxmanifest,需同步改。

### 现实障碍
- **WAP 项目构建需要 Visual Studio**(带"使用 C++ 的桌面开发"/UWP 工具),本机只有 dotnet CLI,无法直接产出 MSIX。
  备选方案:
  1. **GitHub Actions**(推荐):windows runner 预装 VS/msbuild,可在每次 push 时构建 wapproj 出 MSIX 并上传——不需要本机 VS。
  2. 命令行 MSIX 打包(MakeAppx + SignTool,Windows SDK 自带),手动组装 appx 目录与证书,较繁琐。
  3. 临时装 VS Community(免费)构建一次。
- 代码本身(Win32 桌面,SetWindowRgn / SHAppBarMessage / UIA)在 **MSIX 全信任包内可正常工作**,不需要沙盒适配——这正是"代码层面不做适配"可行的关键。

## 三、RoundedTB Revived 特有注意

- **开机自启**:manifest 已有 `windows.startupTask`;商店版用它,与桌面版 Startup 快捷方式并存/替代(代码里 `IsRunningAsUWP` 分支已处理)。
- **任务栏操作**:`runFullTrust` 已声明,桌面应用可访问任务栏 HWND,无需额外能力。
- **隐私政策**:需要 URL;GPL 开源项目可写个简单隐私页(GitHub Pages 或 repo 内文档)。
- **AI 生成代码**:README 已注明;提交商店时按问卷披露。
- **多架构**:MSIX 单包含多架构,商店自动分发。

## 四、建议步骤

1. **注册 Partner Center 开发者账户**(费用/政策先联网核实)。
2. **更新 `Package.appxmanifest`** 的 Identity Name / Publisher / DisplayName / Version(Publisher 需新账户证书指纹)。
3. **构建 MSIX**:优先配 GitHub Actions 用 VS 构建 wapproj;或临时装 VS。
4. **准备商店素材**:名称/描述(中英)、截图、隐私政策 URL、年龄分级问卷。
5. **提交** Partner Center,通过审核后上架。

## 待联网确认清单

- [ ] 开发者账户当前注册费/免费政策
- [ ] 最新 AI 内容披露要求
- [ ] 当前审核细则(隐私、桌面应用专项)
- [ ] MSIX 对 Windows 10 最低版本要求的当前政策
