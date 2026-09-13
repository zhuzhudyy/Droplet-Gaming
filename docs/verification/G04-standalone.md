# G04 standalone observation — 2026-09-07

Built and launched `Builds/Windows/DropletPrototype.exe`, Unity 6000.5.10f1 / StandaloneWindows64 / Mono. Native Windows window was selected by returned process path and exact title DropletPrototype, not the Unity Editor window.

Observed through native window capture and input actions:

1. Ready: 120.0 seconds, 0/10 ships, score 0, readable UI and scene.
2. Clicked BEGIN FLIGHT. Playing and countdown appeared.
3. Actual motion/penetration produced 4/10, score 1000, x4; a later observation showed 6/10, score 1300.
4. Later observed Fleet Cleared: 10/10, score 3580, used time 61.18 seconds. This was an observed running-player result, not a scripted full-input replay or a performance measurement.
5. Injected R from Results. A subsequent observation showed Ready, 120.0 seconds, 0/10, score 0 and restored ships.

Native automation's Esc and Return key injection did not consistently yield the expected observed state. The cause (injection/focus versus game handling) is unconfirmed. Do not count those shortcuts as independently verified in the standalone. Physical keyboard Enter/Esc/R and subjective flight feel remain explicit manual checks. Unity Editor's complete Input System smoke and actual PlayMode state/physics tests passed separately.

Screenshots are adjacent files G04-standalone-playing.png, G04-standalone-victory.png and G04-standalone-restarted.png. G04-player.log records native startup; no managed gameplay exception was observed. An unavailable D3D12 info-queue interface message did not prevent rendering. The standalone was last observed in Ready; Unity Editor remained open, out of Play mode, with clean TestRange.
