# NIGHT//SHIFT

Corporate IT Operations / Cyber Security Task Management Game（Unity）

**v1.1.0** — 1998年の深夜のオフィスで、社内ネットワークの接続審査と IT サポートを担当する30日間。DAY1〜7 の研修と DAY8〜30 の本番を通しでプレイできる。変更点は [CHANGELOG](CHANGELOG.md) を参照。

## 遊ぶ

- [Releases](https://github.com/konnnyakuimo2000/night-shift/releases/latest) から `NIGHT_SHIFT_v1.1.0_Windows.zip` をダウンロードして展開し、`NIGHT_SHIFT.exe` を起動（Windows 64bit）
- ビルドのしかた: Unity のメニュー `NightShift > リリース版をビルド（Windows）`、またはコマンドライン
  `Unity.exe -batchmode -quit -projectPath <このフォルダー> -executeMethod NightShift.EditorTools.ReleaseBuild.BuildWindows`

## 動作環境

- Unity **6000.0.49f1**（Built-in Render Pipeline / uGUI / 旧 Input Manager）
- Windows（UIフォントに MS UI Gothic / MS Gothic を使用）

## 初回セットアップ

1. Unity Hub にログインし、`night_shift` フォルダーをプロジェクトとして追加して開く
2. 初回オープン時に自動で以下が生成される（手動: メニュー `NightShift > ...`）
   - `Assets/NightShift/Resources/NightShiftData/` … DAY1〜30 のデータアセット（社員・権限・ネットワーク・イベント・Help Desk・攻撃シナリオ・商品・マニュアル）。`DefaultDataFactory.DataVersion` が上がると自動で作り直す
   - `Assets/NightShift/Scenes/NightShift.unity` … メインシーン（Build Settings に登録済み）
3. `NightShift.unity` を開いて Play

> データアセットが無くても、どのシーンで Play しても組み込みの初期データで起動します。

## データの編集

社員・権限・イベントなどはすべて ScriptableObject です。`Resources/NightShiftData` 以下のアセットを Inspector で編集すれば、コード変更なしで反映されます。
接続要求の「正しい判断」はイベントに固定せず、その日のルール（`DayDefinition.policy`）と実際の社員データから算出されます。

## 操作・設定

タイトル画面の **[設定]**、またはゲーム中の **[システム設定]** から変更できます（PlayerPrefs に保存）。

- グラフィック: 画面モード / 解像度 / UI の拡大率 / フレームレート / VSync
- サウンド: 音量 / クリック音 / 通知音
- 操作: 文字キーだけでボタンを押す / 通知の表示時間 / デバッグモード（タイトルから任意の日を開始・F12 デバッグパネル）
- キー設定: 下表のキーを変更可能（重複したキーは入れ替え）。マニュアル中のキー表記も設定に合わせて変わります。

| 操作 | 既定のキー |
|---|---|
| 一時停止 | Esc |
| ウィンドウ切替 / 閉じる | Ctrl+Tab / Ctrl+F4 |
| メールを開く | Ctrl+M |
| 接続を許可 / 拒否 / 社員情報を参照 | A / B / R（接続監視画面） |
| リモートPC: スタート / 実行 / エクスプローラー / 設定 / デスクトップ / タスクマネージャー / 更新 | Alt+Home / Ctrl+Alt+R / Ctrl+Alt+E / Ctrl+Alt+I / Ctrl+Alt+D / Ctrl+Alt+End / F5 |
| デバッグパネル / 表示 | F12 / F11 |

Win キー系・Alt+Tab・Ctrl+Shift+Esc は OS が処理するため、ゲームでは上記のキーを使います（OS が処理しなかった場合は実機と同じキーも受け付けます）。

## ドキュメント

- [DAY1 要件定義](Assets/NightShift/Docs/DAY1_Requirements.md) ／ [DAY8〜 要件定義](Assets/NightShift/Docs/DAY8_Requirements.md)
- [ツール一覧](Assets/NightShift/Docs/TOOL_CATALOG.md) ／ [ウイルスと被害](Assets/NightShift/Docs/VIRUS_CATALOG.md) ／ [攻撃シナリオ](Assets/NightShift/Docs/THREAT_CATALOG.md)
- [ITサポートの問題](Assets/NightShift/Docs/HELPDESK_CATALOG.md) ／ [エンディング](Assets/NightShift/Docs/ENDINGS.md) ／ [ネットニュース](Assets/NightShift/Docs/NEWS_DESIGN.md) ／ [前任者](Assets/NightShift/Docs/PREDECESSOR.md)
- [サブクエスト](Assets/NightShift/Docs/SUBQUEST_SPEC.md) ／ [バランス](Assets/NightShift/Docs/BALANCE.md)
- テスト: [30日の通しプレイの結果](Assets/NightShift/Docs/CAMPAIGN_PLAYTEST.md) ／ [PlayMode の監査](Assets/NightShift/Docs/PLAYMODE_AUDIT.md)（隔離したプロジェクトのコピーで実行。セーブを消すため）

## テスト

- EditMode: Unity の Test Runner、または `Unity.exe -batchmode -runTests -testPlatform EditMode -projectPath <コピー>`（プレイヤーのセーブには触れない）
- PlayMode: `Tools/Run-PlayModeAudit.ps1` が プロジェクトのコピー（会社名 NightShiftPlayModeAudit）で実行する。30日の通しプレイは `NightShift.Tests.CampaignPlayTests`

## ライセンス

All Rights Reserved。遊ぶことと、ソースコードの閲覧は自由です。複製・再配布・改変は許可していません。詳しくは [LICENSE](LICENSE) を参照してください。
