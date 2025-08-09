using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 设置面板的可插拔分区接口。
    /// </summary>
    internal interface ISettingsSection
    {
        /// <summary>
        /// 绘制该分区内容。实现应在内部调用 SectionTitle 并合理插入 Gap/GapLine。
        /// 返回可能更新后的配置草案。
        /// </summary>
        /// <param name="list">RimWorld UI 列表绘制器。</param>
        /// <param name="sectionIndex">分区序号（显示用）。实现应在开始处使用并自增。</param>
        /// <param name="draft">可编辑配置草案（值传递）。</param>
        /// <returns>更新后的配置草案。</returns>
        CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft);
    }
}


