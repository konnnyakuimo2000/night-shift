# PlayMode検証結果 — 2026-10-06

Unity 6000.0.49f1 / Windows / PlayMode / batchmode（描画有効）で、24件を実行。**22件成功、2件失敗、スキップ0件**。最終テスト実行時間は95.61秒（起動・コンパイル時間は別）。

## 実行条件

- Computer Useは不使用。ゲームの実GameObject、UIコールバック、GameManager.Updateを使用。
- Assets / Packages / ProjectSettingsの一時コピーを作り、コピーだけcompanyNameをNightShiftPlayModeAuditへ変更。PlayerPrefsを本番から分離。
- Runtime / EditorのC#ファイルのハッシュが、元プロジェクトとテスト用コピーで一致することを確認。
- ゲーム本体は修正していない。追加したのはテスト・実行スクリプト・ドキュメント。
- XML: `Logs/PlayModeAuditVerified.xml`。Unityログ: `Logs/PlayModeAuditVerified.log`。
- 実行手順: [PLAYMODE_AUDIT.md](PLAYMODE_AUDIT.md)。実行スクリプト: `Tools/Run-PlayModeAudit.ps1`。

## 修正後の再実行（2026-10-06）

下の2件を修正し、同じ手順（`Tools/Run-PlayModeAudit.ps1`）で再実行した。**24件すべて成功、失敗0件**。

- 1 → 新規ゲームの開始時に DAY1 のセーブ地点をすぐ作り、最高到達日も 1 に戻す（`StartNewGame` / `ClearSaves`）。
- 2 → 夜を片付けるとき（やり直し・タイトルへ戻る）に「業務完了 → 評価」のタイマーを取り消す（`DisposeShift`）。さらに、業務を終えた夜のときだけ評価へ進む（`Update`）。

## 再現した問題（修正前）

### 1. 新規ゲーム直後に過去の到達日が再開先として残る

テスト: `NewGame_DoesNotResumeOldHighestDay`。

1. 過去の最高到達日をDAY20にする。
2. 通常の新規開始処理StartNewGameを呼ぶ。
3. 初回の業務前画面へ進む前にResumeDayを確認する。

期待は0または1、実際は20。ClearSavesがCheckpointを消した後、ResumeDayが古いセーブとの互換処理でReachedDayにフォールバックする。新規ゲーム開始後、最初のShowBriefingでDAY1のチェックポイントが作られるまでの間に終了・再開すると、過去の到達日を再開先にしうる。

根拠: `Scripts/Runtime/Core/GameManager.cs` のResumeDay（319行付近）、ClearSaves（352行付近）、StartNewGame（483行付近）。新規ゲーム時にDAY1のチェックポイントを作るか、旧セーブへのフォールバックと新規開始済み状態を区別する必要がある。

### 2. 完了直後の再スタートで古い評価タイマーが残る

テスト: `Complete_RestartBeforeEvaluationDoesNotCrash`。

1. DAY8を開始し、ShiftSession.Completeで業務完了へ進める。
2. 評価画面が出るまでの3.5秒の間にGameManager.RestartDayを呼ぶ。
3. 4秒後の状態を確認する。

期待はBriefing、実際はEvaluation。新しいシフトが未開始のまま評価へ送られる。RestartDay / DisposeShiftが_completeTimerを無効化せず、Updateが以前のシフトのタイマーでShowEvaluationを呼ぶ。

根拠: `Scripts/Runtime/Core/GameManager.cs` のRestartDay（599行付近）、Update（635行付近）。画面遷移・破棄時に古い完了タイマーをキャンセルするか、完了したSessionとの一致を確認する必要がある。

このケースは公開メソッドを直接呼んだ境界テスト。通常のマウス操作で同じ時間帯に再スタートできるかまでは検証していないため、通常プレイで必ず起きるとは断定しない。

## 成功した範囲

- 全30日の業務前画面を生成し、メール画面の存在を確認。
- 11種類のツールを起動・終了し、未処理例外がないことを確認。
- 業務開始前の時計停止、開始後の進行、一時停止と復帰。
- 許可・拒否ボタンからの要求処理、処理後の許可ボタン無効化。
- 報告後のボタン無効化、および同じコールバックを再度呼んでも重複加点しないこと。
- 自動審査の初回停止、有効化ボタン、なりすましを保留、メインモニター表示中の自動処理、閉じた後の停止。
- ウィンドウの再表示で重複しないこと、最小化からの復帰。
- 未解決問い合わせがある間の終業猶予、解決後の終了、猶予切れ時の未解決計上。
- 残業時のコーヒー1本消費、開始の重複呼び出しで追加消費しないこと、保留要求の維持。
- 当夜の再挑戦で財布・所持品・要求状態を巻き戻すこと。
- ContinueGameでチェックポイントの信用・所持品・財布・日付を復元すること。
- 週の再挑戦で信用・現在日・再開日を巻き戻すこと。
- 終了→評価→翌日、重大被害→評価→ゲームオーバー→当夜の再挑戦。
- DAY8 / DAY20 / DAY30は実際のGameManager.Updateで一晩進め、評価・給与・夜の記録が生成されること。
- 1280×800・UI150%で接続監視の許可ボタンが画面内にあること。`Logs/AuditConnection150.png`を保存・目視確認。

## 限界

一晩のテストは120倍速で正解値を参照する操作役を使用。リモート案件をすべて直すことや攻撃をすべて防ぐことは要求していない。人間のクリア率・作業時間・ゲームバランスを測った結果ではない。

UIコールバックの呼び出しは、OSマウス入力・実際のクリック判定・キーボードフォーカスまで保証しない。150%の画像では通知が下部の一部に重なる。座標検証の対象は許可ボタンであり、全画面・全ボタンの視認性や操作可能性を保証しない。

初回検証の報告テストは、1回目で正しく無効化されたボタンを通常クリックしようとして失敗した。テストを「無効化を検証した後、バックエンドの重複防止を別途確認」に修正した。最終24件の結果には、このテスト側の誤りは含まれない。

## ケース別結果

| テスト | 結果 | 秒 |
|---|---|---:|
| AllThirtyDays_BriefingScreensBuild | Passed | 2.29 |
| AllTools_OpenAndCloseWithoutExceptions | Passed | 0.65 |
| AutoReview_ContinuesOnMainMonitor_StopsWhenClosed | Passed | 0.23 |
| AutoReview_EnableButtonProcessesOnlyMatchingRequests | Passed | 0.21 |
| AutoReview_FirstLaunchDoesNotProcessPending | Passed | 0.19 |
| Briefing_ClockDoesNotAdvance | Passed | 0.27 |
| Complete_EvaluationAndNextDay | Passed | 3.75 |
| Complete_RestartBeforeEvaluationDoesNotCrash | Failed | 4.23 |
| Connection_150PercentButtonsFitCanvas | Passed | 0.34 |
| Connection_AllowButtonProcessesExactlyOnce | Passed | 0.21 |
| Connection_BlockAndRepeatedReport | Passed | 0.18 |
| Continue_RestoresCheckpointInsteadOfMidnightMutations | Passed | 0.19 |
| Day08_FullNightThroughRealUpdate | Passed | 9.09 |
| Day20_FullNightThroughRealUpdate | Passed | 34.08 |
| Day30_FullNightThroughRealUpdate | Passed | 34.12 |
| FatalIncident_EvaluationToGameOverToRetry | Passed | 3.80 |
| Grace_TimeoutRecordsUnresolved | Passed | 0.18 |
| Grace_WaitsForHelpDesk_ThenCompletes | Passed | 0.17 |
| NewGame_DoesNotResumeOldHighestDay | Failed | 0.12 |
| Overtime_ConsumesOneCoffee_AndPreservesRequests | Passed | 0.18 |
| Restart_RestoresWalletInventoryAndSession | Passed | 0.22 |
| StartPauseResume_UsesGameManagerUpdate | Passed | 0.41 |
| WeekRetry_ChangesResumeDayAndRestoresCareer | Passed | 0.24 |
| Window_ReopenDoesNotDuplicate_AndRestoresMinimized | Passed | 0.20 |
