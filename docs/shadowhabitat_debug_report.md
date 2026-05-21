# ShadowHabitat デバッグ相談レポート

作成日：2026-05-21  
対象：Unity＋Python連携による影検出・エージェント制御デバッグ  
目的：Unity／Python間のOSC通信，背景学習，影検出，エージェント自己検出問題を切り分ける．

---

## 1. 結論

今回のログから，UnityとPython間のOSC通信そのものは成立している．  
主問題は，黒丸エージェント自身がカメラ画像上で暗領域として検出され，Python側の影マスクに含まれている点である可能性が高い．

したがって，次の修正が必要である．

1. 背景学習中はエージェントの見た目を消す．ただしGameObject本体は無効化しない．
2. 通常検出時は，Unityから受け取ったエージェント座標を使って，エージェント自身の円領域を影マスクから除外する．
3. Pythonのデバッグ表示で，影マスク，agent円，除外領域が正しく対応しているか確認する．

---

## 2. システム構成の前提

### 2.1 Unity側

Unity側では，主に以下のコンポーネントが関係している．

- `SystemController`
  - Pythonからの `/system/learn_start`，`/system/learn_end` を受信する．
  - 学習中の表示切替を行う．
- `AgentStateBroadcaster`
  - Unity内のエージェント状態をPythonへOSC送信する．
- `ShapeAgent`
  - 黒丸エージェントの状態遷移，移動，影回避などを担う．
- `AgentCsvLogger`
  - エージェントログをCSV出力する．

### 2.2 Python側

Python側では，主に以下の処理が関係している．

- `main.py`
  - カメラ画像取得
  - 背景学習
  - 影検出
  - Unityへの学習開始／終了通知
  - Unityから受け取ったagent stateの利用
- `osc_receiver.py`
  - `/agent/state` の受信
- `calibrate.py`
  - 投影面4隅のキャリブレーション
  - `calibration.json` の保存

---

## 3. 相談の流れ

### 3.1 最初の疑問

Python側ログでは，`agents/frame coverage=0%` が長く続くことがあり，Unityからの `/agent/state` が届いていないように見えた．

初期仮説は以下だった．

- Unity側のOSC送信設定が不完全
- Python側の受信ポートが違う
- `CircleAgent` が非アクティブまたはdisabledになっている
- 学習中の表示切替でエージェントが止まっている

### 3.2 Unity側ログによる確認

Unity Consoleには，以下のようなログが出ていた．

```text
[AgentStateBroadcaster:OscReceiver] OnEnable. client=OK  surface=Surface  agents.Count=1
  agents[0] = CircleAgent  active=True  enabled=True

[SystemController:OscReceiver] Awake.  uOscServer=OK  hideDuringLearn.Count=1  learnFlashSurface=LearnFlash
  hideDuringLearn[0] = CircleAgent  active=True

[SystemController] SetLearningVisible(False)  toggled=1  null_entries=0  flashSurface=hidden

[AgentCsvLogger] writing to C:/Users/demo/AppData/LocalLow/DefaultCompany/ShadowHabitat\logs\agent_20260521_230901.csv

[ShapeAgent:CircleAgent] Start. pos=(0.00, 0.00, 0.00)  surface=Surface  shadowManager=ShadowManager  firstTarget=(-3.05, 1.64)
```

この時点で，Unity側の参照設定は概ね正常と判断できる．

- `client=OK`
- `surface=Surface`
- `agents.Count=1`
- `agents[0] = CircleAgent active=True enabled=True`
- `uOscServer=OK`
- `hideDuringLearn.Count=1`
- `hideDuringLearn[0] = CircleAgent active=True`

つまり，初期設定漏れではない．

---

## 4. Unity側OSC送信の診断

### 4.1 HBログ

Unity側では，`AgentStateBroadcaster` の heartbeat ログが出ていた．

```text
[AgentStateBroadcaster:OscReceiver] HB sends=446  skipNullAgent=0  skipDisabledAgent=0  skipInactiveGo=0  skipNoClient=0  skipNoSurface=0  agents.Count=1

[AgentStateBroadcaster:OscReceiver] HB sends=575  skipNullAgent=0  skipDisabledAgent=0  skipInactiveGo=0  skipNoClient=0  skipNoSurface=0  agents.Count=1

[AgentStateBroadcaster:OscReceiver] HB sends=581  skipNullAgent=0  skipDisabledAgent=0  skipInactiveGo=0  skipNoClient=0  skipNoSurface=0  agents.Count=1

[AgentStateBroadcaster:OscReceiver] HB sends=568  skipNullAgent=0  skipDisabledAgent=0  skipInactiveGo=0  skipNoClient=0  skipNoSurface=0  agents.Count=1
```

### 4.2 判定

このログから，Unity側では送信処理が動いている．  
また，以下のスキップ要因は発生していない．

| 項目 | 値 | 判定 |
|---|---:|---|
| `skipNullAgent` | 0 | agent参照切れではない |
| `skipDisabledAgent` | 0 | agentコンポーネントdisabledではない |
| `skipInactiveGo` | 0 | GameObject inactiveではない |
| `skipNoClient` | 0 | OSC clientなしではない |
| `skipNoSurface` | 0 | Surface参照なしではない |

したがって，Unity側の送信設定や参照設定は正常である可能性が高い．

---

## 5. Python側受信ログの診断

### 5.1 受信成功ログ

Python側では，以下のようなログが出ていた．

```text
PS C:\Users\demo\Desktop\ShadowHabitat\python> python main.py
[AgentStateReceiver] listening on 0.0.0.0:9001
[main] sent /system/learn_start (8.0s) to Unity
[main] running. press 'q' to quit, 'b' to relearn background, 'd' to toggle debug
[diag] agents/frame coverage=0% (0/1 frames had agents)
[diag] agents/frame coverage=0% (0/20 frames had agents)
[AgentStateReceiver] FIRST /agent/state id=0 surface=plane x=0.500 y=0.500 r=0.040
[diag] agents/frame coverage=10% (2/21 frames had agents) first_agent: id=0 x=0.500 y=0.500 r=0.040 surface=plane
[AgentStateReceiver] FIRST /agent/state id=0 surface=plane x=0.500 y=0.495 r=0.040
[main] sent /system/learn_end to Unity
[diag] agents/frame coverage=38% (8/21 frames had agents)
```

その後，キャリブレーション後には以下のように，より安定した受信区間も見られた．

```text
PS C:\Users\demo\Desktop\ShadowHabitat\python> python main.py
[AgentStateReceiver] listening on 0.0.0.0:9001
[main] sent /system/learn_start (8.0s) to Unity
[main] running. press 'q' to quit, 'b' to relearn background, 'd' to toggle debug
[diag] agents/frame coverage=0% (0/1 frames had agents)
[AgentStateReceiver] FIRST /agent/state id=0 surface=plane x=0.500 y=0.500 r=0.040
[diag] agents/frame coverage=5% (1/20 frames had agents) first_agent: id=0 x=0.500 y=0.500 r=0.040 surface=plane
[AgentStateReceiver] FIRST /agent/state id=0 surface=plane x=0.500 y=0.500 r=0.040
[diag] agents/frame coverage=86% (18/21 frames had agents) first_agent: id=0 x=0.340 y=0.460 r=0.040 surface=plane
[main] sent /system/learn_end to Unity
[diag] agents/frame coverage=100% (21/21 frames had agents) first_agent: id=0 x=0.419 y=0.454 r=0.040 surface=plane
[diag] agents/frame coverage=100% (20/20 frames had agents) first_agent: id=0 x=0.762 y=0.847 r=0.040 surface=plane
[diag] agents/frame coverage=100% (21/21 frames had agents) first_agent: id=0 x=0.959 y=0.535 r=0.040 surface=plane
[diag] agents/frame coverage=100% (20/20 frames had agents) first_agent: id=0 x=0.810 y=0.428 r=0.040 surface=plane
```

### 5.2 判定

Python側も `/agent/state` を受信できている．  
`coverage=100%` の区間があるため，OSC通信やポート設定は成立している．

一方で，次のような0%区間もある．

```text
[main] sent /system/learn_start (8.0s) to Unity
[main] background reset (handshake fired)
[diag] agents/frame coverage=14% (3/21 frames had agents)
[diag] agents/frame coverage=0% (0/21 frames had agents)
[diag] agents/frame coverage=0% (0/20 frames had agents)
[diag] agents/frame coverage=0% (0/21 frames had agents)
[main] sent /system/learn_end to Unity
[diag] agents/frame coverage=0% (0/21 frames had agents)
[diag] agents/frame coverage=0% (0/20 frames had agents)
[diag] agents/frame coverage=0% (0/21 frames had agents)
```

この0%は，当初は通信断やUnity側の送信停止として疑った．  
しかし後の相談で，UnityをPlayしたままPythonウィンドウへフォーカスしていたことが分かった．  
その場合，Unity Editorがバックグラウンドで更新を止める／遅くする設定になっていれば，Unity側の`Update()`が止まり，`/agent/state`送信も止まる．

このため，`coverage=0%`は主原因ではなく，Unity Editorのフォーカス挙動で説明できる可能性が高い．

---

## 6. 背景学習と表示切替

### 6.1 Unity側の学習開始／終了ログ

Unity側では，Pythonからの学習開始・終了通知を受信できていた．

```text
[SystemController] received OSC /system/learn_start  values.Length=1
[SystemController] SetLearningVisible(True)  toggled=1  null_entries=0  flashSurface=shown
[SystemController] BeginLearning 8.0s

[SystemController] received OSC /system/learn_end  values.Length=0
[SystemController] SetLearningVisible(False)  toggled=1  null_entries=0  flashSurface=hidden
[SystemController] EndLearning
```

### 6.2 判定

Python → Unity のOSC通信は成立している．  
`SetLearningVisible(True/False)` も成功している．  
`null_entries=0` なので，`hideDuringLearn` の参照切れではない．

ただし，`hideDuringLearn` に `CircleAgent` 本体を入れている場合，学習中に `CircleAgent.SetActive(false)` している可能性がある．  
これはOSC送信やログにも影響しうる．

ただし，今回の本丸はそこではなく，通常時に黒丸自身が影検出されている点である．

---

## 7. 本丸：黒丸自身を影として検出している問題

### 7.1 観察

ユーザーの観察として，黒丸自身を「逃げるべき影」と判定しているように見えた．  
これは，これまでのログと整合する．

Unity側の `ShapeAgent` ログでは，`Covered` と `AvoidShadow` が頻繁に出ていた．

```text
[ShapeAgent:CircleAgent] state=Covered  pos=(-1.15, 0.37)  vel=(-0.67, 0.20)  target=(-3.05, 1.64)  framesSinceStart=199

[ShapeAgent:CircleAgent] state=Covered  pos=(-1.17, 0.45)  vel=(0.00, 0.00)  target=(-3.05, 1.64)  framesSinceStart=447

[ShapeAgent:CircleAgent] state=AvoidShadow  pos=(0.67, -0.11)  vel=(2.23, -0.43)  target=(-3.05, 1.64)  framesSinceStart=1023

[ShapeAgent:CircleAgent] state=Covered  pos=(1.97, -1.36)  vel=(1.34, -1.69)  target=(-3.05, 1.64)  framesSinceStart=1312

[ShapeAgent:CircleAgent] state=AvoidShadow  pos=(3.80, -2.24)  vel=(1.27, -0.06)  target=(-3.05, 1.64)  framesSinceStart=1605
```

### 7.2 推定される原因

黒丸エージェントは，プロジェクタで投影された黒い図形である．  
カメラ画像上では，黒丸は背景より暗い領域として写る．

背景差分や明度差に基づいて影を検出している場合，Python側からは次の区別が難しい．

- 手による影
- 物体による影
- 投影された黒丸エージェント
- プロジェクタ輝度の変化
- カメラ自動露出による暗部

その結果，黒丸自身が `shadow_mask` に含まれ，Unity側では自分自身の描画を避けるような挙動になる．

---

## 8. 切り分け方法

### 8.1 Pythonデバッグ表示で見るべきもの

`python main.py` 実行中に `d` を押し，デバッグ表示を出す．  
見る対象は以下である．

1. 影マスク
2. 投影面の範囲
3. Unityから受け取ったagent円
4. Covered判定に使っている領域
5. agent領域除外後の影マスク

### 8.2 期待される正常状態

何も置いていない状態：

```text
影マスク：ほぼ空
agent円：Unityの黒丸位置と一致
Covered：false
AvoidShadow：必要以上に発火しない
```

手を入れた状態：

```text
影マスク：手の影部分だけ出る
agent円に手影が重なる：Covered
agent円から手影を離す：Covered解除
```

黒丸自己検出が起きている状態：

```text
影マスク：黒丸の位置に常に出る
agent円と影マスクが常に重なる
ShapeAgent：CoveredまたはAvoidShadowに寄る
```

---

## 9. 推奨修正

### 9.1 背景学習中の表示制御

背景学習中は，黒丸を背景に焼き込まないために見た目を消す必要がある．  
ただし，`CircleAgent` GameObject本体を `SetActive(false)` にすると，OSC送信やログも止まりうる．

推奨は，Rendererだけを無効化する方式である．

```csharp
foreach (var go in hideDuringLearn)
{
    if (go == null) continue;

    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
    {
        r.enabled = !learningVisible;
    }
}
```

設計上は以下のように分ける．

| 対象 | 方針 |
|---|---|
| GameObject本体 | activeのまま |
| `ShapeAgent` | enabledのまま |
| `AgentStateBroadcaster` | enabledのまま |
| `SpriteRenderer` / `MeshRenderer` | 学習中だけ非表示 |

### 9.2 通常時の自己マスク除外

通常時は黒丸が表示されるため，背景学習中に隠すだけでは不十分である．  
Python側の影検出後，判定前にagent領域を影マスクから除外する必要がある．

概念実装：

```python
# shadow_mask: 影検出後の二値マスク
# agents: Unityから受信したagent state一覧
# agent.x, agent.y: surface正規化座標
# agent.r: surface正規化半径

for agent in agents:
    cx, cy = surface_to_image(agent.x, agent.y)
    rr = surface_radius_to_image_radius(agent.r)

    # 投影された黒丸自身を影として扱わないため，少し大きめに消す
    cv2.circle(
        shadow_mask,
        (int(cx), int(cy)),
        int(rr * 1.5),
        0,
        -1
    )
```

### 9.3 マージン

黒丸の輪郭，カメラぼけ，プロジェクタのにじみ，座標変換誤差を考えると，半径は実際のagent半径より大きめに消すべきである．

初期値：

```text
mask除外半径 = agent半径 × 1.5
```

調整範囲：

```text
1.3〜2.0
```

大きすぎると，黒丸近傍の本当の手影まで消してしまう．  
小さすぎると，黒丸の縁が影として残る．

---

## 10. 確認テスト手順

### 10.1 最小テスト

```text
1. UnityをPlayする
2. python main.py を実行する
3. Python側で d を押してデバッグ表示を出す
4. b を押して背景再学習する
5. 投影面から手・物・影をどけて8秒待つ
6. 何もない状態で影マスクを見る
7. 黒丸の位置に影マスクが出ているか見る
8. 手を入れて，手の部分だけ影マスクが出るか見る
9. 手の影を黒丸に重ねる
10. Unity Consoleで state=Covered になるか見る
11. 手を離して state が戻るか見る
```

### 10.2 修正後に期待する挙動

```text
黒丸だけが表示されている：
  shadow_maskには黒丸由来の影が残らない

手の影が黒丸から離れている：
  AvoidShadowまたは通常移動
  Coveredではない

手の影が黒丸に重なる：
  Coveredになる

手を離す：
  Covered解除
```

---

## 11. 確認すべき設定

### 11.1 Unity Editorのバックグラウンド動作

Pythonウィンドウにフォーカスしている間もUnityを動かすには，以下を確認する．

```text
Edit
→ Project Settings
→ Player
→ Resolution and Presentation
→ Run In Background
```

必要ならコードでも明示する．

```csharp
void Awake()
{
    Application.runInBackground = true;
}
```

これは `coverage=0%` のノイズを減らすための設定であり，本丸の自己影検出とは別問題である．

### 11.2 キャリブレーション

`calibrate.py` では，以下の順で4隅をクリックする．

```text
0: TL (top-left)
1: TR (top-right)
2: BR (bottom-right)
3: BL (bottom-left)
```

ログ例：

```text
PS C:\Users\demo\Desktop\ShadowHabitat\python> python calibrate.py
[calibration] Click the 4 corners of the projected surface in order:
  0: TL (top-left)
  1: TR (top-right)
  2: BR (bottom-right)
  3: BL (bottom-left)
[calibration] Keys: u=undo  r=reset  s=save  q/esc=quit
[calibration] Saved to calibration.json
```

---

## 12. 現時点の診断分類

| 仮説 | 判定 | 根拠 |
|---|---|---|
| Unity側に`SystemController`がない | 否定 | `Awake`ログあり |
| `uOscServer`未設定 | 否定 | `uOscServer=OK` |
| `hideDuringLearn`未設定 | 否定 | `hideDuringLearn.Count=1` |
| `CircleAgent`参照切れ | 否定 | `hideDuringLearn[0] = CircleAgent active=True` |
| Unity → Python OSC不通 | 否定 | `FIRST /agent/state`あり，`coverage=100%`区間あり |
| Python → Unity OSC不通 | 否定 | `/system/learn_start`，`/system/learn_end`受信あり |
| Unityフォーカス外で更新停止 | 可能性高 | Pythonウィンドウにフォーカスしていた説明と整合 |
| 背景学習の不備 | 可能性あり | `b`後の挙動に影響しうる |
| 黒丸自己影検出 | 最有力 | ユーザー観察，`Covered`頻発，黒丸が暗領域であることと整合 |

---

## 13. 次に行うべき作業

優先順位は以下である．

1. Python側の影マスクから，agent円領域を除外する．
2. デバッグ表示に，agent円と除外円を描画する．
3. 背景学習中はGameObject本体ではなくRendererだけを非表示にする．
4. Unityの `Run In Background` を有効化する．
5. `d`デバッグ表示で，黒丸自己影が消え，手影だけが残ることを確認する．

---

## 14. 実装メモ

### 14.1 Python側に入れるべき処理の位置

影マスク生成後，Covered判定やUnityへの影情報送信の前に入れる．

```text
camera frame
→ surface warp / calibration
→ background subtraction
→ shadow_mask生成
→ agent領域をshadow_maskから除外
→ ノイズ除去
→ Covered / AvoidShadow判定
→ Unityへ送信
```

### 14.2 注意点

agent除外を早すぎる段階で行うと，画像空間とsurface空間の対応が崩れる可能性がある．  
そのため，現在の判定がどの座標系で行われているかに合わせて除外する．

- 判定がcamera image座標なら，agent座標をcamera image座標へ変換して消す．
- 判定がsurface正規化座標なら，surface上のmaskで消す．
- 判定がprojected surface pixel座標なら，その座標系で消す．

---

## 15. まとめ

通信系は成立している．  
`coverage=0%` はUnityフォーカス外更新停止の影響として説明できる．  
本丸は，投影された黒丸自身がカメラ画像上で暗領域として影検出されている点である．

恒久対策は，背景学習時にRendererを隠し，通常時にはPython側でagent円領域を影マスクから除外することである．
