# NIGHT//SHIFT 音響実装・Claude向け引き継ぎ

## 目的と実装範囲

夜間のオフィス、古い業務端末、理解できない怪異に合わせた控えめな電子音響を追加した。
外部楽曲・サンプル・音声生成サービスは使用していない。固定の譜面・波形合成によるオリジナル音源。
ゲームの審査、報酬、時計、クエスト進行には変更を加えない。

## BGM・環境音

音源フォルダ: `Assets/NightShift/Resources/Audio/NightShift/`

| ファイル | 長さ | 再生条件・狙い |
|---|---:|---|
| Title.wav | 32秒 | タイトル。低いパッドと間隔を空けた柔らかい鍵盤 |
| Office.wav | 32秒 | 通常業務、ログイン、業務連絡。旋律を間引き、読む作業を邪魔しない |
| Pressure.wav | 32秒 | 業務中の保留要求が5件以上。控えめな低音パルス。5件未満になっても8秒保持して頻繁な切替を防ぐ |
| Anomaly.wav | 32秒 | SQ03がActiveかつ最初のPC不調を経験済み（FX_DAY）。言葉や人の声を使わず、うなり・不協和音・膨らむノイズで表現 |
| Dawn.wav | 32秒 | 業務完了・評価画面、深刻な被害でない本編エンド、下記以外のサブエンド。緊張をほどく短い旋律 |
| Aftermath.wav | 32秒 | 本編GO、Criticalの本編エンド、SE-HB01 / SE-HT01 / SE-M01 / SE-N01 / SE-N02。暗い低音と余白 |
| Room.wav | 8秒 | 業務連絡・業務中に重ねる空調・電源・遠いファンの音 |

全音源はモノラル22,050Hz・16bit PCMのWAV。UnityインポートはVorbis品質0.75、DecompressOnLoad、先読み有効。
同じ音源をキャッシュし、トラックを変えるたびに作り直さない。Resourcesに存在しない場合だけ同一譜面から実行時合成する。
音楽は2つのAudioSourceで2.5秒のクロスフェード。環境音と効果音は別AudioSource。

曲調の優先順位は「画面に付いた終端用指定 → タイトル／結果画面 → 怪異 → 繁忙 → 通常」。
繁忙判定は表示中の保留件数だけを使う。悪意・正解・未来の脅威を音で漏らさない。
怪異をオファーしただけでは怪異BGMを鳴らさない。「ホラー控えめ」ではAnomalyを選択しない。

## 効果音と音量

既存の通知・警告・起動・接続・リザルト音は既存呼び出しを維持。
クリック・文字送り・スタンプを短い機械的な過渡音へ変更し、CRT音は22,050Hzで扱えない15,600Hzから穏やかな2,300Hz付近へ変更。
暗いサブエンドでは明るい完了チャイムを鳴らさず、既存の停止音を使う。

- 設定画面に「全体音量」「BGM」「環境音」「効果音」を追加。既定値は60% / 55% / 40% / 100%。
- 全体音量は全チャンネルへ掛ける。効果音を下げてもBGMには影響しない。
- 通知・警告・問い合わせ音が鳴るとBGMを0.8秒間35%へ下げ、急な段差を丸めて戻す。
- 同じ音の連打を抑制: 警告0.8秒、通知・問い合わせ0.18秒、それ以外0.035秒。
- 業務時計が一時停止中はBGM・環境音を25%へ。UIの操作音は使用可能。
- アプリがフォーカスを失ったらBGM・環境音・効果音をミュート。復帰後は設定値へ戻す。
- 「急な大きな音」オフは既存のホラーSE音量制限を継承。
- UI専用シーンでAudioListenerが存在しない場合だけ追加。初期化を重ねても音源コンポーネントを増やさない。
- 旧設定は新しい音量の既定値を補う。明示的に保存した0%は維持する。

## 主なファイル

| ファイル（Assets/NightShift/ 以下） | 役割 |
|---|---|
| Scripts/Runtime/UI/SoundtrackLibrary.cs | 固定譜面、合成波形、Resources読込、キャッシュと解放 |
| Scripts/Runtime/UI/Soundscape.cs | 再生状態、クロスフェード、ダッキング、環境音、フォーカス、SoundtrackCue |
| Scripts/Runtime/UI/AudioFx.cs | 既存SE API、機械音、連打制限、効果音音量。InitからSoundscapeを追加 |
| Scripts/Runtime/Core/GameSettings.cs | 3チャンネルの音量保存、FromJsonOverwriteによる旧設定読込 |
| Scripts/Runtime/UI/SettingsPanel.cs | 4つの音量バー |
| Scripts/Runtime/UI/Screens/EndingScreens.cs | 本編エンド・GO画面へSoundtrackCueを付与 |
| Scripts/Runtime/UI/Screens/SubEndingScreens.cs | サブエンド画面の曲調と終端SE |
| Scripts/Editor/SoundtrackBaker.cs | WAV再生成・インポート設定 |
| Tests/EditMode/SoundtrackTests.cs | 波形、決定性、曲調選択、音量、旧設定・ミュート、同梱音源 |
| Tests/PlayMode/SoundscapePlayModeTests.cs | コンポーネント重複、画面終了時の曲調復帰、全体／フォーカスミュート |

GameManagerの既存 `AudioFx.Init(gameObject)` から起動するため、シーンやPrefabへの手作業での追加は不要。
終端画面を閉じるとSoundtrackCueも破棄される。終端の曲調をタイトルや再挑戦へ持ち越さない。

## Claudeが変更を引き継ぐ場合

1. この資料と上記ファイルを読む。共同作業中のため、他のUI・審査・クエストの差分を戻さない。
2. 音色・譜面を変えるなら `SoundtrackLibrary.Render` を編集する。設定UIのラベルに技術用語を持ち込まない。
3. Unityメニュー `NightShift > Audio > Rebuild Original Soundtrack` で7本のWAVを再生成する。
   同じ名前のWAVを上書きするため、手作業で差し替えた音源がある場合は先に保管する。
4. 完成音源に差し替える場合はResourcesの同じパス・同じ名前を維持。コード変更は不要。継ぎ目の自然なループ素材を使う。
5. `dotnet build` とEditModeの `NightShift.Tests.SoundtrackTests` を確認する。
6. 再生側は隔離コピーで次を実行する（既存の監査スクリプト）。

```powershell
./Tools/Run-PlayModeAudit.ps1 -Filter 'NightShift.Tests.SoundscapePlayModeTests'
```

### 試聴で確認する点

自動テストは波形・状態・音量を確認するもので、主観的な聴感の合格判定ではない。
ヘッドホンとスピーカーで、通常業務の読みやすさ、ループの継ぎ目、通知の聞き取り、長時間の低音疲れを確認する。
通知音をオフにした場合も視覚通知は既存どおり表示される。

### 検証ログ

- WAV生成: `Logs/AudioBake.log`
- EditMode: `Logs/Audio-EditMode.xml` / `Logs/Audio-EditMode.log`
- PlayMode: `Logs/PlayModeAudit-20261008-131559/results.xml`（対象クラスはSoundscapePlayModeTests）
- 2026-10-08 検証結果: WAV7本を生成。EditModeは20件、隔離コピーでのPlayModeは3件がすべて成功（失敗・スキップなし）。
- `dotnet build --nologo -v:q` 成功（警告0・エラー0）。

Claudeへの依頼文例:

> Assets/NightShift/Docs/AUDIO_HANDOFF.md を読み、音響実装を引き継いでください。BGMは6曲、環境音は1本です。通常の審査・クエスト進行には触れず、音源・音量・切り替え条件だけを調整してください。生成WAVを差し替える場合は同じResourcesパスを保ち、手作業の音源をBakerで上書きしないでください。
