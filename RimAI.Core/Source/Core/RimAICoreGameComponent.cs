using RimAI.Framework.API;
using RimWorld;
using System.Threading.Tasks;
using Verse;

namespace RimAI.Core
{
    /// <summary>
    /// RimAI Core 游戏组件
    /// 负责初始化检查和框架连接测试
    /// </summary>
    public class RimAICoreGameComponent : GameComponent
    {
        private bool hasTestedConnection = false;
        
        public RimAICoreGameComponent(Game game)
        {
        }

        public override void GameComponentOnGUI()
        {
            // 在游戏开始后进行一次性连接测试
            if (!hasTestedConnection)
            {
                hasTestedConnection = true;
                _ = Task.Run(TestFrameworkConnection);
            }
        }

        private async Task TestFrameworkConnection()
        {
            try
            {
                if (!RimAIAPI.IsInitialized)
                {
                    Log.Warning("RimAI Framework 未初始化。请确保 Framework mod 已正确加载。");
                    Messages.Message("RimAI Framework 未检测到，Core 功能可能无法正常使用", MessageTypeDefOf.CautionInput);
                    return;
                }

                var (success, message) = await RimAIAPI.TestConnectionAsync();
                
                if (success)
                {
                    Log.Message($"RimAI Framework 连接成功: {message}");
                    
                    // 显示当前模式信息
                    string mode = RimAIAPI.IsStreamingEnabled ? "流式模式" : "标准模式";
                    Messages.Message($"RimAI Core 已就绪 ({mode})", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Log.Error($"RimAI Framework 连接失败: {message}");
                    Messages.Message($"AI 服务连接失败: {message}", MessageTypeDefOf.NegativeEvent);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"RimAI Framework 连接测试异常: {ex.Message}");
                Messages.Message("AI 服务连接测试异常，请检查设置", MessageTypeDefOf.RejectInput);
            }
        }

        public override void GameComponentTick()
        {
            // 可以在这里添加周期性检查逻辑
            base.GameComponentTick();
        }

        /// <summary>
        /// 检查框架是否可用
        /// </summary>
        public static bool IsFrameworkAvailable()
        {
            return RimAIAPI.IsInitialized;
        }

        /// <summary>
        /// 获取当前设置信息
        /// </summary>
        public static string GetSettingsInfo()
        {
            if (!RimAIAPI.IsInitialized)
                return "Framework 未初始化";
                
            var settings = RimAIAPI.CurrentSettings;
            if (settings == null)
                return "设置信息不可用";
                
            string mode = RimAIAPI.IsStreamingEnabled ? "流式模式" : "标准模式";
            return $"当前模式: {mode}";
        }
    }
}
