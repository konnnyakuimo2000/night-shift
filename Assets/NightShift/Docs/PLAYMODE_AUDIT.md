# PlayMode gameplay audit

`Tests/PlayMode/GameplayAuditTests.cs` exercises runtime GameObjects, real UI button callbacks, GameManager.Update, ShiftSession, and save/retry paths. It does not use Computer Use or move the system mouse.

## Save isolation

Run this suite in a **copy** of Assets, Packages and ProjectSettings, with `companyName: NightShiftPlayModeAudit` in the copied ProjectSettings/ProjectSettings.asset. Never change the original project's company name. The suite skips when this marker is absent. The copy has a separate PlayerPrefs namespace. Tests clear only that namespace, including between cases. No real credentials or player save data are needed.

From the project root, `./Tools/Run-PlayModeAudit.ps1` makes that copy and executes the suite. It retains the copy for diagnosis and writes XML, logs and the captured image into a timestamped folder under Logs. Its exit code is nonzero when assertions fail. Do not run multiple copies of this audit concurrently because they share the test-only PlayerPrefs namespace.

Example command (replace COPY_PATH and OUTPUT_PATH with absolute paths):

```text
Unity.exe -batchmode -projectPath COPY_PATH -runTests -testPlatform PlayMode -testFilter NightShift.Tests.GameplayAuditTests -testResults OUTPUT_PATH/results.xml -logFile OUTPUT_PATH/unity.log
```

Do not add `-quit` (the runner exits when finished) or `-nographics` (one test renders the UI). Use Unity 6000.0.49f1. A fresh copy needs an initial package resolution and asset import.

## Scope and interpretation

- Button callbacks: connection allow/block/report, pause resume, auto-review enable, evaluation next day.
- Runtime lifecycle: briefing clock, pause, tool launch/close, minimize/restore, background auto-review, all 30 briefing screens.
- Shift state: grace period, help desk completion, grace expiry, overtime inventory, request preservation.
- Persistence: new game after a previous campaign, retry rollback, payroll/checkpoint after completion, checkpoint restoration and week retry.
- Game over: fatal incident through evaluation, next-day button, game-over screen and retry-night button.
- Timing regression: restarting during the completion transition.
- Full-night smoke runs: DAY8, DAY20 and DAY30, driven by the actual GameManager.Update at 120x debug speed. The agent reads expected decisions; these are **not human difficulty or balance measurements**. Remote tickets may remain unresolved and threats may cause damage.
- UI bounds: 1280x800 rendering at 150% UI scale; `Logs/AuditConnection150.png` is written in the isolated copy.

Private flow entry points are invoked by reflection for setup and transitions that would otherwise require lengthy onboarding. Direct callback invocation verifies callback wiring and state transitions, but does not prove mouse hit-testing, keyboard focus, or usability. Tests expect intended behavior and should remain failed when the game violates an assertion; do not change assertions simply to make a run green.

Results and confirmed limitations are recorded in PLAYMODE_AUDIT_RESULTS.md after execution.
