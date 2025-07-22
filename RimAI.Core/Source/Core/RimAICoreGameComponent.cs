using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Events;
using RimAI.Core.Services;
using RimAI.Core.Settings;
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
            try
            {
                Log.Message("[RimAICoreGameComponent] 🎮 Game component constructor called");
                // 基础构造函数，不做复杂初始化
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimAICoreGameComponent] ❌ Constructor failed: {ex}");
                throw;
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            // This is a good place to clear any data that should not persist across different save games.
            // For now, our services are singletons and manage their own state.
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            // This is a good place to clear any data that should not persist across different save games.
        }

        public override void ExposeData()
        {
            base.ExposeData();
            
            // This single line handles all registered per-save data persistence
            // by calling the ExposeData() method on all IPersistable objects
            // that have registered themselves with the PersistenceService.
            CoreServices.PersistenceService?.ExposeAllRegisteredData();
        }
        
        private void ClearPersistableRegistrations()
        {
            // This is a conceptual method. The actual implementation will depend on 
            // how we manage the lifecycle of persistable objects. For now, we assume
            // services are long-lived and don't need explicit clearing, but if we
            // register per-game objects, this is where we'd clean them up.
            // A more robust implementation might involve the PersistenceService exposing a clear method.
        }

        public override void GameComponentOnGUI()
        {
            try
            {
                // 检查是否启用调试日志
                bool verboseLogging = false;
                try
                {
                    verboseLogging = SettingsManager.Settings?.Debug?.EnableVerboseLogging ?? false;
                }
                catch
                {
                    // 忽略设置获取错误，使用默认值
                }

                // 初始化核心架构（一次性）
                if (!hasInitialized)
                {
                    if (verboseLogging) Log.Message("[RimAICoreGameComponent] 🔄 Starting core architecture initialization...");
                    hasInitialized = true;
                    InitializeCoreArchitecture();
                }

                // 在游戏开始后进行一次性连接测试
                if (!hasTestedConnection && hasInitialized)
                {
                    if (verboseLogging) Log.Message("[RimAICoreGameComponent] 🧪 Starting framework connection test...");
                    hasTestedConnection = true;
                    _ = Task.Run(TestFrameworkConnection);
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimAICoreGameComponent] ❌ GameComponentOnGUI failed: {ex}");
                // 不要重新抛出，避免游戏循环崩溃
            }
        }

        /// <summary>
        /// 初始化核心架构
        /// </summary>
        private void InitializeCoreArchitecture()
        {
            bool verboseLogging = false;
            try
            {
                verboseLogging = SettingsManager.Settings?.Debug?.EnableVerboseLogging ?? false;
            }
            catch
            {
                // 忽略设置获取错误
            }

            try
            {
                Log.Message("[RimAICoreGameComponent] 🔧 Initializing Core architecture...");

                if (verboseLogging)
                {
                    Log.Message("[RimAICoreGameComponent] 📋 Step 1: Getting ServiceContainer instance...");
                }

                // 服务容器会自动注册默认服务
                var services = ServiceContainer.Instance;
                
                if (verboseLogging)
                {
                    Log.Message("[RimAICoreGameComponent] 📋 Step 2: Checking service readiness...");
                }
                
                // 检查服务就绪状态
                if (CoreServices.AreServicesReady())
                {
                    Log.Message("[RimAICoreGameComponent] ✅ Core architecture initialized successfully");
                    
                    if (verboseLogging)
                    {
                        Log.Message("[RimAICoreGameComponent] 📋 Step 3: Publishing initialization event...");
                    }
                    
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
                    Log.Warning("[RimAICoreGameComponent] ⚠️ Some core services failed to initialize");
                    
                    if (verboseLogging)
                    {
                        // 详细检查每个服务状态
                        Log.Message($"[RimAICoreGameComponent] 🔍 Analyzer: {(CoreServices.Analyzer != null ? "✅" : "❌")}");
                        Log.Message($"[RimAICoreGameComponent] 🔍 PromptBuilder: {(CoreServices.PromptBuilder != null ? "✅" : "❌")}");
                        Log.Message($"[RimAICoreGameComponent] 🔍 LLMService: {(CoreServices.LLMService != null ? "✅" : "❌")}");
                        Log.Message($"[RimAICoreGameComponent] 🔍 CacheService: {(CoreServices.CacheService != null ? "✅" : "❌")}");
                        Log.Message($"[RimAICoreGameComponent] 🔍 EventBus: {(CoreServices.EventBus != null ? "✅" : "❌")}");
                        Log.Message($"[RimAICoreGameComponent] 🔍 Governor: {(CoreServices.Governor != null ? "✅" : "❌")}");
                    }
                }

                // 输出就绪状态报告
                var report = CoreServices.GetReadinessReport();
                if (verboseLogging)
                {
                    Log.Message($"[RimAICoreGameComponent] 📊 Service readiness report:\n{report}");
                }
                else
                {
                    Log.Message($"[RimAICoreGameComponent] 📊 Core services status: {(CoreServices.AreServicesReady() ? "Ready" : "Partial")}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimAICoreGameComponent] ❌ CRITICAL: Failed to initialize core architecture: {ex}");
                Log.Error($"[RimAICoreGameComponent] Stack trace: {ex.StackTrace}");
                
                // 不要重新抛出异常，避免游戏崩溃
                // 而是尝试提供最小功能
                Log.Warning("[RimAICoreGameComponent] 🔧 Attempting to continue with minimal functionality...");
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
