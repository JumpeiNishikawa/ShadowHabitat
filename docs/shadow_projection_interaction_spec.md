# 影インタラクション型プロジェクションシステム 仕様書（構想版）

## 1. 目的

本システムは，プロジェクタ投影面上に表示された図形またはキャラクタに対して，ユーザーが手・腕・物体によって作る影を用いて干渉できるインタラクティブシステムである．

初期段階では，平面上に表示された円が影から距離を取る単純な反応を実装する．その後，Heider-Simmel Experimentを参考にした図形間インタラクション，角・天井角への多面投影，最終的には猫モデルへの拡張を行う．

本構想における新規性は，影入力そのものの技術的発明ではなく，影を介した身体的・環境的干渉を，投影キャラクタの行動生成に利用する点に置く．

---

## 2. システム概要

### 2.1 基本構成

```text
ユーザーの手・腕・物体
        ↓
プロジェクタ光を遮る
        ↓
投影面上に影が生じる
        ↓
カメラで投影面を撮影
        ↓
Python/OpenCVで影を検出・補正
        ↓
Unityへ影情報を送信
        ↓
Unity内に不可視の影領域を配置
        ↓
図形またはキャラクタが反応
        ↓
必要に応じてTouchDesignerで多面投影補正
```

### 2.2 最終的な役割分担

```text
Python/OpenCV
- カメラ取得
- 投影面キャリブレーション
- 影検出
- 影特徴量抽出
- イベント判定補助
- ログ保存

Unity
- 図形・キャラクタ描画
- Heider-Simmel風の運動制御
- 影領域との干渉判定
- 状態遷移
- 猫モデルへの拡張

TouchDesigner
- 壁・角・天井角への投影マッピング
- 3面投影の現場調整
- Unity出力映像の最終配置
```

---

## 3. 対象インタラクション

### 3.1 基本インタラクション

| 種類 | 入力条件 | システム内表現 | 反応例 |
|---|---|---|---|
| 近づく | 影が図形付近に入る | `ShadowNear` | 図形が警戒する，見る |
| 遮る | 進行方向前方に影がある | `ShadowBlocking` | 停止，回り込み，後退 |
| 覆う | 図形本体と影の重なり率が高い | `ShadowCovering` | 縮む，震える，停止 |
| 押す | 影が接触しながら移動する | `ShadowPushing` | 影の移動方向へ押される |
| つまむ | 2つの影成分が図形を挟む | `ShadowPinching` | 捕まる，抵抗する，逃げる |
| 分断する | 図形間の直線上に影が入る | `ShadowSeparating` | 追跡失敗，見失い |
| 足場化 | 広く安定した影が邪魔しない位置にある | `WalkableShadowZone` | 猫が踏む，移動先にする |

### 3.2 初期段階で優先するインタラクション

初期実装では，以下に限定する．

1. 影から距離を取る
2. 影で進路を遮る
3. 影で覆う
4. 2影成分でつまむ

猫モデル段階では，以下を追加する．

1. なでる
2. 足場を作る
3. 影を障害物として避ける
4. 影を安全領域・誘導先として使う

---

## 4. 段階的開発計画

## Stage 1：平面・円1体・影回避

### 4.1 目的

影検出，座標補正，Unity側の影領域表現，円の基本反応を検証する．

### 4.2 構成

```text
平面投影
円1体
影入力1つ
Python/OpenCV + Unity
```

### 4.3 実装内容

#### Python/OpenCV側

- カメラ画像取得
- 投影面の4点キャリブレーション
- ホモグラフィ変換
- 影マスク生成
- 影重心・面積・外接矩形の算出
- Unityへの送信

#### Unity側

- 円オブジェクトの表示
- 影情報の受信
- 不可視の`ShadowCollider`生成
- 円が影から一定距離を取る行動
- CSVログまたはPython側ログとの同期

### 4.4 影表現

初期は1影を1つの円Colliderとして近似する．

```text
shadow_x
shadow_y
shadow_area
shadow_radius = sqrt(area / pi)
```

Unity内では以下のように扱う．

```text
ShadowBlob
- position
- radius
- area
- velocity
```

### 4.5 成功条件

- 手や物体の影を投影座標上で取得できる．
- 円が影から安定して距離を取る．
- 影の大きさが変わっても反応が破綻しない．
- 影検出結果と円の状態がログに残る．

---

## Stage 2：平面・円＋三角・Heider-Simmel風運動

### 4.6 目的

単純図形の運動により，追跡・逃避・ためらい・遮断などが意図的に見えるかを検証する．

### 4.7 構成

```text
平面投影
円1体
三角1体
影干渉あり
Python/OpenCV + Unity
```

### 4.8 図形の役割

| 図形 | 初期役割 |
|---|---|
| 円 | 逃避者，弱い対象，猫版への前段階 |
| 三角 | 追跡者，接近者，障害としての対象 |

### 4.9 通常行動

- 三角が円を追う．
- 円は三角から逃げる．
- 両者は壁や境界を避ける．
- 円は影からも距離を取る．
- 三角は影で遮られると追跡を中断する．

### 4.10 状態例

```text
CircleState
- Wander
- EscapeTriangle
- AvoidShadow
- Blocked
- Covered
- Grabbed

TriangleState
- Wander
- ChaseCircle
- BlockedByShadow
- Search
- Hesitate
```

### 4.11 影イベント

```text
ShadowNear
ShadowBlocking
ShadowCovering
ShadowPinching
ShadowSeparating
ShadowRelease
```

### 4.12 成功条件

- 観察者が「追っている」「逃げている」「邪魔された」と解釈できる．
- 影が図形間の関係に介入しているように見える．
- 影操作が図形運動を壊さず，むしろ意味づけを強める．

---

## Stage 2.5：状態・イベント・ログ形式の固定

### 4.13 目的

平面段階で得た仕様を，角3面・猫モデルへ移植できるように抽象化する．

### 4.14 固定する仕様

- 影特徴量の形式
- Unityへの通信形式
- 図形／エージェント状態
- 影イベント分類
- ログ形式
- surface座標系

### 4.15 推奨通信形式

座標はsurface内の0〜1正規化座標で送る．

```json
{
  "surface": "plane",
  "components": [
    {
      "id": 0,
      "x": 0.42,
      "y": 0.61,
      "area": 0.08,
      "major": 0.20,
      "minor": 0.06,
      "angle": 32.0,
      "vx": 0.01,
      "vy": -0.02
    }
  ]
}
```

### 4.16 ログ形式

```csv
time,surface,agent_id,agent_x,agent_y,agent_state,shadow_area,shadow_x,shadow_y,overlap,event
```

---

## Stage 3：角・天井角3面・円1体

### 4.17 目的

平面から角・天井角へ拡張し，複数surface上での表示，キャリブレーション，surface間遷移を検証する．

### 4.18 構成

```text
天井角3面
- 壁A
- 壁B
- 天井

円1体
Python/OpenCV + Unity + TouchDesigner
```

### 4.19 surface定義

```text
SurfaceId
- WallA
- WallB
- Ceiling
```

各surfaceは独立した2D座標系を持つ．

```text
surface-local x: 0.0〜1.0
surface-local y: 0.0〜1.0
```

### 4.20 表示・投影

- Unityは3面分の映像を生成する．
- TouchDesignerで壁A・壁B・天井へマッピングする．
- Kantan Mapper等で現場調整する．

### 4.21 行動

- 円が1つのsurface上を移動する．
- 影が近づくと角方向へ逃げる．
- 角に到達すると別surfaceへ遷移する．
- 天井面は逃げ場または特殊領域として扱う．

### 4.22 成功条件

- 円が角をまたいで移動しているように見える．
- 各面で影干渉が成立する．
- surface切替時に表示・座標・ログが破綻しない．

---

## Stage 4：統合版

### 4.23 目的

Stage 2のHeider-Simmel風運動と，Stage 3の角3面投影を統合する．

### 4.24 構成

```text
天井角3面
円＋三角
影干渉あり
Python/OpenCV + Unity + TouchDesigner
```

### 4.25 統合シナリオ例

```text
三角が円を追う．
円は壁Aから角へ逃げる．
影が円の逃げ道を遮る．
円は天井へ逃げる．
三角が遅れて壁Bへ回り込む．
巨大な影が三角を覆うと，三角は停止する．
```

### 4.26 成功条件

- 図形間関係，影干渉，surface遷移が同時に読める．
- 操作として「遮る」「覆う」「つまむ」が成立する．
- 投影位置の歪み調整が運用可能である．
- ログに基づいて後から状態遷移を再構成できる．

---

## Stage 5：猫モデル版

### 4.27 目的

図形段階で確立した影干渉を，猫モデルに対する身体的・環境的インタラクションへ拡張する．

### 4.28 追加するインタラクション

| インタラクション | 判定 | 猫の反応 |
|---|---|---|
| なでる | 背中・頭部Collider上を低速で影が移動 | 目を細める，止まる，近づく |
| 覆う | 大きな影が身体を覆う | しゃがむ，隠れる，逃げる |
| 足場を作る | 広く安定した影が経路を邪魔しない | その領域を踏む，移動先にする |
| 通せんぼ | 細長い影が経路を遮る | 回り込む，止まる |
| 誘導する | 影が一定距離で移動する | 距離を取りながら追従する |

### 4.29 猫モデルのCollider

```text
Cat
- HeadCollider
- BackCollider
- BodyCollider
- TailCollider
- PawCollider
```

### 4.30 なでる判定

```text
ShadowCapsuleがHeadまたはBackに接触
かつ
低〜中速で移動
かつ
接触が一定時間継続
→ CatStroked
```

### 4.31 足場判定

影を物理床として厳密に扱わず，猫AIが踏んでもよい一時領域として扱う．

```text
影面積が一定以上
かつ
一定時間安定
かつ
猫の移動経路を塞がない
かつ
猫の近傍または進行方向付近にある
→ WalkableShadowZone
```

### 4.32 足場の内部表現

```text
WalkableShadowZone
- center
- width
- height
- stability
- lifetime
- confidence
```

猫はこの領域へ向かい，到達時に足踏み・停止・ジャンプ等のアニメーションを再生する．物理的な接地は必須ではない．

---

## 5. 技術設計

## 5.1 Python/OpenCV側

### 5.1.1 主な処理

```text
1. カメラ画像取得
2. 投影面キャリブレーション
3. ホモグラフィ変換
4. 影マスク生成
5. 影連結成分抽出
6. 特徴量算出
7. surface座標へ正規化
8. Unityへ送信
9. ログ保存
```

### 5.1.2 影特徴量

```text
component_id
surface_id
centroid_x
centroid_y
area
major_axis
minor_axis
angle
velocity_x
velocity_y
stability_time
```

### 5.1.3 影検出の初期方法

明るい背景を前提に，輝度差分で検出する．

```text
expected_brightness - observed_brightness > threshold
```

将来的には，表示画像を既知情報として使い，現在のカメラ画像との差分から影を推定する．

---

## 5.2 Unity側

### 5.2.1 主な処理

```text
1. Pythonから影情報を受信
2. ShadowPrimitiveを更新
3. 不可視Colliderを配置
4. AgentとShadowの関係を判定
5. Agent状態を更新
6. 描画・アニメーションを更新
7. 必要な状態ログを出力
```

### 5.2.2 クラス案

```csharp
public class ShadowInputReceiver { }
public class ShadowPrimitive { }
public class ShadowColliderManager { }
public class ShapeAgent { }
public class HeiderSimmelController { }
public class SurfaceManager { }
public class CatAgent { }
public class ShadowInteractionClassifier { }
```

### 5.2.3 状態定義

```csharp
public enum AgentState
{
    Idle,
    Wander,
    Chase,
    Escape,
    AvoidShadow,
    Blocked,
    Covered,
    Grabbed,
    Searching,
    SurfaceTransition,
    WalkOnShadow,
    BeingStroked
}
```

### 5.2.4 影イベント定義

```csharp
public enum ShadowInteractionType
{
    None,
    Near,
    Blocking,
    Covering,
    Pushing,
    Pinching,
    Separating,
    Walkable,
    Stroke
}
```

---

## 5.3 TouchDesigner側

### 5.3.1 導入タイミング

TouchDesignerはStage 3以降で導入する．Stage 1〜2ではUnity単体出力でよい．

### 5.3.2 主な処理

```text
1. Unity出力を受け取る
2. 壁A・壁B・天井用に映像を分割または配置
3. Kantan Mapper等で3面に合わせる
4. プロジェクタへ出力
```

### 5.3.3 surface管理

```text
surface_A: wall_A
surface_B: wall_B
surface_C: ceiling
```

角を1枚の平面として扱わず，3面を個別surfaceとして管理する．

---

## 6. 作業手順

## Phase 0：準備

### 作業

1. 使用するプロジェクタ，カメラ，PCを確認する．
2. Unityプロジェクトを作成する．
3. Python/OpenCV環境を作成する．
4. PythonからUnityへUDPまたはOSCで値を送る最小テストを行う．
5. Unity側で受信値に応じて円が動くテストを行う．

### 成果物

- Unity最小プロジェクト
- Python送信スクリプト
- 通信確認用シーン

---

## Phase 1：影検出MVP

### 作業

1. カメラ画像をOpenCVで取得する．
2. 投影面を明るい背景にする．
3. 影を輝度差分で抽出する．
4. 最大連結成分の重心・面積を計算する．
5. Unityへ`x, y, area`を送る．
6. Unity内に不可視円Colliderを配置する．
7. デバッグ時は半透明の円を表示する．

### 成果物

- 影検出スクリプト
- Unity内ShadowBlob表示
- 影ログCSV

---

## Phase 2：平面・円1体の影回避

### 作業

1. Unityに円Agentを配置する．
2. ShadowBlobとの距離を計算する．
3. 一定距離以下なら反対方向へ移動する．
4. 影が重なる場合は震え・停止などの反応を入れる．
5. 反応パラメータを調整する．

### 成果物

- Stage 1デモ
- 円の状態ログ
- 影距離・反応イベントログ

---

## Phase 3：平面・円＋三角

### 作業

1. 三角Agentを追加する．
2. 三角が円を追う行動を実装する．
3. 円が三角から逃げる行動を実装する．
4. 影で三角・円の進路を遮れるようにする．
5. 覆う，分断する，つまむの簡易判定を追加する．
6. Heider-Simmel風に見えるよう速度・停止・ためらいを調整する．

### 成果物

- Stage 2デモ
- 図形間インタラクションログ
- 影イベント分類ログ

---

## Phase 4：仕様固定

### 作業

1. 影イベント一覧を固定する．
2. Agent状態一覧を固定する．
3. surface座標系を導入する．
4. 通信形式をJSONまたはOSC形式で固定する．
5. ログ形式を固定する．
6. Unity内ロジックをsurface非依存に整理する．

### 成果物

- 通信仕様
- ログ仕様
- 状態遷移仕様
- surface管理クラス

---

## Phase 5：角3面投影

### 作業

1. TouchDesignerプロジェクトを作成する．
2. Unity出力をTouchDesignerへ入力する．
3. Kantan Mapperで壁A・壁B・天井へマッピングする．
4. Unity側でsurfaceをWallA，WallB，Ceilingに分ける．
5. 円がsurface間を移動する処理を作る．
6. カメラ側も各面ごとにホモグラフィを持つ．
7. 各surface上で影検出・影イベントを発火する．

### 成果物

- Stage 3デモ
- 3面投影設定
- surface遷移ログ

---

## Phase 6：統合版

### 作業

1. Stage 2の円＋三角ロジックを3面対応にする．
2. 影イベントをsurfaceごとに処理する．
3. 円と三角が別surfaceへ移動できるようにする．
4. 角を使った逃避・追跡シナリオを作る．
5. 影による遮断・覆い・つまみを3面で確認する．

### 成果物

- Stage 4統合デモ
- 角3面Heider-Simmelデモ
- ログ一式

---

## Phase 7：猫モデル版

### 作業

1. 研究室Unity資産から猫モデルを導入する．
2. 猫にHead，Back，Body，Tail等のColliderを追加する．
3. 影カプセルが背中・頭に沿って動いたとき，なでるイベントを発火する．
4. 広く安定した影をWalkableShadowZoneとして扱う．
5. 猫がその領域を踏む，歩く，止まる等の行動を実装する．
6. 通せんぼ，覆う，誘導する反応を追加する．

### 成果物

- 猫モデル影インタラクションデモ
- なでる判定
- 足場判定
- 猫状態ログ

---

## 7. 優先実装リスト

### 必須

1. Python/OpenCVによる影検出
2. Python→Unity通信
3. Unity内ShadowCollider生成
4. 円Agentの影回避
5. 円＋三角の追跡・逃避
6. 影による遮断・覆い
7. ログ保存

### 次点

1. つまみ判定
2. surface管理
3. TouchDesignerによる3面投影
4. 角でのsurface遷移
5. 猫モデル導入

### 後回し

1. 手の形状認識
2. 手モデル表示
3. PolygonColliderによる影輪郭再現
4. 高精度な接触判定
5. 厳密な3D再構成

---

## 8. 実装上の割り切り

- 影は手として認識しない．まずは作用領域として扱う．
- Unity内では影を不可視の円・楕円・カプセルColliderとして近似する．
- 接触は実接触ではなく，投影座標上の重なりとして扱う．
- 猫の足場は物理的床ではなく，移動AIが踏んでもよい領域として扱う．
- 天井角3面は初期から狙わず，平面でロジックを固めてから移植する．
- TouchDesignerは最初から使わず，3面投影が必要になった段階で導入する．

---

## 9. 最小MVP

最初に完成させるべきMVPは以下である．

```text
平面に円を投影する．
手や物体の影をカメラで検出する．
影の重心と面積をUnityへ送る．
Unity内に不可視の円Colliderを置く．
投影された円は，影から一定距離を保つように逃げる．
影が円を覆うと，円は震えるまたは停止する．
```

このMVPが成立した後，円＋三角，角3面，猫モデルへ拡張する．

