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
TITLE →「出勤する」（タイムカード打刻）→ 社内システム ログイン
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

### 3.12 チュートリアル構成（DAY1〜7）
日ごとに使えるシステムとルールを段階的に増やす。未解放のシステムはメニュー・ボタン・キー操作・通知のいずれからも開けない（`DayDefinition.unlockedFeatures`）。

| DAY | 解放 | ルール（NEW/変更） | 主なイベント |
|---|---|---|---|
| 1 FIRST SHIFT | 接続監視・社員検索（一覧のみ、端末情報は非表示） | ①登録社員は許可 ②社員として登録されていない利用者は拒否 | 退職者（名簿にない社員番号）、UNKNOWN（外部）、guest |
| 2 DEVICES | ＋端末の確認・社員詳細（顔写真・登録端末） | ③本人の登録端末ではない端末は拒否（①変更） | 私物PC、他人のPC、**パスワード変更依頼** |
| 3 ACCESS RIGHTS | ＋権限情報（利用システムの権限のみ）、接続要求の「必要な権限」欄 | ④要求の「必要な権限」と同じ名前の権限が×なら拒否 | 人事（権限あり）、経理（権限なし）、開発部→共有ファイル（一見怪しいが正常）、CS→開発環境（権限なし） |
| 4 SERVERS | ＋社員詳細の【管理者権限】欄・社内サーバー構成資料（参考の早見表） | 復習（手順は DAY3 と同じ：「必要な権限」と同じ名前の行を見る） | メール・経理・開発環境（権限あり）、営業→人事システム（権限なし）、ローカル管理者のみの社員→リモート管理（サーバー管理者が必要） |
| 5 HELP DESK | ＋ITサポート受付（相談・リモート） | 復習 | パスワード電話の相談、Wi-Fi修理、権限なし要求 |
| 6 OUTSIDE THE WALL | ＋ログ・ネットワークマップ・セキュリティ情報（構成資料にネットワーク区分を追加） | ⑤社外は「VPN接続」だけ・VPN利用「許可」・VPN端末から なら許可、それ以外は拒否（管理者でも） | 正常VPN、VPNを通さない社外→共有ファイル、VPN不許可、スキャンしたIPからの管理ログイン（ログで同じIP）、自宅のサーバー管理者→管理ログイン |
| 7 LONG NIGHT | 総仕上げ | 復習 | 印刷修理中の不審アクセス（唯一の意図的な同時発生）、ログ照合の攻撃チェーン、退職者のVPN |

- チュートリアル中は接続要求の**期限切れなし**（`DayDefinition.requestsExpire = false`）。04:00 後も未処理の要求をすべて処理するまで業務終了を待つ。
- チュートリアル中は**終日、プレイヤーのタスク（保留中の要求・未対応の問い合わせ・作業依頼）が片付くまで次のイベントを出さない**。強制発生（最大待機）は無し。例外は DAY7 の Remote 中の不審アクセスのみ。
- DAY7 の後は「DAY 8 — COMING SOON」。

### 3.12.2 運用担当者アカウント（パスワード）
- DAY2 の業務中に「初期パスワードの変更」依頼が届く（`DayDefinition.passwordChangeAtMinute`）。変更するまで次のイベントは発生しない。
- 規程（6文字以上・英字＋数字・IDと同一不可・現行と同一不可）は緩めで、規程を満たす弱いパスワードも作れる。
- 記録する項目（`OperatorAccount`、PlayerPrefs に保存）: パスワード、変更日、強度(0-4)、弱点（短い／よくある単語／ID を含む…）、**付箋に書いたか**、**初期化回数**。
- ログイン画面: 初期パスワードの間は前任者の付箋、変更後は「付箋に書いた」場合だけ新しいパスワードの付箋が貼られる。変更した後は、業務メニュー・システム設定・付箋のクリックから、いつでも貼る／はがすを選べる。「パスワードを忘れた場合」で初期化できる（回数を記録）。
- **後の日の脆弱性要素**: 弱いパスワード・付箋・初期化の多用を、攻撃者による不正ログインの成否や評価に反映する（DAY7 以降で実装予定）。

### 3.12.4 給与（後のストアで使用）
- 評価画面で給与明細を表示: 基本給 ＋ 評価手当（総合点×単価）＋ ランク賞与 − 減額（不正接続・ルール違反の許可）。計算式は `GameDatabase.payroll`。
- 所持金は `OperatorWallet`（PlayerPrefs）。同じ日をやり直した場合は**その日の最高支給額だけ**を計上（稼ぎ直し防止）。`TrySpend` をストアで使う。
- 翌日の受信トレイに「給与明細」メールが届く。

### 3.12.5 デバッグモード
- 設定の [操作] → 「デバッグモード」。オンでタイトルに「DEBUG 開始日: DAY1〜7」が表示され、任意の日から開始できる。デバッグパネル（F12）・オーバーレイ（F11）もデバッグモード時のみ。
- エディタ実行時は既定でオン。

### 3.12.3 社員詳細の顔写真
- `EmployeeData.portrait` に画像を設定すれば使用。未設定なら社員番号から証明写真風のドット絵を自動生成（`PortraitPainter`）。

### 3.12.1 メール（業務連絡）
- 業務連絡・作業依頼はすべて「メール」（受信トレイ）に届く。過去日の業務連絡も既読で残る。
- 業務開始前はメールに今日の業務連絡が開いた状態で表示され、START SHIFT もここから行う。
- タスクバーの [メール (未読数)]、メインメニューの [メールを見る(B)] からいつでも開ける。
- 業務連絡には、その日のルールに合わせた「接続審査の流れ」の図が入る。
- マニュアルは**業務ごとのページ**（`DocumentSection.title`）。左に目次、右にページ。新しい業務が解放された日にページが増え、目次に NEW が付く。
- 各ページに**実際の画面と同じ項目名・配置の図**（`ManualUiFigures`）を入れ、見るべき場所を赤枠と番号で示す。審査フロー・社外からの接続・ネットワーク区分・リモート対応の流れの図（`ManualFigures`）もある。
- 業務開始前の読了条件は「メール本文」＋「今日新しく増えたページをすべて最後まで読む」。
- タスクバーの [業務連絡]、メインメニューの [業務連絡を見る(B)]、ヘルプメニューからいつでも再表示できる。
- 添付ファイルはメール上部の目立つ枠に大きなアイコンで表示。
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
