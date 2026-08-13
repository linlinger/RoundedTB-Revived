# RoundedTB Revived 本地化(i18n)指南

本程序使用**轻量 JSON 翻译文件**方案做界面本地化。它相当于 Linux 程序里的
`.po`(gettext)翻译文件,只是格式换成了 JSON:**
每种语言一个文本文件,直接编辑即可汉化,不需要改代码、不需要重新编译。**

---

## 1. 文件都在哪

```
RoundedTB/Strings/
    en.json       英文(默认语言)
    zh-Hans.json  简体中文
    zh-Hant.json  繁体中文
```

构建时(`build.bat`)会把这些文件自动复制到程序的输出目录:

```
RoundedTB/bin/Release/net8.0-windows10.0.19041/Strings/
```

程序启动时从**输出目录的 `Strings/`** 读取翻译。所以**改了源码里的 JSON 后重新构建**,
或者**直接改输出目录里的 JSON**,效果一样(下次启动生效)。

---

## 2. 格式

每个文件是一个"key → 文本"的 JSON 字典:

```json
{
  "Main_Apply": "Apply",
  "Main_CornerRadius": "Corner radius"
}
```

- **key** 是全程序唯一的标识符,不要改(代码和 XAML 靠它找文本)。
- **值** 是显示给用户的文本,翻译它即可。

### JSON 转义(重要)

- 换行写成 `\n`
- 双引号写成 `\"`
- 反斜杠写成 `\\`

例:

```json
"About_KnownIssuesBody": "第一行。\n第二行。\"带引号\"的文本。"
```

> 注意:文件必须是 **UTF-8 无 BOM** 编码。用 VS Code / 记事本保存为 UTF-8 即可。
> 不要用 GBK/ANSI 保存,否则中文会乱码。

---

## 3. key 命名约定

按界面分组,加前缀,一眼能看出是哪个界面的文本:

| 前缀 | 对应界面 | 例 |
|---|---|---|
| `Main_` | 主配置窗口 | `Main_Apply`, `Main_CornerRadius` |
| `Menu_` | 托盘菜单 / 动态文本 | `Menu_Show`, `Menu_Exit`, `Menu_RunAtStartup` |
| `Help_` | 帮助弹窗(拆分模式 / 兼容性) | `Help_SplitBody` |
| `About_` | 关于窗口 | `About_Welcome`, `About_KnownIssuesBody` |
| `Info_` | Infobox 通用弹窗 | `Info_Ok`, `Info_Title` |

新增 key 时按这个规则命名。

---

## 4. 语言检测与候选加载(程序怎么挑语言文件)

启动时程序按系统显示语言(`CultureInfo.CurrentUICulture`)生成一个**候选文件名列表**,
按优先级从高到低**依次尝试**,第一个存在的文件被加载:

1. 完整区域名:`zh-CN.json`、`ja-JP.json`
2. 区域名下划线写法:`zh_CN.json`、`ja_JP.json`(兼容下划线命名,你例子里的 zh_CN.json 就在这里)
3. 语言两字母:`zh.json`、`ja.json`
4. 中文简/繁 script 名(现有文件):
   - 简体系统 → 追加 `zh-Hans.json` / `zh_Hans.json`
   - 繁体系统(zh-TW/HK/MO/zh-Hant)→ 追加 `zh-Hant.json` / `zh_Hant.json`
5. 全部未命中 → 回退 `en.json`(默认英文)

例如系统是 `zh-CN`:候选为 `zh-CN` → `zh_CN` → `zh` → `zh-Hans` → `zh_Hans`,
会命中现有的 `zh-Hans.json`;如果你另放一个 `zh_CN.json`,它会优先被加载。

---

## 5. 错误处理(语言文件不合法)

- 候选文件**都不存在** → 静默回退英文(不算错误,只是该语言没有翻译)。
- 某个候选文件**存在但 JSON 解析失败** → 启动时弹**对话框**提示"语言文件错误",
  并回退英文。提示文案是内置的(不依赖可能损坏的翻译文件),中英双语。
- 修正该文件内容后重启即可。

---

## 6. 如何新增一种语言(例如日语 ja)

1. **复制** `Strings/en.json` 为 `Strings/ja.json`。
2. 把里面的值翻译成日语,**不要动 key**。
3. 由于检测是"候选列表自动回退",系统为日语(ja-JP)时候选 `ja-JP` → `ja_JP` → `ja`
   会自动命中 `ja.json`,**无需改任何代码**。
4. 重新 `build.bat`,或直接把 `ja.json` 丢进**输出目录的 `Strings/`** 再运行程序
   (改翻译的快捷方式)。

> 语言标识建议用 BCP-47 风格:`zh-Hans`、`zh-Hant`、`ja`、`ko`、`de` 等,
> 文件用连字符(`zh-Hans.json`)或下划线(`zh_CN.json`)都会被识别。

---

## 7. 开发注意事项(给写代码的人)

### 在 XAML 里取文本

窗口根元素已声明 `xmlns:l="clr-namespace:RoundedTB"`,用 `{l:Loc key}`:

```xml
<Button Content="{l:Loc Main_Apply}"/>
<TextBlock Text="{l:Loc About_Welcome}"/>
```

### 在代码里取文本

```csharp
ib.Title = Localization.Get("Help_SplitTitle");
ShowMenuItem.Header = Localization.Get("Menu_Show");
```

### 规则与限制

- **不要**再往 XAML 或代码里硬编码用户可见的英文。新文本一律走资源。
- key 找不到时,程序会**显示 key 本身**(如 `Main_Apply`),方便发现漏翻译。
- 长段落(如关于窗口的说明)整个放进**一个 key**,用 `\n` 换行,不要用 `<LineBreak/>` + 多段拼接。
- 因为段落改成了纯文本,原先 About 窗口里的**内嵌超链接会被去掉**。
- `Localization.Init()` 在 `App.OnStartup` 里调用,**必须早于任何窗口创建**(XAML 的 `{l:Loc}` 在窗口构造时求值)。语言文件解析失败时的对话框也在 `OnStartup` 里弹。
- 当前是**跟随系统语言,不做运行时热切换**:改系统语言或改 JSON 后需重启程序生效。
- 品牌名保留英文 "RoundedTB Revived";构建代号(如 "Canary" 标签)不翻译。
- `Fody/Costura` 只嵌入程序集,不影响这些 JSON,无需特殊处理。

---

## 8. 常见问题

| 现象 | 原因与解决 |
|---|---|
| 界面显示 `Main_Apply` 这种英文 key | 该 key 在当前语言文件里缺失。检查 `Strings/{当前语言}.json` 是否有对应条目,或看文件是不是放错了目录/编码不对。 |
| 启动弹"语言文件错误"对话框 | 某个候选语言文件存在但 JSON 不合法(多半是编码或语法错误)。用 UTF-8 无 BOM 重新保存,或修正 JSON 后重启。 |
| 中文显示成乱码 | JSON 文件被存成了 GBK/ANSI。用 UTF-8 无 BOM 重新保存。 |
| 改了 JSON 但界面没变 | 确认改的是**输出目录** `bin/.../Strings/` 里的文件(运行时读它),且已重启程序。 |
| 简体和繁体没区分开 | 检查系统语言是否为 `zh-TW`/`zh-HK`(繁体)或 `zh-CN`(简体)。 |
| 新增语言不被识别 | 确认文件名能被候选列表命中(见第 4 节);`ja.json` 应该能自动命中,若系统区域较特殊,用完整区域名(如 `pt-BR.json`)再试。 |
