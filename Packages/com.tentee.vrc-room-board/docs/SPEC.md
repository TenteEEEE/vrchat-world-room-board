# Room Board 仕様書

## 1. 目的

Room Board は、ワールド内の決めたエリア（部屋）に今誰がいるかを掲示板に表示する VRChat ワールド用ギミックです。部屋ごとの在室者を、ロビーの総合掲示板やドア横の表札など複数の場所に同時に表示できます。

在室状況は**同期しません**。VRChat が各クライアントに配信しているプレイヤー位置を、各クライアントが自分で定期的にエリアの範囲と照合して求めます。途中参加者も最初のスキャンから正しい状態を表示でき、同期値がずれることもありません。

対象環境は Unity 2022.3、VRChat Worlds SDK 3.8.x 以降、UdonSharp 1.x、TextMeshPro 3.0.6 です。

## 2. 構成要素

| コンポーネント | 数 | 役割 |
| --- | --- | --- |
| `RoomBoardManager` | シーンに 1 つ | 全プレイヤーを定期スキャンし、エリアごとの在室者リストを保持する |
| `RoomBoardArea` | 部屋の数だけ | エリア名・色・範囲（BoxCollider 群）を持つ。ロジックは持たない |
| `RoomBoardDisplay` | 掲示板の数だけ | Manager の結果を表示する。総合掲示板とドア札の両方を同じクラスで実現する |

3 つとも `[UdonBehaviourSyncMode(BehaviourSyncMode.None)]` です。`[UdonSynced]` フィールドは 1 つも持ちません。ネットワークイベントも使いません。

## 3. RoomBoardArea

```csharp
public string areaName = "Room";
public Color areaColor;                 // 手で追加したときは Color.clear。GetAreaColor() が既定色を補う
public BoxCollider[] volumes;
public bool movesAtRuntime = false;     // 動く乗り物の上のエリアだけ true
```

- `volumes` は範囲の形状データです。複数の箱を組み合わせて L 字の部屋なども表せます。`volumes` が空なら、同じ GameObject の `BoxCollider` を 1 つ使います。
- `GetAreaColor()` は `areaColor.a == 0` のとき既定のティール `#2ED3C6` を返します。表示はこちらを使います。
- BoxCollider は**形状データとしてだけ**使います。物理判定やインタラクトの光線を遮らないよう、installer と自動配線は BoxCollider を `enabled = false`、`isTrigger = true` にし、GameObject のレイヤーを `Ignore Raycast`（2）にします。無効化された BoxCollider でも `center` / `size` / `transform` は読めます。
- `OnDrawGizmos` / `OnDrawGizmosSelected` は UdonSharp では使わず、ギズモは Editor 側（§8）で描きます。

### 3.1 判定点

プレイヤーの判定点は `player.GetPosition() + Vector3.up * 0.5f`（足元から 0.5 m 上）です。床面ぴったりの箱でも足が床にめり込んで外れないようにするためです。

### 3.2 内外判定（スケール対応）

箱 `b` に対する判定は、点を箱のローカル空間に変換してから行います。

```text
local = b.transform.InverseTransformPoint(p) - b.center
half  = b.size * 0.5
```

余裕幅 `margin`（メートル）を使う判定では、ローカル空間の値は箱のスケールで縮んでいるので、**軸ごとに `abs(b.transform.lossyScale)` で割ってローカル単位へ換算**します。

```text
scale = abs(b.transform.lossyScale)   // 各軸。0 の軸は 1e-4 に丸める
inside(axis) = abs(local.axis) <= half.axis + margin / scale.axis
```

`margin = 0` のとき厳密判定、`margin > 0` のとき拡張判定です。エリアの内外は「`volumes` のどれか 1 つの内側」です。

### 3.3 ジオメトリのキャッシュ

判定は 1 回の走査でプレイヤー数 × エリア数だけ呼ばれるので、箱ごとの `worldToLocalMatrix`・`center`・`half`・`1 / scale` を `RefreshGeometry()` でキャッシュし、判定は `MultiplyPoint3x4` 1 回と比較だけにします。

- キャッシュは最初の `ContainsPoint` で作ります（`Start` を待たない）。非アクティブで始まるエリアは `Start` が呼ばれず、Manager が `Start` より先に問い合わせることもあるためです。
- エリアは動かない前提です。`movesAtRuntime = true` のエリアだけ、毎回キャッシュを作り直します。実行中に箱を動かした場合は `RefreshGeometry()` を呼んでください。

公開メソッド:

```csharp
public bool ContainsPoint(Vector3 worldPoint, float marginMeters);
public void RefreshGeometry();
public Color GetAreaColor();
```

## 4. RoomBoardManager

### 4.1 Inspector

```csharp
public RoomBoardArea[] areas;          // 配列順が優先度（小さい index が優先）
public float scanInterval = 0.5f;         // 秒
public float exitMargin = 0.3f;           // メートル。退室だけに使うヒステリシス
public UdonBehaviour[] listeners;         // 変化時に OnPresenceChanged を送る
public int playersPerFrame = 6;           // 1 フレームで判定する人数（1 以上）
```

### 4.2 所属の決め方（1 人 1 エリア）

各プレイヤーは**同時に最大 1 つのエリア**に所属します。エリアが重なる場合は配列順で優先します。入れ子の部屋（大部屋の中の個室）は、個室を大部屋より前に並べれば個室が優先されます。

スキャンごとに、各プレイヤーの所属エリア `cur`（いなければ -1）を次の順で決め直します。

1. index が `cur` より小さいエリア（`cur = -1` なら全エリア）を index 順に**厳密判定**し、最初に当たったものに所属する。
2. 1 で決まらず `cur >= 0` なら、`cur` を**拡張判定**（`exitMargin`）し、当たればそのまま残留する。
3. 2 で外れたら、`cur` より後ろのエリアを index 順に**厳密判定**し、最初に当たったものに所属する。どれにも当たらなければ -1。

入るときは厳密判定、出るときは `exitMargin` ぶん広げて判定するので、入口に立っている人の表示が点滅しません。より優先度の高いエリアには、現在のエリアの余裕幅の中にいても入れます。

### 4.3 データ構造

UdonSharp の制約に合わせて、独自クラスを使わず平坦な配列で持ちます。`MaxPlayers = 128` の固定長です。

```text
slotPlayerId[MaxPlayers]   int   空きは -1
slotName[MaxPlayers]       string  入室時点の displayName をキャッシュ
slotArea[MaxPlayers]       int   所属エリア index、-1 は無所属
slotEnteredAt[MaxPlayers]  float 現在のエリアに入った Time.time（ローカル）
slotHighlight[MaxPlayers]  bool  入室ハイライト対象か（§4.5）
slotIsLocal[MaxPlayers]    bool
slotSweep[MaxPlayers]      int   最後にその人を見た走査の番号
occupiedSlots / occupiedCount    使用中スロットの一覧（全 128 枠を舐めないため）
slotByPlayerId             DataDictionary  playerId → スロット（O(1) で引く）

memberSlots[areaCount * MaxPlayers]  エリアごとの在室スロット（入室順に並べ済み）
memberCount[areaCount]
```

並び順は「`slotEnteredAt` の昇順、同値なら `slotPlayerId` の昇順」です。入室時刻は各クライアントのローカル観測なので、同じ走査で観測した人には同じ時刻（その走査の開始時刻）を入れ、順序を playerId で決めます。走査は複数フレームにまたがるので、フレームごとの `Time.time` を入れると同じ走査の人どうしの順序がクライアントごとに変わってしまいます。

### 4.4 走査（複数フレームに分散）

Udon は命令ごとのコストが C# より桁違いに重いので、全員を 1 フレームで判定すると満員のインスタンスで周期的なカクつきになります。走査は次のように分散します。

- 走査中でなく、前回の走査開始から `scanInterval` 秒経ったら新しい走査を始めます。開始時に `VRCPlayerApi.GetPlayers(buffer)`（事前確保した長さ `MaxPlayers` の配列）と `GetPlayerCount()` を 1 回ずつ呼び、走査番号 `sweepId` を進め、開始時刻 `sweepStartedAt` を記録します。
- 各フレームでバッファから最大 `playersPerFrame` 人を判定します。`Utilities.IsValid` でない人と、退室直後の人（§4.5）は飛ばします。判定した人のスロットに `slotSweep = sweepId` を記録し、所属が変わった人は `slotEnteredAt = sweepStartedAt` にします。
- バッファを最後まで処理したら走査終了です。この走査で見なかったスロットを解放し、所属に変化があったときだけ `memberSlots` を作り直します。並びが前回から変わっていれば `revision` を 1 増やし、`listeners` の非 null 要素に `SendCustomEvent("OnPresenceChanged")` を送ります。
- `ScanNow()` は「次の走査をすぐ始める」要求です。`Start` 直後と `OnPlayerJoined` / `OnPlayerLeft` の次のフレームに呼びます（`SendCustomEventDelayedFrames(nameof(ScanNow), 1)`）。1 フレームで全員を判定することはしません。

### 4.5 退室と初回スキャン

- `OnPlayerLeft(player)` では、その `playerId` のスロットを**直ちに**解放し、メンバーリストを作り直して `revision` を増やします。退室者は直後のフレームでもまだ `GetPlayers` に返ることがあり、進行中の走査のバッファにも残っているので、退室した playerId を 8 件のリングに 2 秒間記録し、その間は判定で飛ばします。同じフレームに複数人が退室しても全員が消えたままになります。
- **ローカルプレイヤーの入場後、最初の有効な走査から 3 秒間（`BaselineSeconds`）は基準状態の取得期間として扱います。** この期間に所属が決まった・変わったプレイヤーは `slotHighlight = false` にします。入場直後は他プレイヤーの位置がまだ定まらず、数百 ms 遅れて「入室」したように見えることがあるため、初回スキャン 1 回だけでは足りません。これをしないと、途中参加した瞬間に既に部屋にいる全員がハイライトされます。期間後に所属エリアが変わったプレイヤーは `slotHighlight = true` にします。

### 4.6 公開 API（Display 用）

すべて計算・参照だけで、状態を変えません。

```csharp
public int GetRevision();
public int GetAreaCount();
public RoomBoardArea GetArea(int areaIndex);
public int IndexOfArea(RoomBoardArea area);       // 見つからなければ -1
public int GetMemberCount(int areaIndex);
public string GetMemberName(int areaIndex, int k);   // k は 0 始まりの入室順
public float GetMemberEnteredAt(int areaIndex, int k);
public bool GetMemberHighlight(int areaIndex, int k);
public bool GetMemberIsLocal(int areaIndex, int k);
public int GetTotalPresentCount();                   // いずれかのエリアにいる人数
```

範囲外の index には `0` / `""` / `false` / `null` を返します。

## 5. RoomBoardDisplay

### 5.1 Inspector

```csharp
public RoomBoardManager manager;
public RoomBoardArea[] areas;        // 表示するエリア。空なら manager の全エリア
public string boardTitle = "在室状況";  // 空文字ならタイトル行を隠す
public int maxNamesPerArea = 12;        // 超えた分は「ほか N 人」
public float highlightSeconds = 5f;     // 入室ハイライトの表示時間
public string nameSeparator = "　";     // 名前の区切り（全角スペース）
public string labelEmpty = "空室";
public string labelCountSuffix = "人";
public string labelOthersPrefix = "ほか ";
public string labelOthersSuffix = " 人";
public string labelUnregistered = "未登録";
public float textScale = 1f;            // エディターで適用する文字サイズ倍率（実行時は読まない）

public TextMeshProUGUI titleText;       // null 可
public RectTransform rowContainer;      // 行を並べる親（VerticalLayoutGroup 付き）
public GameObject rowTemplate;          // 行のひな形。非アクティブで置く
```

### 5.1.1 文字サイズ倍率

遠くから読む掲示板のために、`RoomBoardDisplayEditor`（カスタムインスペクター）の `文字サイズ` スライダー（0.5〜3 倍）で倍率を変えられます。適用は**エディター上**で行い、`ApplyTextScale(display, scale)` が旧倍率との比 `r = new / old` を次の値に掛けます。

- タイトルの `fontSize` と `LayoutElement.preferredHeight`
- 行のひな形の `LayoutElement.preferredHeight` / `minHeight`、`AreaName` / `Count` / `Names` の `fontSize`、`Accent` を含む 4 部品の `offsetMin` / `offsetMax`
- 行コンテナとパネルの `VerticalLayoutGroup.spacing`

パネルの `padding` は整数で、スライダーのドラッグ中に小さな比を何度も掛けると丸めで動かなくなるので、拡大しません。実行時の行はひな形の複製なので、ランタイムのコードは倍率を読みません。比を掛ける方式なので、2.0 から 1.0 に戻すと元の値に戻ります。掲示板の幅は変わらないので、1 行に入る名前は減ります。掲示板全体を大きくする場合は Transform の Scale を使います。

### 5.2 行の生成

- `Start` で、表示するエリア数だけ `rowTemplate` を `Instantiate` して `rowContainer` の子にし、アクティブにします。ひな形自体は非アクティブのまま残します。
- 行の中の部品は名前で `transform.Find` して取得します。

| 子の名前 | 型 | 内容 |
| --- | --- | --- |
| `Accent` | Image | エリア色の帯。在室者がいればエリア色、空室ならグレー `#6B7A85` |
| `AreaName` | TextMeshProUGUI | エリア名 |
| `Count` | TextMeshProUGUI | 人数＋`labelCountSuffix`（「3人」）。空室なら `labelEmpty` |
| `Names` | TextMeshProUGUI | 在室者の名前 |

- `areas` に指定されたエリアが manager に登録されていなければ、その行は `labelUnregistered` と表示し、`Debug.LogWarning` を 1 回だけ出します。`manager` が未設定のときも `Start` で警告を 1 回出します。
- 表示される語句はすべて Inspector の項目です。既定値は日本語で、他の言語では書き換えて使います。
- 総合掲示板とドア札は、同じクラスを違う見た目の prefab で使い分けます（§7）。ドア札は `areas` に 1 部屋だけを指定します。

### 5.3 更新

- 毎フレーム `manager.GetRevision()` を見て、前回描画時から変わったとき**だけ**全行の文字列を作り直します。
- ハイライトは時間で消えるので、描画時に「最も早く切れるハイライトの時刻」を覚えておき、`Time.time` がそれを過ぎたときにも作り直します。
- 文字列の組み立ては `string.Format` や `$""` を使わず、`+` 連結で行います。

### 5.4 名前の書式とエスケープ

`Names` は rich text を有効にします。名前は次の**1 つの規則だけ**でエスケープします。

> 名前の中の `<` を、すべて `<` + U+200B（ゼロ幅スペース）に置き換える。

これで名前がタグとして解釈されることはありません（`<b>` は `<​b>` になり、文字として表示される）。`<noparse>` は使いません。

各名前は、エスケープ後に次の装飾をします。

- ローカルプレイヤー自身: `<b>` で太字
- ハイライト中（`GetMemberHighlight` が true かつ `Time.time - enteredAt < highlightSeconds`）: `<color=#F4B942>` で強調

名前は `nameSeparator` で区切り、`maxNamesPerArea` 人を超えた分は末尾に `labelOthersPrefix + N + labelOthersSuffix`（「ほか N 人」）を付けます。空室なら `Names` は空文字です。

## 6. フォント

プレイヤー名は任意の文字を含むので、**動的 TMP フォントアセット**を使います。

- 同梱の `Fonts/NotoSansJP-Regular.otf`（SIL OFL 1.1）から `Fonts/RoomBoard JP Dynamic.asset` を生成します。
- `TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true)`（最後の引数で multi atlas を有効化）。
- ビルドに元フォントが含まれるよう、フォントアセットの source font 参照（`sourceFontFile`）が null でないことを installer で確認し、null ならエラーにします。
- 保存前に `ClearFontAssetData(true)` で、エディター上のプレビューで溜まったグリフを消します（パッケージの肥大化を防ぐ）。
- `m_ClearDynamicDataOnBuild = true` にします。エディターで追加されたグリフをビルドに持ち込まず、実行時に空のアトラスから必要な文字だけを生成させるためです（setter が internal なので `SerializedObject` 経由で書きます）。
- Noto Sans JP にない文字（ハングル、絵文字など）は □ で表示されます。README の既知の制限に書きます。

## 7. Prefab と installer

installer のメニュー:

- `Tools/TenteEEEE/Room Board/Build Prefabs`
- `Tools/TenteEEEE/Room Board/Install Sample into Current Scene`
- `Tools/TenteEEEE/Room Board/Auto-Wire Current Scene`
- `Tools/TenteEEEE/Room Board/Export UnityPackage`

バッチ用に `BuildPrefabsBatch()` と `ExportUnityPackageBatch()` を public static で用意します（`-executeMethod` 用、失敗時は例外をログに出して `EditorApplication.Exit(1)`）。

- UdonSharp の program asset がまだ無ければ作成し、その回は「もう一度実行してください」という例外で止まります（コンパイルを待つ必要があるため、新しいコピーでは 2 回実行が必要）。
- ルートは `Packages/com.tentee.vrc-room-board/` が有ればそこを、無ければ `Assets/RoomBoard/` を使います。
- `Generated/` の UI 用・TMP 用マテリアルは render queue 3000・`unity_GUIZTestMode = LessEqual` を明示します。通常の半透明ジオメトリと同じ奥行きテストで描くためです。
- 一時的な Build Temp ルートが残っていれば消してから作ります。何度実行しても同じパスに上書きします。
- パッケージには生成済みの program asset・フォント・マテリアル・prefab を含めて配布するので、利用者が `Build Prefabs` を実行する必要はありません。

### 7.1 Prefab

| Prefab | 内容 |
| --- | --- |
| `Prefabs/RoomBoard Manager.prefab` | `RoomBoardManager` だけを持つ空の GameObject |
| `Prefabs/RoomBoard Area.prefab` | `RoomBoardArea` + BoxCollider（size 4×3×4、center (0,1.5,0)、無効・トリガー・Ignore Raycast） |
| `Prefabs/RoomBoard Overview.prefab` | 総合掲示板。幅 0.9 m 相当。タイトル行＋行のひな形 1 つ。行は高さ 100 px 固定（部屋名 20 px＋名前 18 px で 2 行）で縦に並び、背景は行数に合わせて上端基準で伸びる（`VerticalLayoutGroup` + `ContentSizeFitter`、pivot 上端）。`maxNamesPerArea = 12` |
| `Prefabs/RoomBoard Door Sign.prefab` | ドア札。幅 0.35 m 相当。タイトルなし（`boardTitle = ""`）、行は 1 つで高さ 150 px（部屋名 26 px＋名前 22 px で約 3 行）。3 行に収まらない分が「…」で消えないよう `maxNamesPerArea = 4` |

- World Space Canvas、sorting order 0。操作しないので `GraphicRaycaster`・`VRCUiShape`・BoxCollider は付けません（インタラクトの光線を遮らないため）。
- 色のトークン: 背景 `#07131B`、面 `#0F2431`、文字 `#EAF4F5`、補助文字 `#8FA8B3`。
- 行内の部品は、伸縮アンカーからの上下左右の余白（offset）で配置します。部屋名は左上、人数は右上、名前欄はその下の残り全体です。
- 部屋名と名前欄のはみ出しは Ellipsis。名前欄は折り返しあり。

### 7.2 Install Sample into Current Scene

現在のシーンに、配線済みのサンプル一式を置きます: Manager 1、Area 2（「部屋A」「部屋B」、色違い、x = ±5 m）、Board 1（原点付近、高さ 1.5 m）、Door Sign 2（各部屋の手前）。シーンに既に Manager がある場合は何も置かずにエラーを出します。

### 7.3 Auto-Wire Current Scene

- シーンの `RoomBoardManager` が 1 つでなければエラーダイアログ（バッチモードではエラーログ）を出して終了。
- Manager の `areas` が空なら、シーン内の全 `RoomBoardArea` を階層順で登録する（空でなければ触らない）。
- `manager` が null の全 `RoomBoardDisplay` に、その Manager を設定する。
- 全 Area の BoxCollider（自分の GameObject のものと `volumes` に登録されたもの）を §3 の状態（無効・トリガー・Ignore Raycast）にそろえる。
- UdonSharp のプロキシ経由で書き、Undo に積み、シーンを dirty にする。何を変更したかをログにまとめて出す。

### 7.4 ギズモ

Editor に `[DrawGizmo]` を使った描画を置き、各 Area の `volumes` をエリア色の半透明の箱（選択時は不透明の枠）で描きます。

## 8. ファイル構成

```text
Packages/com.tentee.vrc-room-board/   （開発時は Assets/RoomBoard/）
  package.json
  LICENSE
  CHANGELOG.md
  README.md
  README.ja.md
  docs/SPEC.md
  Runtime/RoomBoard.Runtime.asmdef
  Runtime/RoomBoardManager.cs
  Runtime/RoomBoardArea.cs
  Runtime/RoomBoardDisplay.cs
  Runtime/RoomBoard.Runtime.UdonSharpAssembly.asset
  Runtime/RoomBoard*.asset         （installer が生成）
  Editor/RoomBoard.Editor.asmdef
  Editor/RoomBoardInstaller.cs
  Editor/RoomBoardInstaller.Ui.cs
  Editor/RoomBoardGizmos.cs
  Editor/RoomBoardDisplayEditor.cs
  Fonts/NotoSansJP-Regular.otf
  Fonts/OFL-1.1.txt
  Fonts/RoomBoard JP Dynamic.asset （installer が生成）
  Generated/                          （installer が生成）
  Prefabs/                            （installer が生成）
```

名前空間はランタイムが `RoomBoard`、エディターが `RoomBoardEditor` です。

## 9. 実装上の制約

- 同期フィールド・ネットワークイベントを使わない。
- 毎フレームの処理で配列を確保しない（`GetPlayers` のバッファ、メンバー配列は Start で確保）。
- 独自クラスや構造体の配列を使わない。平坦な配列で持つ。
- 時間表示や文字列は複合書式に頼らず、`+` 連結と `ToString()` で組み立てる。
- `Time.time` はハイライトと入室順というローカルだけで完結する用途に限る。

## 10. 既知の制限

- 1 人が同時に所属するエリアは 1 つだけです。「フロア全体」の掲示板で、入れ子の個室にいる人を同時に数えることはできません。
- 入室順は各クライアントの観測順です。途中参加者には、参加時点で部屋にいた人が playerId 順に並びます。
- 判定間隔（既定 0.5 秒）と VRChat の位置同期の遅れのぶん、表示は実際の移動から少し遅れます。

見た目、名前の表示、複数人での在室判定は、Unity と VRChat を実際に動かして確認します。コードだけで確認できない部分を動作確認済みとは扱いません。
