using System;
using System.Windows.Markup;

namespace RoundedTB
{
    /// <summary>
    /// XAML 里取翻译文本的标记扩展。
    ///
    /// 用法(推荐):<c>{l:Loc Main_Apply}</c>
    /// 或          <c>{l:Loc Key=Main_Apply}</c>
    ///
    /// 例:<Button Content="{l:Loc Main_Apply}"/>
    ///     <TextBlock Text="{l:Loc About_Welcome}"/>
    ///
    /// 值在窗口 XAML 解析时求值,因此 Localization.Init() 必须在创建任何窗口前调用
    /// (已在 App.OnStartup 中处理)。
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public class LocExtension : MarkupExtension
    {
        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        /// <summary>翻译 key,例如 "Main_Apply"。</summary>
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Localization.Get(Key);
        }
    }
}
