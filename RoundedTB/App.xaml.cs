using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RoundedTB
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // 必须在任何窗口创建前加载语言,这样 XAML 里的 {l:Loc ...} 才能取到对应文本。
            Localization.Init();

            // 语言文件存在但格式不合法:用内置双语文案提示(不依赖可能损坏的翻译文件),已回退英文。
            if (Localization.HasLanguageError)
            {
                string message =
                    "Language file is invalid: " + Localization.ErrorFile +
                    "\nFalling back to English.\n\n" +
                    "语言文件错误:" + Localization.ErrorFile + "\n已回退到英文。";
                MessageBox.Show(message, "RoundedTB Revived", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            WPFUI.Theme.Watcher.Start();
        }
    }
}
