# ShadowHabitat 作業ログ

## 2026-05-22：Stage 1 MVP の自己検出問題を解決

### この日に確定した実装

#### 双方向 OSC
- Python → Unity: port 9000、`/shadow/*` と `/system/learn_*`
- Unity → Python: port 9001、`/agent/state <id> <surface> <x> <y> <radius> <vx> <vy>`
- どちらも `python-osc` / `uOSC (hecomi)` で動作確認済み

#### 背景学習
- 方式：**ピクセルごとの running max**（EMA は廃止）
- 起動時／`b` キーで `/system/learn_start <duration>` ハンドシェイク
- Unity 側 `SystemController` は **Renderer.enabled でのみ隠す**（GameObject は active のまま → ShapeAgent と Broadcaster は動き続ける）
- 学習中も `/agent/state` は送られ続けるので状態追跡が途切れない
- 学習完了で `/system/learn_end` を返送 → Unity が Renderer 復帰

#### 自己マスク
- Unity 側で `ShapeAgent.visualRadius` を Renderer bounds から **自動算出**（`autoVisualRadiusFromRenderer = true` がデフォルト）
- 値は `Mathf.Max(bodyRadius, visualRadius)` で送信
- Python 側は受信した `(x, y, r, vx, vy)` から **過去位置→現在→未来位置を結ぶカプセル形**で mask=0 を塗る
- パラメータ：`motion_lookback_sec: 0.12`、`motion_lookahead_sec: 0.06`

#### その他
- `edge_inset_pixels`（warp 外周を一律無視）でキャリブレーション枠外の漏れを潰す
- `Application.runInBackground = true` を `SystemController.Awake` で設定
- 診断ログ：`AgentStateBroadcaster` の HB、`SystemController` の Awake / OnDataReceived / SetLearningVisible
- `ShadowColliderManager.debugVisible` は **手動でOFFが運用ルール**（投影フィードバックを避けるため）。デフォルト ON のままだが、現場で必ず切る

### 動作確認の結果
- 黒丸が自分から逃げ続けるループは **解消**
- `coverage=0%` 問題は `runInBackground` で解決
- Wander の動きが見える状態で観察可能

### 残課題（次の作業対象）

#### 課題：細長い影の表現が雑
**症状**：腕全体・棒など、細長い影が映ったとき、Python は連結成分の重心1点＋楕円フィット情報を送る。Unity 側 `ShadowColliderManager` は **CircleCollider2D 1個**しか作らないため、影本体は細長いのに干渉判定は重心の小さな円になる。

**インタラクションへの影響**：
- 「触れる」「つまむ」「なでる」（Stage 5 猫モデル）が成立しづらい
- 腕で円を押そうとしても重心しか効かない
- Heider-Simmel 文脈の「遮る」「分断する」も腕の長さ全体で判定したい

**改善方針**（候補、優先順位）：
1. **CapsuleCollider2D / 楕円コライダー化**
   - Python は既に major / minor / angle を送っている（`/shadow/blob`）
   - `ShadowColliderManager` を CircleCollider2D → CapsuleCollider2D に置き換え、major / minor / angle を反映
   - 最小コードで効果大
2. **影の骨格線（skeleton）を送る**
   - Python 側で `cv2.ximgproc.thinning` または距離変換から骨格抽出
   - 折れ線の端点列を `/shadow/skeleton` で送信
   - Unity 側で EdgeCollider2D のチェイン化
   - 表現力高いが影輪郭の安定性が必要
3. **影輪郭ポリゴン送信**
   - `cv2.findContours` の近似ポリゴンを PolygonCollider2D に
   - 「最終的にやりたいこと」だが OSC 帯域と座標数の上限を考慮する必要あり

**次セッションのファーストステップ**：方針1（CapsuleCollider2D 化）を実装して効果確認。それで足りなければ 2 or 3 へ。

### 次回着手前に確認したいこと
- 今の `ShadowColliderManager.CreateColliderObject` を Capsule に書き換えるだけでよいか、それとも当面 Circle と並存させたいか
- 楕円の major / minor が安定するか（Python 側で minor が極端に小さくなる時の挙動）
- Heider-Simmel 段階で Triangle Agent を追加するタイミング（自己検出は visualRadius + capsule mask の仕組みでそのまま流用可能）

---

### 今日の設計決定が将来段階に与える影響（先回り洗い出し）

将来：Stage 2（三角追加）、Stage 3（角3面 + TouchDesigner）、Stage 4（統合）、Stage 5（猫モデル + つまむ／なでる）。今日の実装で**そのまま使えるもの**と**改修が要るもの**を分けて記録。

#### A. そのまま使える（変更不要）

| 仕組み | 理由 |
|---|---|
| OSC プロトコル `/shadow/begin` `/blob` `/end` `/agent/state` `/system/learn_*` | surface_id がすでに引数に入っており、Stage 3 の多面化でルーティング可能。座標は正規化済みで surface 差し替えが安い |
| running max 背景学習 + `/system/learn` ハンドシェイク | 猫が止まっていても `hideDuringLearn` に入れれば学習中だけ Renderer を切れる。多面化しても全 surface 同時学習で問題なし |
| Renderer-only hide（GameObject は active のまま） | 学習中も ShapeAgent / Broadcaster / Logger が動き続ける → 多エージェント・多 surface でも追跡が途切れない |
| capsule self-mask (vx, vy) | エージェント数が増えても線形に重ねて適用するだけ。猫の Head/Back/Body 各々を別 ID で送れば各々マスクされる |
| `Application.runInBackground = true` | プロセス分離が増えても引きずらない |
| edge_inset_pixels | 角3面では各 surface に対して個別に warp するので、各々で inset を持てる |

#### B. 軽い改修が要る（小〜中規模、設計済み）

| 障害 | 顕在化 | 方針 |
|---|---|---|
| **ShadowColliderManager が CircleCollider2D 固定** | Stage 1残課題、Stage 2 つまむ、Stage 5 なでる | CapsuleCollider2D 化（既に送ってる major/minor/angle を使うだけ）。次セッションのファーストステップ |
| **AgentCsvLogger.surfaceId が手書き定数** | Stage 3 surface transition | ShapeAgent から `CurrentSurface` を pull するよう変更 |
| **つまむ判定ロジック未実装** | Stage 2 | `ShadowInteractionClassifier` 新設。ActiveTags を見て、エージェントを中心に角度差 ~180° で挟む2 blob を検出 → `Pinching` イベント |
| **visualRadius auto-detect が GameObject の全 Renderer を統合** | Stage 5 猫（複数 Renderer） | 猫モデルでは `autoVisualRadiusFromRenderer = OFF`、Head / Back / Body 個別に IShadowAgent として visualRadius を手動設定 |

#### C. 構造的な書き直しが要る（中〜大規模）

| 障害 | 顕在化 | 方針 |
|---|---|---|
| **`AgentStateBroadcaster.agents` が `List<ShapeAgent>` 固定** | Stage 2 三角、Stage 5 猫（ShapeAgentを継承しない） | `IShadowAgent` インタフェース（`Position`, `VisualRadius`, `Velocity`, `SurfaceId`）を切り、Broadcaster を `List<IShadowAgent>` に。ShapeAgent は実装するだけ。猫は Head/Back/Body 各々が IShadowAgent を実装 |
| **Surface が単一前提** — `ShadowOscReceiver.expectedSurfaceId` 固定、`ShadowColliderManager.surface` 単一、`ShapeAgent.surface` 単一 | Stage 3 | OscReceiver は全 surface の blob を受け、内部で `Dictionary<surfaceId, ColliderManager>` にルーティング。ShapeAgent は「今いる surface」を内部状態として持ち、角到達で切替 |
| **Python 側 ShadowDetector / Calibration が単一** | Stage 3 | surface ごとに `(Calibration, ShadowDetector)` ペアを持つ。1カメラで全面が写れば multi-homography で並列処理。物理的に分離した surface で multi-camera が必要なら multi-process or threaded |

#### D. 将来確認が必要（今は判断保留）

| 項目 | 内容 |
|---|---|
| **TouchDesigner 連携の出力経路** | Spout / NDI / 単純な複数ディスプレイ出力のどれにするか。Unity → TD は出力プラグインの選定問題で、検出パイプラインには影響なし |
| **TD の y 軸方向** | Unity が y-up、TD は通常 top-left origin。受け渡しで反転が要るかは実機で確認 |
| **影輪郭の精度限界** | Capsule で猫なでが成立するか。不十分なら polygon / skeleton 送信（spec §7 後回しリスト）に進む |
| **複数カメラ時のキャリブレーションフロー** | Stage 3 で 1 cam で全面映らない場合のフロー。calibrate.py を surface_id 引数取れるよう拡張する程度で足りる見込み |

#### E. 短期で得した副次効果

- `Application.runInBackground = true` のおかげで、将来 TouchDesigner や別ウィンドウへフォーカス移しても Unity 側のサーバが死なない
- `IShadowAgent` 化はやらないと Stage 2 三角が始まらないので、次々セッションあたりで投入予定。これがあると Stage 5 猫の体パーツ複数登録も労せず実装できる
- `motion_lookback_sec` のパラメータは、エージェント／影どちらの速度感にも合わせやすい（猫は遅い、人手は速い）

#### F. 結論

**Stage 2 着手前にやるべき優先タスク**：

1. CapsuleCollider2D 化（細長影対応）
2. `IShadowAgent` インタフェース導入（多エージェント・多種エージェント対応）
3. `ShadowInteractionClassifier` 新設（つまむ／覆う／分断などのイベント発火）

これが揃えば Stage 2（円＋三角＋つまむ）に直接入れる。Stage 3 の surface 多面化はその後で構造的書き直しが必要だが、protocol レベルでは今日決めた仕様で問題なく拡張できる。
