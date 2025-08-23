# Important Lessons — StatPart + Hediff Runtime Injection (RimWorld 1.6)

This document distills the root cause, the fixes, the final design, and best practices learned from making the AI Server buff truly effective on pawns.

## Problem Statement

- We needed the AI Server building(s) to grant real, system-level work stat buffs to colonists.
- Initial approach via XML patched StatParts failed intermittently — stats did not change even though UI text claimed they did.
- Health tab should show a green Hediff entry (for visibility) without cluttering Inspect string.

## Root Causes

1) XML Patch fragility and load-order differences
- Our StatPart injections via XML patches weren’t reliably applied at game load (RimWorld 1.6). Schema/patch errors made the part absent from StatDef.parts.
- Consequence: TransformValue/ExplanationPart never executed; any UI messaging was cosmetic only.

2) Missing parentStat on runtime-created StatPart
- Even when we added the StatPart at runtime, we initially did not set part.parentStat.
- Without parentStat, RimWorld’s stat pipeline does not invoke our logic properly; explanation lines did not appear and value changes didn’t apply.

3) Dual-source signal jitter
- We briefly used both server scanning and Hediff-less adjustments, which caused occasional flicker and ambiguity. A single source of truth was needed.

## Final Solution

- Hediff as source of truth: A visible green Hediff (`RimAI_ServerBuff`) is maintained on colonists; its Severity encodes the global buff percent.
- MapComponent owning severity: Every ~1 second, we scan online servers on a map and set Hediff Severity for all player humanlikes; remove Hediff when severity would be 0.
- StatPart reading Hediff: `StatPart_RimAIServer` reads Severity and applies multiplicative factors to work-related stats; falls back to a lightweight server scan only if the Hediff isn’t present momentarily.
- Runtime injection: `StatPartRuntimeInjector` injects `StatPart_RimAIServer` into the relevant StatDefs at startup, and crucially sets `part.parentStat = statDef`. It also prints a dev-only attachment list for verification.
- No Inspect postfix: To avoid UI noise, we removed the Inspect string postfix. Health shows a clean entry with a “+X%” bracket via a HediffComp.

## Verifications

- Stats actually increase: After a restart (to trigger static constructors), affected stats show higher values and include an explanation line like `AI服务器增益 severity: +X%`.
- DevMode attachment log: At startup, DevMode prints the StatDefs that have our StatPart attached.
- Optional dump tooling was removed to reduce clutter; logs were sufficient for verification.

## Best Practices

- Prefer runtime injection for StatParts (1.6): Use a `StaticConstructorOnStartup` injector; avoid fragile XML patches for stat parts.
- Always set parentStat: When creating a StatPart programmatically, set `part.parentStat = statDef` so Transform/Explanation run as expected.
- Single source of truth: Drive visible UI (Hediff) and actual effects (StatPart) from the same severity value; it makes debugging and UX coherent.
- Keep updates low-frequency and efficient: A 60-tick interval is responsive enough while avoiding flicker.
- Minimize UI chatter: Prefer Health tab visibility via Hediff; avoid Inspect string clutter.
- Dev-only logs: Gate diagnostics behind `Prefs.DevMode` and keep them minimal; log attachment lists at startup only.
- Keep fallback minimal: Only scan servers if the Hediff hasn’t been applied yet; steady state should rely on Hediff.

## Result

- Real, engine-level work stats are modified for colonists based on online AI servers. The buff is both visible and authoritative.
- Implementation is robust across restarts and is largely self-healing, with minimal UI noise.

## Appendix — Affected Stats

- Core/related stats we target: WorkSpeedGlobal, GlobalWorkSpeed, GeneralLaborSpeed, ConstructionSpeed, MiningSpeed, PlantWorkSpeed, ResearchSpeed, MedicalOperationSpeed, MedicalTendSpeed. Compatibility aliases are attempted if present.

