# NIGHT//SHIFT

Corporate IT Operations / Cyber Security Task Management Game（Unity）

現在の内容: **DAY 1 — FIRST SHIFT** の Vertical Slice（00:00〜04:00、実時間 約8分）

## 動作環境

- Unity **6000.0.49f1**（Built-in Render Pipeline / uGUI / 旧 Input Manager）
- Windows（UIフォントに MS UI Gothic / MS Gothic を使用）

## 初回セットアップ

1. Unity Hub にログインし、`night_shift` フォルダーをプロジェクトとして追加して開く
2. 初回オープン時に自動で以下が生成される（手動: メニュー `NightShift > ...`）
   - `Assets/NightShift/Resources/NightShiftData/` … DAY1 データアセット（社員・権限・ネットワーク・イベント・Help Desk・Threat）
   - `Assets/NightShift/Scenes/NightShift.unity` … メインシーン（Build Settings に登録済み）
3. `NightShift.unity` を開いて Play

> データアセットが無くても、どのシーンで Play しても組み込みの初期データで起動します。

## データの編集

社員・権限・イベントなどはすべて ScriptableObject です。`Resources/NightShiftData` 以下のアセットを Inspector で編集すれば、コード変更なしで反映されます。
接続要求の「正しい判断」はイベントに固定せず、その日のルール（`DayDefinition.policy`）と実際の社員データから算出されます。

## 操作

| 操作 | キー |
|---|---|
| 一時停止 | Esc（リモート画面以外がアクティブな時） |
| ウィンドウ切替 | Ctrl+Tab / Alt+PageUp |
| ウィンドウを閉じる | Ctrl+F4 |
| 接続を許可 / 拒否 | A / B（接続監視画面） |
| Debug パネル / オーバーレイ | F12 / F11 |

リモートPC操作中（リモート画面がアクティブな時）:

| 機能 | キー |
|---|---|
| スタートメニュー | Alt+Home |
| ファイル名を指定して実行 | Ctrl+Alt+R |
| エクスプローラー / 設定 / デスクトップ | Ctrl+Alt+E / I / D |
| タスク マネージャー | Ctrl+Alt+End |
| 更新 | F5 |

Win キー系・Alt+Tab・Ctrl+Shift+Esc は OS が処理するため、ゲームでは上記の代替キーを使います（すべてマウスからも操作可能）。

## ドキュメント

- [DAY1 要件定義](Assets/NightShift/Docs/DAY1_Requirements.md)
