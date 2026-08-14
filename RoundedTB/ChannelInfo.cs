using System;

namespace RoundedTB
{
    /// <summary>
    /// 按构建通道(Canary / Dev / Master)提供的常量。
    /// 通道由 csproj 的 -p:Channel 属性决定(默认 Canary),并生成对应的
    /// CHANNEL_CANARY / CHANNEL_DEV / CHANNEL_MASTER 条件编译符号。
    /// 构建示例:dotnet build -c Release -p:Channel=Master
    /// </summary>
    public static class ChannelInfo
    {
#if CHANNEL_CANARY
        public const string Name = "Canary";
        public const string Subtitle = "Canary";
        public const string IconUri = "pack://application:,,,/RoundedTBCanary.ico";
        public const string Banner = "res/HeadBannerCan.png";
        public const int Version = -1;          // 预发布:豁免"版本升级首次启动"重置
        public static readonly bool VerboseLogging = true; // 预发布保留完整诊断日志(static readonly 避免 const 折叠触发 CS0162)
#elif CHANNEL_DEV
        public const string Name = "Dev";
        public const string Subtitle = "Dev";
        public const string IconUri = "pack://application:,,,/RoundedTBDev.ico";
        public const string Banner = "res/HeadBannerDev.png";
        public const int Version = -1;
        public static readonly bool VerboseLogging = true;
#else // Master(正式版)
        public const string Name = "Master";
        public const string Subtitle = "R4.1";
        public const string IconUri = "pack://application:,,,/RoundedTB.ico";
        public const string Banner = "res/HeadBanner.png";
        public const int Version = 3;
        public static readonly bool VerboseLogging = false; // 正式版过滤高频诊断日志
#endif

        // 将来 Canary 独有实验功能放 #if CHANNEL_CANARY 块(现为空)。
    }
}
