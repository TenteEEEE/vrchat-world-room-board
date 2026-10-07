# Room Board

This repository publishes the `com.tentee.vrc-room-board` VPM package for VRChat worlds: boards and
door signs that show who is currently inside each area, with no network sync.

The package is under `Packages/com.tentee.vrc-room-board/`. Its user documentation is in
[`README.md`](Packages/com.tentee.vrc-room-board/README.md) (English) and
[`README.ja.md`](Packages/com.tentee.vrc-room-board/README.ja.md) (日本語).

## Install

Add the TenteEEEE VPM listing to VCC (`https://tenteeeee.github.io/vpm-repos/index.json`) and add
**Room Board** to your world project. Each GitHub release also attaches a VPM zip and a
`.unitypackage`.

## Development

1. Open this repository as a Unity 2022.3 project.
2. Install the VRChat Worlds SDK and TextMeshPro Essential Resources.
3. Work from `Packages/com.tentee.vrc-room-board/`.
4. Run `python3 Tools/verify-package.py` before releasing.
5. Bump `version` in `package.json`, then push a matching `x.y.z` tag. The release workflow verifies the
   package, builds the VPM zip and `.unitypackage`, and publishes the GitHub release.

## License

MIT, see `LICENSE`. The bundled Noto Sans JP font is under the SIL Open Font License 1.1
(`Packages/com.tentee.vrc-room-board/Fonts/OFL-1.1.txt`).
