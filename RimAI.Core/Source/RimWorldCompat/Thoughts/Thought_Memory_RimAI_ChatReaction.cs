using Verse;
using RimWorld;

namespace RimAI.Core.Source.RimWorldCompat.Thoughts
{
    /// <summary>
    /// 自定义记忆思想：允许实例级自定义标题，用于显示在需求面板（LabelCap）。
    /// </summary>
    public class Thought_Memory_RimAI_ChatReaction : Thought_Memory
    {
        public string customTitle;

        public override string LabelCap
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(customTitle))
                {
                    return customTitle.CapitalizeFirst();
                }
                return base.LabelCap;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref customTitle, "customTitle");
        }
    }
}
