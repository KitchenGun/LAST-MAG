# Result UI layer guide

## Canvas

- Screen Space - Overlay
- Reference Resolution: `1920 x 1080`
- Scale With Screen Size, Match: `0.5`
- All coordinates below use top-left origin in the 1920 x 1080 reference layout.

## Layer order

1. `T_UI_Result_Background` — full stretch
2. `T_UI_Result_FrameOverlay` — full stretch, raycast disabled
3. Result panels
4. Existing weapon silhouette sprites
5. TextMeshPro text and input field
6. Button and input interaction objects

`T_UI_Result_FullOverlay` is provided for exact-position assembly. Do not display it together with the individually cropped panels.

## Panel placement

| Sprite | X | Y | Width | Height |
| --- | ---: | ---: | ---: | ---: |
| HeaderPlate | 41 | 43 | 624 | 108 |
| ScorePanel | 75 | 159 | 641 | 248 |
| RunDataPanel | 75 | 419 | 642 | 234 |
| WeaponStatsPanel | 75 | 666 | 641 | 249 |
| SubmissionPanel | 735 | 159 | 517 | 657 |
| RankingPanel | 1272 | 159 | 536 | 656 |
| RetryButton | 1238 | 833 | 581 | 140 |
| DeathStrip | 57 | 938 | 866 | 72 |

## Runtime content

- All labels, values, nickname, submission state, ranking rows and button captions use TextMeshPro.
- Weapon silhouettes reuse:
  - `Assets/UI/InGame/Textures/WeaponSilhouettes/T_UI_Weapon_Pistol_Silhouette.png`
  - `Assets/UI/InGame/Textures/WeaponSilhouettes/T_UI_Weapon_Shotgun_Silhouette.png`
  - `Assets/UI/InGame/Textures/WeaponSilhouettes/T_UI_Weapon_Rifle_Silhouette.png`
- Apply Image color at runtime: pistol red `#EA4047`, shotgun green `#35C759`, rifle blue `#2C87E8`.
- Place transparent `Button` and `TMP_InputField` interaction objects over the baked button and input-field shapes.
- Keep `Retry` interactable in every submission state.
