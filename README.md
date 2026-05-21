# ShadowHabitat

影インタラクション型プロジェクションシステム — Stage 1 MVP 実装。

## 全体像

```
[カメラ] --影画像--> [Python/OpenCV] --OSC(/shadow/blob)--> [Unity 6 + uOSC] --投影--> [プロジェクタ]
```

## ディレクトリ

```
docs/                  仕様書
python/                影検出 + OSC送信（私が動かす）
  ├ requirements.txt
  ├ config.json        ポート / カメラ / 検出パラメータ
  ├ calibrate.py       4点キャリブレーション（初回のみ）
  ├ main.py            メイン: 影検出 → OSC → Unity
  └ src/               モジュール群
unity_scripts/         Unityにコピーして使う C# スクリプト（あなたがEditorで配置）
  ├ README_Unity.md    ★ Unity セットアップ手順はこれを見る
  └ *.cs               6本のスクリプト
```

## クイックスタート

### Python 側（私の領分・既に完成）

```powershell
cd C:\Users\jumpe\dev\ShadowHabitat\python
py -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt

# 初回 or 設置変更時:
python calibrate.py            # 4隅を順に TL → TR → BR → BL クリック → s で保存

# 通常起動:
python main.py
```

### Unity 側（あなたが Editor 操作）

→ **`unity_scripts/README_Unity.md` の手順通り**

要点だけ:
1. Unity 6 LTS で新規 2D プロジェクト
2. Package Manager → `https://github.com/hecomi/uOSC.git#upm` を追加
3. `unity_scripts/*.cs` を `Assets/Scripts/ShadowHabitat/` にコピー
4. シーンに Surface / OscReceiver(+uOscServer port 9000) / ShadowManager / CircleAgent を配置
5. Python を起動してから Unity Play

## 通信仕様（このリポジトリの正準）

OSC over UDP. 双方向。

| 方向 | Port | Address | Args | 意味 |
|---|---|---|---|---|
| Py → Unity | 9000 | `/shadow/begin` | `surface:string` `count:int` `frame:int` | フレーム開始 |
| Py → Unity | 9000 | `/shadow/blob`  | `id:int` `x:float` `y:float` `area:float` `major:float` `minor:float` `angle:float` `vx:float` `vy:float` | 影1個分。x/y/area は surface 内 0..1 正規化 |
| Py → Unity | 9000 | `/shadow/end`   | `frame:int` | フレーム確定 |
| Py → Unity | 9000 | `/system/learn_start` | `duration:float` | 背景学習開始。Unityは円を非表示・白塗り |
| Py → Unity | 9000 | `/system/learn_end`   | — | 学習終了。Unityは通常描画に戻す |
| Unity → Py | 9001 | `/agent/state` | `id:int` `surface:string` `x:float` `y:float` `radius:float` | 円位置の返送。Pythonは影マスクから除外 |

- 座標系: 画像原点 = 左上、x→右、y→下。Unity `Surface.cs` が y を反転してワールドへ変換。
- area は surface 面積に対する比、major/minor/radius は max(W,H) で正規化。

### 自己検出防止
Unity は毎フレーム `/agent/state` で各エージェントの位置・半径を Python に返送する。Python は影マスクからそのディスク領域を除外（`agent_mask_padding_px` で余白拡張）。これにより円自身が「影」として検出されない。

### 背景学習ハンドシェイク
Python起動時 / `b`キー押下時に `/system/learn_start` を発行 → Unity が円を非表示・白塗り → Python が per-pixel running max でクリーン背景学習 → freeze後に `/system/learn_end` を発行 → Unity 復帰。

## ログ

- Python: `python/logs/shadow_YYYYMMDD_HHMMSS.csv`
- Unity: `%APPDATA%/../LocalLow/<Company>/<Product>/logs/agent_*.csv`
  - Editor 上では `Application.persistentDataPath` をログ出力で確認可。

## Stage 進行計画

- ✅ Stage 1: 平面・円1体・影回避 ← **いまここ（このコード）**
- ⏭ Stage 2: 円＋三角の Heider-Simmel 風運動 — `ShapeAgent.cs` を継承して TriangleAgent を追加すれば差分で書ける
- ⏭ Stage 2.5: 仕様固定（このREADME / OSCプロトコルが既に該当）
- ⏭ Stage 3: 角3面 — `Surface` を複数置いて受信側で surface_id 別にルーティング
- ⏭ Stage 4: 統合
- ⏭ Stage 5: 猫モデル
