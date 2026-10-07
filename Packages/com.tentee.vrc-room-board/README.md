# Room Board

Room Board shows who is currently inside each area of a VRChat world. Put a board in the lobby
that lists every room, and a small sign next to each door that shows just that room.

Occupancy is not synchronized over the network. Every client reads the player positions VRChat
already delivers and checks them against the areas itself, so late joiners see the correct state
immediately and the package adds no network traffic.

## Requirements

- Unity 2022.3
- VRChat SDK Worlds 3.x (`com.vrchat.worlds` `^3.8.0`)
- UdonSharp included with the VRChat Worlds SDK
- TextMeshPro Essential Resources

## Install

1. Add the package through VCC using the TenteEEEE VPM listing.
2. Open the scene and run `Tools > TenteEEEE > Room Board > Install Sample into Current Scene`.
   This places a Manager, two Areas, one Board, and two Door Signs, already wired together.
3. Move and resize the Areas to fit your rooms, and move the Board and Door Signs where you want them.

The package ships prebuilt program assets, prefabs, and font, so you never need to run
`Build Prefabs`. As with any UdonSharp package, UdonSharp updates the program assets when it compiles
in your project, and the dynamic font collects the glyphs the editor displays; both are normal, and
the collected glyphs are cleared from world builds. `Build Prefabs` regenerates everything for a
writable, embedded copy of the package; on a fresh copy run it twice (the first run only creates the
UdonSharp program assets).

## Components

| Component | How many | Role |
| --- | --- | --- |
| `RoomBoardManager` | One per scene | Scans players and keeps the member list of every area |
| `RoomBoardArea` | One per room | Area name, color, and shape (one or more `BoxCollider`s) |
| `RoomBoardDisplay` | One per board or sign | Shows the Manager's result for the areas you choose |

### Areas

- The area shape is the `BoxCollider` on the Area object, or the colliders listed in `Volumes`
  (combine several boxes for an L-shaped room). The colliders are used as shape data only: keep them
  disabled, set to Trigger, on the `Ignore Raycast` layer so they never block physics or interaction.
  `Auto-Wire Current Scene` applies these settings for you.
- A player belongs to at most one area at a time. **The order of the Manager's `Areas` array is the
  priority**: where areas overlap, the earlier one wins. For a small room inside a larger one, list the
  small room first.
- Entering uses the exact box; leaving uses the box grown by `Exit Margin` (0.3 m by default), so a
  person standing in a doorway does not flicker in and out.
- Areas are assumed not to move. Tick `Moves At Runtime` on an Area that sits on a moving object.

### Boards and signs

- `RoomBoard Overview.prefab` lists several rooms. Leave its `Areas` empty to show every room the
  Manager knows, or list the rooms you want. Rows are created at runtime and the board grows to fit.
- `RoomBoard Door Sign.prefab` shows one room in large type. Set its `Areas` to that one room.
- Names are listed in order of arrival. Your own name is bold. Someone who just walked in is
  highlighted for `Highlight Seconds`. Beyond `Max Names Per Area`, the rest is summarized as
  "and N more".
- **Text size:** the `文字サイズ` slider at the bottom of the `RoomBoardDisplay` inspector (0.5x to
  3x) enlarges the text, row height, and spacing while keeping the board width, for boards read from a
  distance. Fewer names fit per line, so lower `Max Names Per Area` until the rest reads as "and N
  more" instead of being cut off. To make the whole board bigger instead, change the Transform scale.
- All visible words (`Board Title`, empty-room label, count suffix, "and N more", unregistered label)
  are fields on `RoomBoardDisplay`. The defaults are Japanese; change them for other languages.

## Editor menu

`Tools > TenteEEEE > Room Board`

- `Build Prefabs` — regenerate program assets, the dynamic font, materials, and prefabs.
- `Install Sample into Current Scene` — place a wired sample set (refuses if the scene already has a Manager).
- `Auto-Wire Current Scene` — with exactly one Manager in the scene: register all Areas in hierarchy
  order if the Manager's `Areas` is empty, assign the Manager to every Display that has none, and
  normalize every Area collider (disabled, Trigger, `Ignore Raycast`). Undoable.
- `Export UnityPackage` — export the package folder as `RoomBoard.unitypackage`.

## Integration

Add UdonBehaviours to the Manager's `Listeners` to receive `OnPresenceChanged` on every client
whenever the set or order of people in any area changes. Read the details through the Manager's
public methods: `GetAreaCount`, `GetArea`, `IndexOfArea`, `GetMemberCount`, `GetMemberName`,
`GetMemberEnteredAt`, `GetMemberHighlight`, `GetMemberIsLocal`, `GetTotalPresentCount`, `GetRevision`.

## Performance

The Manager spreads each scan over several frames (`Players Per Frame`, default 6) and starts a new
scan every `Scan Interval` seconds (default 0.5). Boards redraw only when membership changes or a
highlight expires.

## Known limitations

- A person is counted in one area only. A "whole floor" board cannot also count people who are in a
  nested room.
- Arrival order is what each client observed. Someone who joins the instance late sees the people
  already present ordered by player ID.
- The display lags real movement by up to one scan interval plus VRChat's own position latency.
- The bundled Noto Sans JP font covers Latin, Japanese, and full-width characters. Hangul, emoji,
  and other scripts it lacks are shown as □.
- Verify the look, name rendering, and multi-player behaviour in Unity and in VRChat before publishing
  your world.

See [`docs/SPEC.md`](docs/SPEC.md) for the detailed behaviour (Japanese).

## License and credits

Code and generated assets are released under the MIT License in `LICENSE`. The bundled Noto Sans JP
font is provided under the SIL Open Font License 1.1; see `Fonts/OFL-1.1.txt`.
