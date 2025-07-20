using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Events;
using RimAI.Core.Services;
using RimAI.Framework.API;
using RimWorld;
using System.Threading.Tasks;
using Verse;

namespace RimAI.Core
{
    /// <summary>
    /// RimAI Core 游戏组件
    /// 负责初始化新架构和框架连接测试
    /// </summary>
    public class RimAICoreGameComponent : GameComponent
    {
        private bool hasInitialized = false;
        private bool hasTestedConnection = false;
        
        public RimAICoreGameComponent(Game game)
        {
        }

        public override void GameComponentOnGUI()
        {
            // 初始化核心架构（一次性）
            if (!hasInitialized)
            {
                hasInitialized = true;
                InitializeCoreArchitecture();
            }

            // 在游戏开始后进行一次性连接测试
            if (!hasTestedConnection && hasInitialized)
            {
                hasTestedConnection = true;
                _ = Task.Run(TestFrameworkConnection);
            }
        }

        /// <summary>
        /// 初始化核心架构
        /// </summary>
        private void InitializeCoreArchitecture()
        {
            try
            {
                Log.Message("[RimAICoreGameComponent] Initializing Core architecture...");

                // 服务容器会自动注册默认服务
                var services = ServiceContainer.Instance;
                
                // 检查服务就绪状态
                if (CoreServices.AreServicesReady())
                {
                    Log.Message("[RimAICoreGameComponent] Core architecture initialized successfully");
                    
                    // 发布系统初始化事件
                    var eventBus = CoreServices.EventBus;
                    _ = Task.Run(() => eventBus?.PublishAsync(new ConfigurationChangedEvent(
                        "CoreArchitecture", 
                        "Uninitialized", 
                        "Initialized", 
                        "GameComponent"
                    )));
                }
                else
                {
                    Log.Warning("[RimAICoreGameComponent] Some core services failed to initialize");
                }

                // 输出就绪状态报告
                var report = CoreServices.GetReadinessReport();
                Log.Message($"[RimAICoreGameComponent] Service readiness report:\n{report}");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimAICoreGameComponent] Failed to initialize core architecture: {ex}");
            }
        }

        private async Task TestFrameworkConnection()
        {
            try
            {
                // 检查核心服务是否就绪
                if (!CoreServices.AreServicesReady())
                {
                    Log.Warning("[RimAICoreGameComponent] Core services not ready, skipping Framework test");
                    Messages.Message("RimAI Core 服务未完全就绪", MessageTypeDefOf.CautionInput);
                    return;
                }

                var llmService = CoreServices.LLMService;
                
                if (llmService == null || !llmService.IsInitialized)
                {
                    Log.Warning("RimAI Framework 未初始化。请确保 Framework mod 已正确加载。");
                    Messages.Message("RimAI Framework 未检测到，Core 功能可能无法正常使用", MessageTypeDefOf.CautionInput);
                    return;
                }

                // 简单测试Framework是否可用
                if (llmService.IsInitialized)
                {
                    Log.Message("RimAI Framework 连接成功");
                    
                    // 显示当前模式信息
                    string mode = llmService.IsStreamingAvailable ? "流式模式" : "标准模式";
                    Messages.Message($"RimAI Core 已就绪 ({mode})", MessageTypeDefOf.PositiveEvent);

                    // 发布连接成功事件
                    var eventBus = CoreServices.EventBus;
                    await eventBus.PublishAsync(new ConfigurationChangedEvent(
                        "FrameworkConnection", 
                        "Disconnected", 
                        "Connected", 
                        "ConnectionTest"
                    ));
                }
                else
                {
                    Log.Warning("RimAI Framework 初始化失败");
                    Messages.Message("AI Framework 未初始化", MessageTypeDefOf.RejectInput);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"RimAI Framework 连接测试出错: {ex.Message}");
                Messages.Message("AI 连接测试失败", MessageTypeDefOf.RejectInput);
            }
        }

        public override void GameComponentTick()
        {
            // 可以在这里添加定期检查或维护任务
            // 例如：缓存清理、性能监控等
        }

        /// <summary>
        /// 获取Core系统状态
        /// </summary>
        public static string GetSystemStatus()
        {
            if (!ServiceContainer.Instance.IsRegistered<RimAI.Core.Architecture.Interfaces.IColonyAnalyzer>())
            {
                return "❌ Core系统未初始化";
            }

            var readiness = CoreServices.GetReadinessReport();
            var container = ServiceContainer.Instance.GetStatusInfo();
            
            return $"{readiness}\n\n{container}";
        }
    }
}
