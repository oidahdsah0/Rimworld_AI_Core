using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
    internal sealed class ManualInterServerTrigger : IStageTrigger, IManualStageTrigger
    {
        private volatile bool _armed;
        private volatile bool _enabled;
        private readonly IStageService _stage;
    private readonly RimAI.Core.Source.Modules.Stage.Diagnostics.IStageLogging _log;

    public ManualInterServerTrigger(IStageService stage, RimAI.Core.Source.Modules.Stage.Diagnostics.IStageLogging log) { _stage = stage; _log = log; }

        public string Name => "ManualInterServerTrigger";
        public string TargetActName => "InterServerGroupChat";

        public Task OnEnableAsync(CancellationToken ct) { _enabled = true; return Task.CompletedTask; }
        public Task OnDisableAsync(CancellationToken ct) { _enabled = false; return Task.CompletedTask; }

    public void ArmOnce() { _armed = true; try { _log?.Info("ManualInterServerTrigger armed once"); } catch { } }

        public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
        {
            if (!_enabled || !_armed) return; _armed = false; try { _log?.Info("ManualInterServerTrigger run"); } catch { }
            var auto = _stage?.TryGetAutoProvider(TargetActName);
            if (auto == null) return;
            try
            {
                var intent = await auto.TryBuildAutoIntentAsync(ct).ConfigureAwait(false);
                if (intent != null)
                {
                    // Tag as manual so it can bypass coalesce/cooldown gates
                    intent.Origin = "Manual";
                    var decision = await submit(intent).ConfigureAwait(false);
                    try { _log?.Info($"ManualInterServerTrigger submit outcome={decision?.Outcome} reason={decision?.Reason}"); } catch { }
                }
            }
            catch { }
        }
    }
}
