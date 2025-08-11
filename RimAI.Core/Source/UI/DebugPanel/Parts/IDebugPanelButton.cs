using System;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    /// <summary>
    /// 调试面板按钮契约：每个按钮即独立测试用例。
    /// </summary>
    public interface IDebugPanelButton
    {
        string Label { get; }

        /// <summary>
        /// 执行按钮对应的测试逻辑。
        /// </summary>
        /// <param name="ctx">面板运行时上下文（输出、服务定位、流式工具等）。</param>
        void Execute(DebugPanelContext ctx);
    }
}


