# GULAG Desktop UI Layer Catalog

## Runtime target

- Desktop 16:9, reference resolution `1920 x 1080`.
- Canvas Scaler: `Scale With Screen Size`, match width/height `0.5`.
- Runtime text: TextMeshPro with `Tomorrow-SemiBold`. Text is never baked into these sprites.
- Safe area: `64 px` horizontal, `48 px` vertical at the reference resolution.
- Layout grid: `8 px`.

> The repository currently contains `Tomorrow-SemiBoldItalic.ttf`, not the requested upright `Tomorrow-SemiBold.ttf`. Do not silently substitute the italic face. Connect the upright font asset when it is added.

## Naming

`T_UI_<Feature>_<Element>_<State>[_9S].png`

- `T`: texture asset.
- `UI`: UI-only rendering.
- `Feature`: `Common`, `Start`, `HUD`, or `Result`.
- `State`: `Normal`, `Focused`, `Pressed`, `Active`, `Empty`, `Ready`, or another visible state.
- `9S`: Unity Image Type `Sliced`; the sprite border is stored in the TextureImporter.
- Cyan corner flourishes are separate `TL` / `BR` overlay sprites so they never stretch with a 9-slice panel.

## Canvas hierarchy

### StartMenuCanvas / Settings

1. `Background`: existing `T_UI_Start_Background`.
2. `Frame`: existing `T_UI_Start_Frame`.
3. `PrimaryNavigation`: existing menu-button state sprites.
4. `SettingsPanel`: `T_UI_Start_SettingsPanel_Default_9S` plus optional common corner-accent overlays.
5. `SettingsRows`: normal or focused settings rows.
6. `Controls`: toggle, slider track/fill/handle, and value box.
7. `TMP_Text`: title, labels, values, button captions.

Reference sizes: panel `880 x 560`, row `816 x 64`, menu button `320 x 56`, back button `160 x 56`.

### CrosshairCanvas / Gameplay HUD

1. `Timer`: top-center timer panel.
2. `ScoreCombo`: top-right score and combo panels.
3. `Crosshair`: existing `T_UI_Crosshair_Default_Crisp` at screen center.
4. `WeaponRail`: inactive, active, and empty weapon rows; ready skill row.
5. `Feedback`: combo pips and pickup toast.
6. `TMP_Text`: timer, score, ammo, labels, and transient feedback.

Reference sizes: timer `304 x 72`, score `320 x 96`, combo `320 x 136`, inactive weapon row `320 x 48`, active weapon row `320 x 72`, skill row `320 x 48`, pickup toast `224 x 48`.

### ResultCanvas

1. `Background`: existing `T_UI_Result_Background`.
2. `Summary`: `T_UI_Result_SummaryPanel_Default_9S`.
3. `Columns`: run data, loadout kills, and global ranking panels.
4. `Rows`: data rows, ranking rows, and focused player row.
5. `Submission`: input field and submit button.
6. `Actions`: secondary main-menu and primary retry buttons.
7. `TMP_Text`: all headings, values, rank data, and actions.

Reference sizes: summary `1600 x 120`, data panels `504 x 472`, data row `472 x 80`, ranking row `472 x 48`, input `296 x 64`, submit `208 x 64`, retry `424 x 88`.

## Import contract

- Texture Type: `Sprite (2D and UI)`.
- Sprite Mode: `Single`.
- Mesh Type: `Full Rect`.
- Mip Maps: disabled.
- Alpha Is Transparency: enabled.
- Wrap Mode: `Clamp`.
- Filter Mode: `Bilinear`.
- Compression: `None` for the reusable line-art sprites.
- Pixels Per Unit: `100`.
- Pivot: centered.
- `9S` files store a `12 px` sprite border; thin controls use `6 px` or `8 px` as listed in `UI_LayerCatalog.json`.
- Do not merge corner-accent overlays into a sliced sprite. Anchor `TL` to top-left and `BR` to bottom-right at native `24 x 24 px`.

## Source concepts

- `Assets/Art/Concept/UI/StartMenu/StartMenu_Settings_Concept_v3_1920x1080.png`
- `Assets/Art/Concept/UI/InGame/CrosshairCanvas_HUD_Concept_v7_1920x1080.png`
- `Assets/Art/Concept/UI/Result/ResultCanvas_Concept_v4_1920x1080.png`
