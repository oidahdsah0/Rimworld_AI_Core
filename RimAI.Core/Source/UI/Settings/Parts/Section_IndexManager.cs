using System.IO;
using System.Runtime.InteropServices;
using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.Embedding;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 索引管理（状态/重建/打开文件夹）
    /// </summary>
    internal sealed class Section_IndexManager : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 索引管理");
            list.Label("工具向量索引:");
            IToolVectorIndexService index = null;
            try { index = CoreServices.Locator.Get<IToolVectorIndexService>(); } catch { /* ignore */ }
            var state = index == null ? "Unavailable" : (index.IsBuilding ? "Building..." : (index.IsReady ? "Ready" : "Not Ready"));
            list.Label($"状态: {state}");
            list.Gap(SettingsUIUtil.UIControlSpacing);

            var btnRow = list.GetRect(32f);
            float halfW2 = (btnRow.width - SettingsUIUtil.UIControlSpacing) / 2f;
            var btn1 = new Rect(btnRow.x, btnRow.y, halfW2, btnRow.height);
            var btn2 = new Rect(btnRow.x + halfW2 + SettingsUIUtil.UIControlSpacing, btnRow.y, halfW2, btnRow.height);

            if (index != null)
            {
                if (Widgets.ButtonText(btn1, index.IsBuilding ? "正在重建…" : "重建工具索引"))
                {
                    try
                    {
                        index.MarkStale();
                        _ = index.EnsureBuiltAsync();
                        Verse.Messages.Message("RimAI: 已触发工具索引重建", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 重建索引失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                }
            }
            if (Widgets.ButtonText(btn2, "打开索引文件夹"))
            {
                try
                {
                    string dir = index?.IndexFilePath;
                    if (!string.IsNullOrWhiteSpace(dir) && File.Exists(dir))
                    {
                        dir = Path.GetDirectoryName(dir);
                    }
                    if (string.IsNullOrWhiteSpace(dir))
                    {
                        var cfg = CoreServices.Locator.Get<IConfigurationService>();
                        var basePath = cfg?.Current?.Embedding?.Tools?.IndexPath;
                        if (string.IsNullOrWhiteSpace(basePath) || string.Equals(basePath, "auto", System.StringComparison.OrdinalIgnoreCase))
                        {
                            dir = GetDefaultIndexBasePath();
                        }
                        else
                        {
                            dir = basePath;
                        }
                    }
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
                catch (System.Exception ex)
                {
                    Verse.Messages.Message("RimAI: 打开文件夹失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            return draft;
        }

        private static string GetDefaultIndexBasePath()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var local = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                    var appDataDir = System.IO.Directory.GetParent(local)?.Parent?.FullName ?? local; // .../AppData
                    var localLow = System.IO.Path.Combine(appDataDir, "LocalLow");
                    return System.IO.Path.Combine(localLow,
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    return System.IO.Path.Combine(home,
                        "Library", "Application Support",
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    return System.IO.Path.Combine(home,
                        ".config", "unity3d",
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
            }
            catch { }
            return System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "RimWorld", "RimAI");
        }
    }
}


