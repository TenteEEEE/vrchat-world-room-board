# Room Board（在室ボード）

VRChat ワールドの各エリアに今誰がいるかを、掲示板やドア札に表示する VPM パッケージ `com.tentee.vrc-room-board` のリポジトリです。ネットワーク同期は使いません。

パッケージ本体は `Packages/com.tentee.vrc-room-board/` にあります。使い方は [`README.ja.md`](Packages/com.tentee.vrc-room-board/README.ja.md)（日本語）と [`README.md`](Packages/com.tentee.vrc-room-board/README.md)（English）を参照してください。

## 導入

VCC に TenteEEEE の VPM listing（`https://tenteeeee.github.io/vpm-repos/index.json`）を追加し、ワールドのプロジェクトに **Room Board** を追加します。GitHub の各リリースには VPM 用 zip と `.unitypackage` も添付されます。

## 開発

1. このリポジトリを Unity 2022.3 のプロジェクトとして開きます。
2. VRChat Worlds SDK と TextMeshPro Essential Resources を導入します。
3. `Packages/com.tentee.vrc-room-board/` で作業します。
4. リリース前に `python3 Tools/verify-package.py` を実行します。
5. `package.json` の `version` を上げ、同じ `x.y.z` のタグを push します。リリース用ワークフローがパッケージを検証し、VPM 用 zip と `.unitypackage` を作って GitHub のリリースを公開します。

## ライセンス

MIT（`LICENSE` を参照）。同梱フォント Noto Sans JP は SIL Open Font License 1.1 です（`Packages/com.tentee.vrc-room-board/Fonts/OFL-1.1.txt`）。
