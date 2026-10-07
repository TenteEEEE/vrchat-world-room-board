# Room Board（在室ボード）

Room Board は、VRChat ワールドの各エリア（部屋）に今誰がいるかを表示するギミックです。ロビーに全部屋の一覧を出す総合掲示板を、各部屋のドア横にその部屋だけを出すドア札を置けます。

在室状況はネットワーク同期しません。VRChat が各クライアントに届けているプレイヤー位置を、各クライアントが自分でエリアと照合します。途中参加した人にも入った直後から正しい状態が表示され、通信量も増えません。

## 必要環境

- Unity 2022.3
- VRChat SDK Worlds 3.x（`com.vrchat.worlds` `^3.8.0`）
- VRChat Worlds SDK に含まれる UdonSharp
- TextMeshPro Essential Resources

## 導入

1. TenteEEEE の VPM listing から VCC 経由でパッケージを追加します。
2. シーンを開き、`Tools > TenteEEEE > Room Board > Install Sample into Current Scene` を実行します。Manager 1 つ、Area 2 つ、総合掲示板 1 つ、ドア札 2 つが配線済みの状態で置かれます。
3. Area を部屋の形に合わせて移動・拡大し、掲示板とドア札を好きな場所へ動かします。

パッケージには生成済みのプログラムアセット・prefab・フォントが入っているので、`Build Prefabs` を実行する必要はありません。UdonSharp を使うパッケージの通例どおり、プロジェクトでのコンパイル時にプログラムアセットが更新され、動的フォントにはエディターで表示した文字が追加されます。どちらも正常な動作で、追加された文字はワールドのビルドからは除かれます。`Build Prefabs` は、書き込み可能な埋め込みパッケージで再生成するためのものです。新しいコピーで実行するときは 2 回実行してください（1 回目は UdonSharp のプログラムアセットを作るだけで終わります）。

## コンポーネント

| コンポーネント | 数 | 役割 |
| --- | --- | --- |
| `RoomBoardManager` | シーンに 1 つ | プレイヤーを走査し、エリアごとの在室者リストを持つ |
| `RoomBoardArea` | 部屋の数だけ | エリア名・色・範囲（1 つ以上の `BoxCollider`） |
| `RoomBoardDisplay` | 掲示板・札の数だけ | 選んだエリアについて Manager の結果を表示する |

### エリア

- 範囲は Area と同じオブジェクトの `BoxCollider`、または `Volumes` に登録したコライダーです（L 字の部屋は箱を組み合わせます）。コライダーは形状データとしてだけ使うので、無効・Trigger・`Ignore Raycast` レイヤーにしておきます。物理や操作の光線を遮りません。`Auto-Wire Current Scene` がこの設定をそろえます。
- 1 人が同時に属するエリアは 1 つだけです。**Manager の `Areas` の並び順が優先度**で、重なった場所では先のエリアが選ばれます。大部屋の中の個室は、個室を先に並べてください。
- 入るときは箱ちょうど、出るときは `Exit Margin`（既定 0.3 m）だけ広げた箱で判定します。入口に立っている人の表示が点滅しません。
- エリアは動かない前提です。動く乗り物の上などに置く Area は `Moves At Runtime` をオンにしてください。

### 掲示板とドア札

- `RoomBoard Overview.prefab` は複数の部屋を一覧表示します。`Areas` を空にすると Manager の全部屋を、指定するとその部屋だけを表示します。行は実行時に作られ、掲示板は行数に合わせて伸びます。
- `RoomBoard Door Sign.prefab` は 1 部屋を大きな文字で表示します。`Areas` にその部屋を 1 つ設定してください。
- 名前は入室順に並びます。自分の名前は太字、入ってきたばかりの人は `Highlight Seconds` の間ハイライトされます。`Max Names Per Area` を超えた分は「ほか N 人」にまとめます。
- **文字サイズ:** `RoomBoardDisplay` のインスペクター下部の `文字サイズ` スライダー（0.5〜3 倍）で、掲示板の幅はそのままに文字・行の高さ・間隔を拡大できます。遠くから見る掲示板向けです。1 行に入る名前が減るので、名前が「…」で切れるときは `Max Names Per Area` を減らしてください。残りが「ほか N 人」で表示されます。掲示板全体を大きくしたい場合は Transform の Scale を変えてください。
- 表示される語句（`Board Title`、空室、人数の単位、「ほか N 人」、未登録）はすべて `RoomBoardDisplay` の項目で変更できます。既定は日本語です。

## エディターメニュー

`Tools > TenteEEEE > Room Board`

- `Build Prefabs` — プログラムアセット・動的フォント・マテリアル・prefab を再生成します。
- `Install Sample into Current Scene` — 配線済みのサンプル一式を置きます（Manager が既にあるシーンでは実行しません）。
- `Auto-Wire Current Scene` — シーンの Manager がちょうど 1 つのとき、Manager の `Areas` が空なら全 Area を階層順に登録し、Manager 未設定の Display に Manager を設定し、全 Area のコライダーを無効・Trigger・`Ignore Raycast` にそろえます。Undo できます。
- `Export UnityPackage` — パッケージのフォルダを `RoomBoard.unitypackage` に書き出します。

## 外部連携

Manager の `Listeners` に UdonBehaviour を登録すると、どこかのエリアの在室者か並び順が変わるたびに、各クライアントで `OnPresenceChanged` が送られます。詳細は Manager の公開メソッド `GetAreaCount`、`GetArea`、`IndexOfArea`、`GetMemberCount`、`GetMemberName`、`GetMemberEnteredAt`、`GetMemberHighlight`、`GetMemberIsLocal`、`GetTotalPresentCount`、`GetRevision` で取得できます。

## 負荷

Manager は 1 回の走査を複数フレームに分け（`Players Per Frame`、既定 6 人）、`Scan Interval`（既定 0.5 秒）ごとに新しい走査を始めます。掲示板は在室者が変わったときとハイライトが切れたときだけ描き直します。

## 既知の制限

- 1 人は 1 つのエリアにしか数えられません。「フロア全体」の掲示板で、入れ子の個室にいる人を同時に数えることはできません。
- 入室順は各クライアントの観測順です。途中参加した人には、その時点で部屋にいた人が playerId 順に並びます。
- 表示は実際の移動から、最大で走査間隔 1 回分と VRChat の位置同期の遅れだけ遅れます。
- 同梱の Noto Sans JP はラテン文字・日本語・全角文字に対応しています。ハングルや絵文字など、含まれない文字は □ で表示されます。
- ワールドを公開する前に、見た目・名前の表示・複数人での動作を Unity と VRChat で確認してください。

詳しい動作は [`docs/SPEC.md`](docs/SPEC.md) を参照してください。

## ライセンスとクレジット

コードと生成アセットは `LICENSE` の MIT License で公開しています。同梱フォント Noto Sans JP は SIL Open Font License 1.1 で提供されています。`Fonts/OFL-1.1.txt` を参照してください。
