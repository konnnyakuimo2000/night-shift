# NIGHT//SHIFT — DAY1 Vertical Slice 要件定義

最終更新: 2026-10-03

## 0. 目的

DAY1を 00:00〜04:00（実時間 約8分）通しでプレイでき、
「情報を見る → 調べる → 照合する → 判断する → 操作する → 他の仕事へ戻る」
というゲームループの面白さを検証できる状態にする。

## 1. 技術前提

| 項目 | 決定 |
|---|---|
| Unity | 6000.0.49f1（ローカルにインストール済み、既存プロジェクト Coin Rush と同一） |
| Render Pipeline | Built-in（2D UIのみのため） |
| UI | uGUI（コードで構築）+ Legacy `Text` + OSフォント（MS UI Gothic / MS Gothic / Meiryo） |
| Input | Legacy Input Manager（`UnityEngine.Input`）。OS全体のキー入力を横取りする仕組みは使わない |
| データ | ScriptableObject（`Assets/NightShift/Resources/NightShiftData/`）。エディタ拡張で初期データを生成 |
| Scene | `Assets/NightShift/Scenes/NightShift.unity`（1シーン構成、画面状態はGameManagerで遷移） |

UIをPrefabではなくコード生成にする理由: バッチモードで検証可能、マージ衝突がない、旧式業務ソフト風の統一部品（ベベル枠・青タイトルバー）を1か所で管理できるため。

## 2. 画面フロー

```
TITLE → CLOCK IN（タイムカード打刻）→ 社内システム ログイン
 → "DAY 1 — FIRST SHIFT" → 業務連絡（Morning Briefing）→ [START SHIFT]
 → 社内システム デスクトップ（00:00〜04:00 リアルタイム）
 → 03:50 終了予告 → 04:00 新規イベント停止 → 未処理要求の猶予 → SHIFT COMPLETE
 → 評価 → DAY 2 — COMING SOON（再挑戦 / タイトルへ）
```

## 3. 機能要件

### 3.1 時間
- ゲーム内 00:00〜04:00 = 240分。既定 2.0実秒/ゲーム分（= 8分）。`DayDefinition`で変更可。
- すべての業務Window・Remote Support表示中も進行。停止するのは Pause Menu（Esc）のみ。
- 開発用 TimeScale（x0.5〜x30）、任意時刻ジャンプ。

### 3.2 社内システム（NEXUS CORPORATION 社内情報システム Ver.4.21）
メインメニュー:
1. ネットワーク接続監視（Connection Request 処理）
2. 社員情報の検索・参照（Employee Directory）
3. システムログの検索・参照（Log Viewer）
4. ITサポート受付管理（Help Desk）
5. セキュリティ情報（業務ルール・Network Monitor）
6. システム設定（音量など）
7. 業務終了（04:00前は利用不可）
- 「サーバー運用管理」はメニューに灰色で表示し「権限がありません（DAY2解禁予定）」。

### 3.3 Window System
- 開く/閉じる/前面化/ドラッグ/最小化/複数同時表示/タスクバー。
- Alt+Tab（スタンドアロンではOSから取り込み）、エディタ用代替 Ctrl+Tab。
- Remote Support Windowがアクティブな間は、キー入力はリモートPCへ送られる（実際のRDPと同じ挙動）。

### 3.4 社員データ（データ駆動）
- `EmployeeData`(SO): EmployeeID, Name, Kana, Age, Department, Position, DeviceID, DeviceIP, WorkHours, OnShift, VPNEnabled, ITLiteracy, SecurityAwareness, NormalAccessTargets, NormalBehaviorProfile, Permissions[]。
- `PermissionDefinition`(SO): ID, 表示名, 区分（システム利用権限 / 管理者権限）, 並び順。Directoryの表示行はこの定義から生成（コードに表示名をハードコードしない）。
- **Permissions（権限）と NormalAccessTargets（通常利用）は別フィールド。**

### 3.5 ネットワーク定義
- `NetworkNodeDefinition`(SO): NodeID, Zone(External/DMZ/Internal), IsCritical, IsAdministrative, 提供するAccessType一覧（必要Permission・VPN要否）。
- DAY1ノード: INTERNET, FIREWALL, EMPLOYEE PCs, WEB-SRV01, MAIL-SRV01, FILE-SRV01, APP-SRV01, AUTH-SRV01, ADMIN-SRV01。
- サーバーはNetwork Monitor上のNodeとして見えるがクリックしても操作不可。

### 3.6 イベント（データ駆動）
- `EventDefinition`(SO): EventID, EventType, MinimumDay/MaximumDay, TriggerWindow, Weight, Severity, EmployeeConditions, SourceType, Destination, AccessType, IsMalicious, ExpectedAction（PolicyDerived / Allow / Block）, FollowUp, Timeout, PreferWhileRemoteActive, NotifyAsSecurityAlert。
- `EventInstance`: Definition, Employee, Device, GeneratedTime, Source, Destination, State, IsMalicious, PlayerAction, ResponseTime。
- `DayDefinition`: 固定スケジュール（骨格）＋ランダム枠（細部）。人物・端末・IP・発生時刻の揺らぎはランダム、学習順序は固定。
- EventDirector ├ TimelineScheduler ├ NormalEventGenerator ├ HelpDeskGenerator └ ThreatScenarioManager。
- 正解判定は `PolicyEvaluator` が**実際の社員データ**とDAY1ルールから算出 → 社員データを編集してもシナリオが壊れない。

### 3.7 DAY1 判断ルール（Morning Briefing で提示）
1. 登録済み社員端末から通常業務システムへの通信は原則許可
2. 未登録端末から内部重要システムへのアクセスは禁止
3. 外部から管理系システムへのアクセスは禁止
4. 管理系システムへの接続は、利用者の管理権限を確認すること
5. 社外からの接続はVPN利用許可のある社員の登録端末に限る

### 3.8 Connection Request
- 表示: Request ID / User / Employee ID / Device / Source / Destination / Access Type / Time。
- 操作: 接続を許可(A) / 接続を拒否(B)。結果表示は「要求を処理しました」のみ。危険項目の自動強調なし。
- 内部記録: Correct Allow / Correct Block / False Positive / Malicious Allow / Expired / Response Time。

### 3.9 Network Monitor / Log Viewer
- Network Monitor: Source → Destination の通信を線上のパケットで可視化。
- Log Viewer: Time / Source / Destination / User / Event Type / Result。スクロール、文字列フィルタ、種別フィルタ。自動相関なし。

### 3.10 Help Desk
- TYPE A（二択）: 2件。回答後「対応内容を送信しました」のみ。
- TYPE B（Remote Support）: 2件。症状のみ提示 → REMOTE CONNECT → 調査 → 操作 → 動作確認 → 「対応完了を報告」。
  - 判定はボタン押下ではなく `VirtualPCState` の最終状態（`ResolutionCondition`）。
  - ① インターネットにつながらない（Wi-Fi OFF） ② 印刷できない（Print Queue 一時停止）
  - 未解決で報告 → 社員から「まだ直っていません」→ 継続（2回目で失敗扱い）。
- Remote Sessionは中断（Window最小化/切断）・再開可能。状態は保持。

### 3.11 Remote PC（簡略化した実物PC）
- Desktop / Start Menu / 設定（ネットワーク・プリンター）/ タスクマネージャー / エクスプローラー / プリンターキュー / ファイル名を指定して実行 / コマンドプロンプト / ブラウザー。
- 疑似Terminal: `ipconfig`, `ipconfig /all`, `ping`, `hostname`, `whoami`, `help`, `cls`, `exit`（コマンドはレジストリ方式で追加可能）。
- Run: `cmd`, `ncpa.cpl`, `control printers`, `taskmgr`, `explorer`, `ms-settings:`, `msedge` 等。
- ショートカット: Ctrl+Shift+Esc / Win+R / Win+E / Win+I / Alt+Tab / Alt+F4 / Win+D / F5。
  - OS予約キー（Win系・Alt+Tab・Ctrl+Shift+Esc・Alt+F4）はOSが処理するため取り込まない。RDP互換の代替キー（Alt+Home=スタート、Ctrl+Alt+End=タスクマネージャー、Alt+PageUp/Ctrl+Tab=切替、Ctrl+F4=閉じる）＋ Ctrl+Alt+R/E/I/D で同じ機能に到達。マウスからも全機能に到達可能。
  - ショートカット使用によるスコア加点はしない。

### 3.12 シナリオ骨格（DAY1）
| 目安時刻 | 内容 | 正しい判断 |
|---|---|---|
| 00:03 | 正常アクセス（登録PC → MAIL/WEB） | 許可 |
| 00:12 | Directory照合（登録PC → FILE、FILE_ACCESSあり） | 許可 |
| 00:20 | 二択① パスワード共有の相談 | A |
| 00:30 | 社員名だが未登録端末 → FILE-SRV01 | 拒否 |
| 00:40 | Remote① ネットにつながらない（Wi-Fi OFF） | 状態で判定 |
| 00:45〜01:05 | 最初の不審アクセス UNKNOWN / UNKNOWN-14 / EXTERNAL → ADMIN-SRV01（Remote中に優先発生 = 同時発生演出） | 拒否 |
| 01:00〜 | スキャナーによるFIREWALL拒否ログ | ― |
| 01:25 | 一見怪しい正常: 開発部 登録DEV-PC → FILE-SRV01 | 許可 |
| 01:40 | 二択② 不審な添付ファイル | A |
| 02:00 | Remote② 印刷できない（キュー一時停止） | 状態で判定 |
| 02:15〜02:45 | Threat: 外部IP → WEB-SRV01 → 管理パス探索 → 関連IPから ADMIN-SRV01 ADMIN LOGIN | 拒否 |
| 02:40前後 | VPN許可社員の登録ノートPCから社外接続（正常） | 許可 |
| 03:20 | 最終問題: 営業部社員＋登録PC＋INTERNAL → ADMIN-SRV01 ADMIN LOGIN（権限なし） | 拒否 |
| 随時 | ランダムな正常要求 4件（全拒否戦略対策） | 許可 |
| 03:50 | 業務終了まで10分 | ― |
| 04:00 | 新規生成停止 → 猶予 → SHIFT COMPLETE | ― |

### 3.12.1 チュートリアル進行（DAY1）
- DAY1 はチュートリアルのため、イベント数を最小限にする（接続要求 計12件・相談2件・リモート2件）。
- **01:30 までは、保留中の要求・未対応の問い合わせが残っている間は次のイベントを出さない**（片付いてから2分後に次を出す。最大30分待って強制発生）。
- 例外: 「Remote Support 中の不審アクセス」は同時発生を体験させるため待機しない。
- 設定は `DayDefinition.tutorialGateUntilMinute / tutorialGateBreathMinutes / tutorialGateMaxDelayMinutes`。

### 3.12.2 業務連絡
- タスクバーの [業務連絡]、メインメニューの [業務連絡を見る(B)]、ヘルプメニューからいつでも再表示できる。
- 進行上重要なルールは太字・色文字で表示する（許可=青、禁止=赤、要確認=茶）。データ側のリッチテキストで指定。

### 3.13 通知
Connection Request / Help Desk Ticket / Security Alert / Shift終了10分前。トースト＋仮SE（手続き生成音）。クリックで該当Windowを開く。

### 3.14 評価
SECURITY / ACCURACY / HELP DESK / RESPONSE TIME / AVAILABILITY と総合ランク。
全拒否戦略はAVAILABILITY・ACCURACYが大きく下がるため高得点にならない。

### 3.15 Debug（F12 パネル / F11 オーバーレイ）
TimeScale、時刻ジャンプ、Normal/Request/二択/Remote/Threat 発生、Shift終了、DAY1 Restart。
オーバーレイ: Current Time / Active Events / Pending Requests / Active Tickets / Active Remote Session。

## 4. DAY1で実装しないもの
Server Management / Server Remote Control / MFA / GeoIP / EDR / SIEM / NDR / DLP / SOAR / Sentinel AI / AI Attacker / Ransomware / Lateral Movement / 高度なMalware / Patch Management / Budget / Tool Shop / DAY2〜7本編。
→ `NetworkNodeDefinition`・`DayDefinition.unlockedFeatures`・`EventDefinition.MinimumDay` などの拡張点のみ用意。

## 5. 実装フェーズ

| Phase | 内容 |
|---|---|
| 1 | GameManager / TimeManager / DayManager / Shift Start/End |
| 2 | RetroUI部品・WindowManager・タスクバー・Alt+Tab・入力ルーティング |
| 3 | EmployeeData / PermissionDefinition / Employee Directory |
| 4 | EventDirector / TimelineScheduler / NormalEventGenerator |
| 5 | Connection Request / Allow・Block / 結果記録 → **コンパイル・動作確認** |
| 6 | Log Viewer / Network Monitor |
| 7 | Help Desk TYPE A |
| 8 | VirtualPCState / Remote Support / Remote Desktop / ネットワーク設定 / プリンターキュー |
| 9 | Remote PCショートカット / Run / 疑似Terminal |
| 10 | Threat Scenario / Remote中のSecurity Alert |
| 11 | Morning Briefing / Notification / Evaluation |
| 12 | UI polish / 仮SE / Debug / 通しプレイ確認 |

## 6. 技術的リスク
1. **OS予約ショートカット**（Win+R, Alt+Tab, Ctrl+Shift+Esc, Alt+F4）はOSが先に処理する → 代替キーで対応。Alt+F4 はゲーム/エディタ自体を閉じるので注意（ゲーム内では Ctrl+F4 を使用）。
2. Legacy Text + OSフォントは環境依存（Windows前提）。フォールバックとしてArialを指定。
3. コード生成UIのためシーン上での見た目調整がしづらい → `RetroTheme` に色・寸法を集約。
4. ランダム要素で学習順序が崩れないよう、チュートリアル上重要なイベントは固定時間帯＋条件付き社員選択。
