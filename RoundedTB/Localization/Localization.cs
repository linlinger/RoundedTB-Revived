using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RoundedTB
{
    /// <summary>
    /// 轻量本地化方案:所有界面文本放在程序目录 <c>Strings/&lt;lang&gt;.json</c> 中,
    /// 每种语言一个 JSON 文件(对应 Linux 程序的 .po / 翻译文件)。
    /// 加语言、改翻译都只需编辑 JSON,无需重新编译。
    ///
    /// 启动时(App.OnStartup)调用 <see cref="Init"/>。按系统显示语言生成一组候选
    /// 文件名,依次尝试加载:
    ///   1. 完整区域名(zh-CN.json, 下划线兼容 zh_CN.json)
    ///   2. 语言两字母(zh.json)
    ///   3. 中文简/繁 script 名(zh-Hans.json / zh-Hant.json)
    /// 全部未命中则回退英文(en.json)。若某候选文件存在但 JSON 格式不合法,
    /// 记录错误(<see cref="HasLanguageError"/>/<see cref="ErrorFile"/>)并回退英文,
    /// 由调用方提示用户。
    /// </summary>
    public static class Localization
    {
        private static Dictionary<string, string> _strings = new Dictionary<string, string>();

        /// <summary>当前生效的语言标识("en" / "zh-Hans" / "zh-Hant" / ...)。</summary>
        public static string CurrentLanguage { get; private set; } = "en";

        /// <summary>语言文件是否存在但解析失败(JSON 不合法)。</summary>
        public static bool HasLanguageError { get; private set; } = false;

        /// <summary>解析失败的语言文件完整路径;无错误时为 null。</summary>
        public static string ErrorFile { get; private set; } = null;

        /// <summary>在创建任何窗口之前调用一次。</summary>
        public static void Init()
        {
            HasLanguageError = false;
            ErrorFile = null;

            CultureInfo ui = CultureInfo.CurrentUICulture;
            foreach (string lang in BuildCandidates(ui))
            {
                string file = Path.Combine(AppContext.BaseDirectory, "Strings", lang + ".json");
                Dictionary<string, string> loaded = LoadStrings(file, out bool exists, out bool parseFailed);
                if (loaded != null)
                {
                    CurrentLanguage = lang;
                    _strings = loaded;
                    return;
                }
                if (parseFailed)
                {
                    // 文件存在但内容不合法:提示并回退英文,不再尝试其他候选。
                    HasLanguageError = true;
                    ErrorFile = file;
                    break;
                }
                // 文件不存在:尝试下一个候选。
            }

            // 回退英文;英文文件也缺失时用空字典(Get 会返回 key 本身)。
            CurrentLanguage = "en";
            _strings = LoadStrings(Path.Combine(AppContext.BaseDirectory, "Strings", "en.json"), out _, out _) ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// 根据系统显示语言生成候选语言文件名(优先级从高到低)。
        /// 兼容连字符(zh-Hans)与下划线(zh_CN)两种命名风格。
        /// </summary>
        private static List<string> BuildCandidates(CultureInfo ui)
        {
            var list = new List<string>();
            string name = ui?.Name ?? "";

            if (!string.IsNullOrEmpty(name))
            {
                AddCandidate(list, name);                 // zh-CN
                AddCandidate(list, name.Replace('-', '_')); // zh_CN
                string two = name.Split('-')[0];          // zh / ja / en
                AddCandidate(list, two);
            }

            // 中文简/繁 script 名(现有 zh-Hans.json / zh-Hant.json)
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                bool traditional =
                    name.IndexOf("Hant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_TW", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_HK", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("_MO", StringComparison.OrdinalIgnoreCase);
                if (traditional)
                {
                    AddCandidate(list, "zh-Hant");
                    AddCandidate(list, "zh_Hant");
                }
                else
                {
                    AddCandidate(list, "zh-Hans");
                    AddCandidate(list, "zh_Hans");
                }
            }

            return list;
        }

        private static void AddCandidate(List<string> list, string lang)
        {
            if (!string.IsNullOrEmpty(lang) && !list.Contains(lang))
            {
                list.Add(lang);
            }
        }

        /// <summary>
        /// 加载指定文件。
        /// 返回 null 时:exists 表示文件是否存在;parseFailed 表示文件存在但 JSON 解析失败。
        /// </summary>
        private static Dictionary<string, string> LoadStrings(string file, out bool exists, out bool parseFailed)
        {
            exists = false;
            parseFailed = false;
            if (!File.Exists(file))
            {
                return null;
            }
            exists = true;
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file));
            }
            catch (Exception)
            {
                parseFailed = true;
                return null;
            }
        }

        /// <summary>
        /// 取指定 key 的翻译文本。找不到时返回 key 本身,便于在界面上发现漏翻译的项。
        /// </summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }
            if (_strings != null && _strings.TryGetValue(key, out string value))
            {
                return value;
            }
            return key;
        }
    }
}
