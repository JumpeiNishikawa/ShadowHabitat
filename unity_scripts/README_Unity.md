# Unity セットアップ手順（Stage 1 MVP）

対象 Unity バージョン: **Unity 6 (6000.x LTS)** / 2D Built-in RP

このフォルダの `.cs` ファイル6本を Unity プロジェクトの `Assets/Scripts/ShadowHabitat/` 配下にコピーして使う。

ファイル一覧:
- `ShadowBlob.cs` — Python から受け取る影1個分のデータ構造
- `Surface.cs` — 正規化座標(0..1) ↔ Unityワールド座標 変換
- `ShadowOscReceiver.cs` — uOSC で `/shadow/begin` `/shadow/blob` `/shadow/end` を受信
- `ShadowColliderTag.cs` — 影オブジェクトのマーカー
- `ShadowColliderManager.cs` — 影ごとに不可視CircleCollider2Dを生成・更新・破棄
- `ShapeAgent.cs` — 円エージェント（影回避・覆われたら震えて停止）
- `AgentCsvLogger.cs` — 状態CSVログ

---

## 1. プロジェクト作成

1. Unity Hub → New project → **Universal 2D** または **2D (Built-in Render Pipeline)** を選ぶ。
   - 推奨は **2D (Built-in Render Pipeline)**（一番シンプル）。
2. プロジェクト名: `ShadowHabitatUnity` など。場所: `C:\Users\jumpe\dev\ShadowHabitatUnity` を推奨（このリポジトリのすぐ隣）。

## 2. uOSC をインストール

1. Window → **Package Manager** を開く。
2. 左上の `+` → **Add package from git URL**。
3. 次の URL を貼って Add:
   ```
   https://github.com/hecomi/uOSC.git#upm
   ```
4. インポート完了を待つ（コンパイルエラーが出ないこと）。

## 3. スクリプトをコピー

1. Project ウィンドウで `Assets/` を右クリック → Create → Folder → `Scripts` → さらに中に `ShadowHabitat` フォルダを作る。
2. このリポジトリの `unity_scripts/*.cs` 全部を `Assets/Scripts/ShadowHabitat/` にドラッグ＆ドロップ。
3. コンパイルエラーが無いことを確認（Console を見る）。

## 4. シーン構築

シーンに次のオブジェクトを作る:

### a) Main Camera
- 既存の Main Camera を選択。
- **Projection: Orthographic**, **Size: 3.0** くらい（後で見えるよう調整）。
- Background は黒で良い。

### b) Surface (基準座標)
1. Hierarchy で右クリック → Create Empty → 名前を `Surface` に。
2. Inspector で **Add Component → Surface**。
3. Surface コンポーネントの値:
   - Surface Id: `plane`
   - Width World: `10`
   - Height World: `5.625`
4. Transform Position を `(-5, 2.8125, 0)` に。
   - Surface は **左上原点** 扱い。これで surface の中心がカメラ正面に来る。
   - Scene ビューにシアン色の枠が出ているはず。

### c) OSC Receiver / Sender（双方向）
1. Create Empty → `OscReceiver`。
2. Add Component → **uOSC.uOscServer**（uOSC から）。
   - Port: `9000`。Auto Start: ON。
3. Add Component → **Shadow Osc Receiver**。Expected Surface Id: `plane`。
4. Add Component → **uOSC.uOscClient**（uOSC から）— **Python へ送り返す側**。
   - Address: `127.0.0.1`、Port: `9001`（`config.json` の `osc.incoming_port` と合わせる）。
   - Auto Start: ON。
5. Add Component → **System Controller**（学習ハンドシェイクを受ける）。
   - 下の (e) で円を作ったあと、Inspector の `Hide During Learn` リストに `CircleAgent` を**ドラッグ**して追加する。
   - `Learn Flash Surface`：(g) で作る白塗り Quad を後で割り当てる（任意だが推奨）。
6. Add Component → **Agent State Broadcaster**（Pythonへ円の位置を返す）。
   - Surface: `Surface` をドラッグ。
   - Agents: (e) で作る `CircleAgent` の **Shape Agent** コンポーネントをドラッグ追加。
   - Send Interval: `0`（毎フレーム）。

### d) ShadowColliderManager
1. Create Empty → `ShadowManager`。
2. Add Component → **Shadow Collider Manager**。
   - Receiver: `OscReceiver` の `ShadowOscReceiver` をドラッグ。
   - Surface: `Surface` をドラッグ。
   - Debug Visible: ON（影の位置を半透明で可視化、本番はOFFにする）。
   - 他はデフォルト。

### e) 円エージェント
1. GameObject → 2D Object → Sprites → **Circle** で白い円を作る。
   - 名前を `CircleAgent` に。
   - Transform Position を `Surface` の中心付近（例: `(0, 0, 0)`）に。
   - Scale `(0.8, 0.8, 1)` 程度（後で調整）。
2. Add Component → **Shape Agent**。
   - Surface: `Surface`。
   - Shadow Manager: `ShadowManager`。
   - Body Radius: 0.4（円のスケールに合わせる）。
   - Max Speed / Avoid Radius / Avoid Strength は最初はデフォルトでOK。
3. Add Component → **Agent Csv Logger**（任意）。
   - Agent: 自身の Shape Agent をドラッグ。
   - Shadow Manager: `ShadowManager`。

### f) 投影面の背景
- Hierarchy → Create Empty Child of `Surface` → 名前 `ProjectedBackground`。
- Add Component → **Sprite Renderer** → Sprite に Unity の **Knob** か **Square** （白）を使うか、自分で白いPNGを Assets に追加して使う。
- Transform local position `(5, -2.8125, 1)`（Surfaceの中央、円より奥）。
- Transform scale を `Surface` サイズに合わせる（白いSquareなら `(10, 5.625, 1)`）。
- これがプロジェクタで映し出される「明るい背景」。Python側は輝度差分でこの背景の暗化（=影）を検出する。

### g) 学習時用の白塗りフラッシュ（推奨）
- Hierarchy → Create Empty Child of `Surface` → 名前 `LearnFlash`。
- Add Component → **Sprite Renderer**、Sprite = 白Square、Color = pure white、Sorting Order を円より大きい値に（例 10）。
- Transform local position `(5, -2.8125, 0.5)`、scale を Surface 全体に。
- **初期は GameObject を Disable**（Inspectorのチェック外す）にしておく。
- `OscReceiver` の **System Controller** の `Learn Flash Surface` フィールドにこの `LearnFlash` をドラッグ。
- これで学習時：Pythonが `/system/learn_start` を送る → `CircleAgent` が非表示・`LearnFlash` が表示 → カメラは純白の投影面だけを撮る → クリーンな背景を学習できる。

## 5. 動かす

1. **Unity を先に Play** （uOscServer / uOscClient を起動状態にしておく）。
2. 別ターミナルで Python を起動:
   ```powershell
   cd C:\Users\jumpe\dev\ShadowHabitat\python
   python main.py
   ```
3. Python 起動直後に **自動で `/system/learn_start` を送信** → Unity 側の円が消えて白塗りに → Pythonが背景学習 → 学習完了で円が復帰。
   - `surface (warped)` ウィンドウの黄色メッセージが緑になったら学習完了。
4. カメラの前で手を動かす → Unity の円が逃げ、覆うと震える。
5. 失敗したら Python ウィンドウで `b`キー → 再ハンドシェイク。

## 6. 投影本番

1. Unity を **Build & Run**（File → Build Profiles → Windows）して全画面プロジェクタへ出す。
   - Player Settings → Resolution and Presentation → Fullscreen Mode: **Exclusive Fullscreen**。
2. プロジェクタの投影面にカメラを向ける（カメラは投影面全体が映る位置）。
3. Python を起動する前に、もう一度 `python calibrate.py` を実行して投影矩形の4隅をクリックし直す。
4. `python main.py` を再起動。
5. Unity 側 Surface サイズと投影解像度が合っているか確認（Window 表示で位置だけずれている場合は Surface の Transform で調整）。

## 7. パラメータ調整の目安

| 症状 | いじる値 | どこで |
|---|---|---|
| 影が検出されない | `diff_threshold` を下げる（例 25） | `python/config.json` |
| 細かいノイズが影として誤検出 | `min_area_ratio` を上げる（例 0.005）／`morph_kernel` を上げる | 同上 |
| 円の逃げが鈍い | `avoidStrength` ↑ または `avoidRadius` ↑ | `CircleAgent` の ShapeAgent |
| 震えがすぐ発動 | `coverDistance` を下げる | 同上 |
| 影の半透明表示を消したい | `debugVisible` OFF | `ShadowManager` |
| 円が影として誤検出される | `Agent State Broadcaster` が動いているか、`agents` リストに円が入っているか確認。Pythonウィンドウに青枠 `A#0` が描かれていればOK | OscReceiver |
| 学習中に円が消えない | `System Controller` の `Hide During Learn` に円がドラッグされているか確認 | OscReceiver |
| 円除外の余白を増やしたい | `agent_mask_padding_px` を 6 → 12 等に | `python/config.json` |

---

困ったら Console / OSCポート競合 / カメラ占有を最初に疑う。
