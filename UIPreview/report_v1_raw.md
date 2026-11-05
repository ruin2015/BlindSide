# UI Lint report

Screen 1440x810 · 516 issues · generated 2026-10-02 01:28

Severity (for ordering only): clipped = px over (text ×1.5, masked parent ×2) · offscreen = px × 1.5 · overlap = % of the smaller rect · aspect = diff% × 2 · fontsize = (11 − size) × 10.

## Count by state

| 상태 | 건수 | clipped | overlap | offscreen | aspect | fontsize |
|---|---|---|---|---|---|---|
| title_attack | 32 | 23 | 3 | 1 | 5 | 0 |
| title_defense | 30 | 22 | 2 | 1 | 5 | 0 |
| hud_selected | 80 | 36 | 26 | 4 | 14 | 0 |
| hud_unselected | 69 | 27 | 26 | 3 | 13 | 0 |
| hud_hit | 78 | 33 | 27 | 3 | 15 | 0 |
| hud_ended | 75 | 32 | 26 | 3 | 14 | 0 |
| dialog_move | 83 | 36 | 27 | 4 | 16 | 0 |
| phase_banner | 69 | 27 | 26 | 3 | 13 | 0 |

## Issues

| 상태 | 종류 | 요소명 | 위치 | 측정값 | 기대값 | 심각도 |
|---|---|---|---|---|---|---|
| title_attack | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/BASTION/Badge | x730 y714 72x59.9 | rect 1.2 (72x59.9) | sprite 0 (1x32), diff 3745.2% | 7490.3 |
| title_attack | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/FLARE/Badge | x730 y519.1 72x59.9 | rect 1.2 (72x59.9) | sprite 0 (1x32), diff 3745.2% | 7490.3 |
| title_attack | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/IGNIS/Badge | x730 y584 72x59.9 | rect 1.2 (72x59.9) | sprite 0 (1x32), diff 3745.2% | 7490.3 |
| title_attack | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/SERUM/Badge | x730 y648.9 72x60 | rect 1.2 (72x60) | sprite 0 (1x32), diff 3738.8% | 7477.7 |
| title_attack | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/CRUSHER/Badge | x730 y454 72x60 | rect 1.2 (72x60) | sprite 0 (1x32), diff 3738.8% | 7477.7 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/BASTION/Text | x814 y747 381.3x15 | preferred 525x15 | rect 392x18 | 199.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/IGNIS/Text | x814 y617 381.3x15 | preferred 500x15 | rect 392x18 | 162 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/FLARE/Text | x814 y552 381.3x15 | preferred 475x15 | rect 392x18 | 124.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/SERUM/Text | x814 y682.1 381.3x15 | preferred 444.7x15 | rect 392x18 | 79.1 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Left/Text | x79 y769.9 641.1x52.5 | x79 y769.9 641.1x52.5 (over by 56.1) | inside parent x64 y92.1 600x690 | 56.1 |
| title_attack | overlap | BlindsideCanvas/TitleSelect/Right/WinStartRow/WinBox/Text  ×  WinBox/Text | x754 y369.6 218.3x14.7 | overlap 218.3x14.7 (39.3% of the smaller) | none (or add LintIgnoreOverlap) — A x754 y332.2 218.3x52.1 · B x754 y369.6 272.2x30.1 | 39.3 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/WinStartRow/WinBox/Text | x754 y332.2 218.3x52.1 | preferred 411.9x53.3 | rect 279x37 | 24.4 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/FLARE/Badge/Face | x730 y519.1 72x72 | x730 y519.1 72x72 (over by 12.1) | inside parent x730 y519.1 72x59.9 | 24.1 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/BASTION/Badge/Face | x730 y714 72x72 | x730 y714 72x72 (over by 12.1) | inside parent x730 y714 72x59.9 | 24.1 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/IGNIS/Badge/Face | x730 y584 72x72 | x730 y584 72x72 (over by 12.1) | inside parent x730 y584 72x59.9 | 24.1 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/SERUM/Badge/Face | x730 y648.9 72x72 | x730 y648.9 72x72 (over by 12) | inside parent x730 y648.9 72x60 | 23.9 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/CRUSHER/Badge/Face | x730 y454 72x72 | x730 y454 72x72 (over by 12) | inside parent x730 y454 72x60 | 23.9 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Left/Text | x79 y769.9 641.1x52.5 | preferred 1409.1x54.4 | rect 645x40 | 21.6 |
| title_attack | offscreen | BlindsideCanvas/TitleSelect/Left/Text | x79 y769.9 641.1x52.5 | 79,769.9 → 720.1,822.4 | within 0,0 → 1440,810 | 18.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-B/Text | x1113 y44.6 108x64.9 | preferred 108x64.8 | rect 277x54 | 16.2 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-A/Text | x760 y44.6 108x64.9 | preferred 108x64.8 | rect 277x54 | 16.2 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Left/Text | x64 y123.7 587.6x122.8 | preferred 587.6x122.7 | rect 600x112 | 16.1 |
| title_attack | overlap | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-A/Text  ×  Side_SIDE-A/Text | x760 y107 108x2.5 | overlap 108x2.5 (9.7% of the smaller) | none (or add LintIgnoreOverlap) — A x760 y44.6 108x64.9 · B x760 y107 139.6x19.8 | 9.7 |
| title_attack | overlap | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-B/Text  ×  Side_SIDE-B/Text | x1113 y107 108x2.5 | overlap 108x2.5 (9.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1113 y44.6 108x64.9 · B x1113 y107 140.7x19.8 | 9.6 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/SERUM/Stats/MOV/Text | x1374.1 y677.6 21.9x12.5 | preferred 21.9x12.5 | rect 18x14 | 5.9 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/FLARE/Stats/MOV/Text | x1374.1 y547.7 21.9x12.6 | preferred 21.9x12.5 | rect 18x14 | 5.9 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/IGNIS/Stats/HP/Text | x1374.4 y594.7 21.7x12.5 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/FLARE/Stats/HP/Text | x1374.4 y529.7 21.7x12.5 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/CRUSHER/Stats/HP/Text | x1374.4 y464.6 21.7x12.6 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/SERUM/Stats/HP/Text | x1374.4 y659.7 21.7x12.6 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/WinStartRow/StartButton/Label/Text | x1115 y337.2 152x45.6 | preferred 152x45.6 | rect 215x42 | 5.4 |
| title_attack | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/BASTION/Stats/HP/Text | x1375.5 y724.6 20.5x12.6 | preferred 20.6x12.5 | rect 18x14 | 3.8 |
| title_defense | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/WRAITH/Badge | x730 y584 72x59.9 | rect 1.2 (72x59.9) | sprite 0 (1x32), diff 3745.2% | 7490.3 |
| title_defense | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/MIASMA/Badge | x730 y519.1 72x59.9 | rect 1.2 (72x59.9) | sprite 0 (1x32), diff 3745.2% | 7490.3 |
| title_defense | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/ARGUS/Badge | x730 y714 72x59.9 | rect 1.2 (72x59.9) | sprite 0 (1x32), diff 3745.2% | 7490.3 |
| title_defense | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/VOLT/Badge | x730 y454 72x60 | rect 1.2 (72x60) | sprite 0 (1x32), diff 3738.8% | 7477.7 |
| title_defense | aspect | BlindsideCanvas/TitleSelect/Right/SquadList/OBSIDIAN/Badge | x730 y648.9 72x60 | rect 1.2 (72x60) | sprite 0 (1x32), diff 3738.8% | 7477.7 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/VOLT/Text | x814 y487 381.3x15 | preferred 575x15 | rect 392x18 | 274.5 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/WRAITH/Text | x814 y617 381.3x15 | preferred 487.5x15 | rect 392x18 | 143.3 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/OBSIDIAN/Text | x814 y682.1 381.3x15 | preferred 462.5x15 | rect 392x18 | 105.8 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/ARGUS/Text | x814 y747 381.3x15 | preferred 437.5x15 | rect 392x18 | 68.3 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/MIASMA/Text | x814 y552 375.9x15 | preferred 432.1x15 | rect 392x18 | 60.2 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Left/Text | x79 y769.9 641.1x52.5 | x79 y769.9 641.1x52.5 (over by 56.1) | inside parent x64 y92.1 600x690 | 56.1 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/WRAITH/Badge/Face | x730 y584 72x72 | x730 y584 72x72 (over by 12.1) | inside parent x730 y584 72x59.9 | 24.1 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/ARGUS/Badge/Face | x730 y714 72x72 | x730 y714 72x72 (over by 12.1) | inside parent x730 y714 72x59.9 | 24.1 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/MIASMA/Badge/Face | x730 y519.1 72x72 | x730 y519.1 72x72 (over by 12.1) | inside parent x730 y519.1 72x59.9 | 24.1 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/VOLT/Badge/Face | x730 y454 72x72 | x730 y454 72x72 (over by 12) | inside parent x730 y454 72x60 | 23.9 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/OBSIDIAN/Badge/Face | x730 y648.9 72x72 | x730 y648.9 72x72 (over by 12) | inside parent x730 y648.9 72x60 | 23.9 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Left/Text | x79 y769.9 641.1x52.5 | preferred 1409.1x54.4 | rect 645x40 | 21.6 |
| title_defense | offscreen | BlindsideCanvas/TitleSelect/Left/Text | x79 y769.9 641.1x52.5 | 79,769.9 → 720.1,822.4 | within 0,0 → 1440,810 | 18.5 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-B/Text | x1113 y44.6 108x64.9 | preferred 108x64.8 | rect 277x54 | 16.2 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-A/Text | x760 y44.6 108x64.9 | preferred 108x64.8 | rect 277x54 | 16.2 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Left/Text | x64 y123.7 587.6x122.8 | preferred 587.6x122.7 | rect 600x112 | 16.1 |
| title_defense | overlap | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-A/Text  ×  Side_SIDE-A/Text | x760 y107 108x2.5 | overlap 108x2.5 (9.7% of the smaller) | none (or add LintIgnoreOverlap) — A x760 y44.6 108x64.9 · B x760 y107 139.6x19.8 | 9.7 |
| title_defense | overlap | BlindsideCanvas/TitleSelect/Right/SideCards/Side_SIDE-B/Text  ×  Side_SIDE-B/Text | x1113 y107 108x2.5 | overlap 108x2.5 (9.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1113 y44.6 108x64.9 · B x1113 y107 140.7x19.8 | 9.6 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/WRAITH/Stats/MOV/Text | x1374.1 y612.7 21.9x12.4 | preferred 21.9x12.5 | rect 18x14 | 5.9 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/MIASMA/Stats/HP/Text | x1374.4 y529.7 21.7x12.5 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/VOLT/Stats/HP/Text | x1374.4 y464.6 21.7x12.6 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/WRAITH/Stats/HP/Text | x1374.4 y594.7 21.7x12.5 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/ARGUS/Stats/HP/Text | x1374.4 y724.6 21.7x12.6 | preferred 21.7x12.5 | rect 18x14 | 5.5 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/WinStartRow/StartButton/Label/Text | x1115 y337.2 152x45.6 | preferred 152x45.6 | rect 215x42 | 5.4 |
| title_defense | clipped | BlindsideCanvas/TitleSelect/Right/SquadList/OBSIDIAN/Stats/HP/Text | x1375.5 y659.7 20.5x12.6 | preferred 20.6x12.5 | rect 18x14 | 3.8 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Por/Box | x32 y60.8 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Por/Box | x22.4 y275.2 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Por/Box | x22.4 y221.5 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Por/Box | x22.4 y168 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Por/Box | x22.4 y114.4 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_selected | aspect | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | rect 1 (83.2x83.3) | sprite 0 (1x32), diff 3097.1% | 6194.2 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Paper | x1136.1 y9.5 287.9x46.6 | rect 6.2 (287.9x46.6) | sprite 1 (64x64), diff 518.3% | 1036.5 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Paper | x16 y9.7 223.9x36.7 | rect 6.1 (223.9x36.7) | sprite 1 (64x64), diff 510.5% | 1021 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper | x16 y275.2 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper | x25.6 y60.8 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper | x16 y167.9 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper | x16 y114.4 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper | x16 y221.5 200x49.7 | rect 4 (200x49.7) | sprite 1 (64x64), diff 302% | 604.1 |
| hud_selected | aspect | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper | x778.4 y9.7 104x36.7 | rect 2.8 (104x36.7) | sprite 1 (64x64), diff 183.4% | 366.9 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Hatch  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Hatch  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow  ×  Visual/Sub | x125.6 y178 104.8x11.6 | overlap 104.8x11.6 (77.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y156.8 228.7x78.4 · B x125.6 y178 134.6x11.6 | 77.8 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow  ×  Visual/Sub | x134.4 y124.5 96x11.5 | overlap 96x11.5 (76.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y103.1 228.7x78.5 · B x134.4 y124.5 125x11.5 | 76.8 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow  ×  Visual/Sub | x152.8 y285.3 77.6x11.6 | overlap 77.6x11.6 (73.3% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y263.9 228.7x78.5 · B x152.8 y285.3 105.8x11.6 | 73.3 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub | x168 y70.9 125x11.6 | x168 y70.9 125x11.6 (over by 67.4) | inside parent x25.6 y60.8 200x49.6 | 67.4 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Hatch  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg  ×  TbRight_Objective/EnemyLabel | x1324.8 y16.1 19.2x9.1 | overlap 19.2x9.1 (66.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1164.9 y13.6 179.2x12.8 · B x1324.8 y16.1 28.8x9.1 | 66.5 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Hatch  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_selected | clipped | PlayerSelection_Canvas/CmdMenu/Cmd_skill/Body/Face/T | x323.1 y245.7 120x22.4 | preferred 150x32.2 | rect 107x40 | 64.5 |
| hud_selected | clipped | PlayerSelection_Canvas/UnitPanel/Head/Kr | x128 y680.5 139.7x12 | preferred 230.9x15 | rect 190x16 | 61.4 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Hatch  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_selected | clipped | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | x27.2 y618.1 83.2x83.3 (over by 59.3) | inside parent x16 y618 343.9x24 | 59.3 |
| hud_selected | clipped | PlayerSelection_Canvas/OpArt/Art/Photo | x1154.5 y84.8 299.1x299.1 | x1154.5 y84.8 299.1x299.1 (over by 29.6) | inside parent x1184.1 y84.8 239.9x299.1 | 59.2 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow  ×  Visual/Sub | x168 y70.9 72x11.6 | overlap 72x11.6 (57.6% of the smaller) | none (or add LintIgnoreOverlap) — A x11.2 y49.5 228.7x78.5 · B x168 y70.9 125x11.6 | 57.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | preferred 116.8x31.2 | rect 84x32 | 49.3 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Hatch  ×  Visual/Sub | x168 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x25.6 y60.8 200x49.6 · B x168 y70.9 125x11.6 | 46.1 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper  ×  Visual/Sub | x168 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x25.6 y60.8 200x49.6 · B x168 y70.9 125x11.6 | 46.1 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub | x125.6 y178 134.6x11.6 | x125.6 y178 134.6x11.6 (over by 44.2) | inside parent x16 y167.9 200x49.6 | 44.2 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub | x134.4 y124.5 125x11.5 | x134.4 y124.5 125x11.5 (over by 43.4) | inside parent x16 y114.4 200x49.6 | 43.4 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub | x152.8 y285.3 105.8x11.6 | x152.8 y285.3 105.8x11.6 (over by 42.6) | inside parent x16 y275.2 200x49.6 | 42.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | x1104.9 y-13.5 350.3x108.8 (over by 39.2) | inside parent x1136.1 y9.7 287.9x46.4 | 39.2 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | x-15.2 y-13.5 286.3x99 (over by 38.9) | inside parent x16 y9.7 223.9x36.9 | 38.9 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | x526.4 y-13.5 387.1x99 (over by 38.9) | inside parent x557.6 y9.7 324.7x36.9 | 38.9 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Body | x1164.9 y27.9 246.5x24.5 | preferred 308.2x31.5 | rect 290x36 | 27.3 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y178 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x125.6 y178 134.6x11.6 · B x212.7 y167.9 3.2x49.6 | 23.3 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub  ×  Visual/Edge | x220.7 y70.9 4.8x11.6 | overlap 4.8x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x168 y70.9 125x11.6 · B x220.7 y60.8 4.8x49.6 | 23.3 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y285.3 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x152.8 y285.3 105.8x11.6 · B x212.7 y275.2 3.2x49.6 | 23.3 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y231.7 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x137.6 y231.7 86.6x11.5 · B x212.7 y221.5 3.2x49.7 | 23.1 |
| hud_selected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y124.5 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x134.4 y124.5 125x11.5 · B x212.7 y114.4 3.2x49.6 | 23.1 |
| hud_selected | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | 1104.9,-13.5 → 1455.2,95.2 | within 0,0 → 1440,810 | 22.8 |
| hud_selected | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | -15.2,-13.5 → 271.1,85.4 | within 0,0 → 1440,810 | 22.8 |
| hud_selected | offscreen | PlayerSelection_Canvas/OpArt/Art/Photo | x1154.5 y84.8 299.1x299.1 | 1154.5,84.8 → 1453.6,383.9 | within 0,0 → 1440,810 | 20.4 |
| hud_selected | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | 526.4,-13.5 → 913.6,85.4 | within 0,0 → 1440,810 | 20.3 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow | x1.6 y156.8 228.7x78.4 | x1.6 y156.8 228.7x78.4 (over by 17.7) | inside parent x16 y167.9 200x49.6 | 17.7 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow | x1.6 y103.1 228.7x78.5 | x1.6 y103.1 228.7x78.5 (over by 17.6) | inside parent x16 y114.4 200x49.6 | 17.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow | x11.2 y49.5 228.7x78.5 | x11.2 y49.5 228.7x78.5 (over by 17.6) | inside parent x25.6 y60.8 200x49.6 | 17.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow | x1.6 y263.9 228.7x78.5 | x1.6 y263.9 228.7x78.5 (over by 17.6) | inside parent x16 y275.2 200x49.6 | 17.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Shadow | x1.6 y210.3 228.7x78.4 | x1.6 y210.3 228.7x78.4 (over by 17.5) | inside parent x16 y221.5 200x49.7 | 17.5 |
| hud_selected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1369.8 y785.2 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.6 |
| hud_selected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y710.5 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | x802.4 y15.2 93.4x25 (over by 13.5) | inside parent x778.4 y9.7 104x36.9 | 13.5 |
| hud_selected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1305.4 y785.2 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.5 |
| hud_selected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y775.1 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.5 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/SelArrow | x16 y80.9 4.8x9.5 | x16 y80.9 4.8x9.5 (over by 9.6) | inside parent x25.6 y60.8 200x49.6 | 9.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub | x137.6 y231.7 86.6x11.5 | x137.6 y231.7 86.6x11.5 (over by 8.2) | inside parent x16 y221.5 200x49.7 | 8.2 |
| hud_selected | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 38.8x13.4 | preferred 48.6x16.7 | rect 44.4x20 | 6.3 |
| hud_selected | clipped | PlayerSelection_Canvas/UnitPanel/Head/Code | x128 y647.7 143.1x9.1 | preferred 194x11.5 | rect 190x14 | 6 |
| hud_selected | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 38.8x13.4 | x132.8 y714.8 38.8x13.4 (over by 1.7) | inside parent x128 y701.1 41.9x32.8 | 3.5 |
| hud_selected | clipped | PlayerSelection_Canvas/OpArt/NameText/Text | x1198.5 y396.2 125.2x28.3 | preferred 156.6x35.4 | rect 264x34 | 2.1 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg/Title | x1169.7 y14.2 165.9x10.6 | preferred 207.4x13.2 | rect 212x12 | 1.8 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_2/Shape | x1288.8 y15.2 8.8x9.5 | x1288.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1290.4 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_4/Shape | x1389.6 y15.2 8.8x9.5 | x1389.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1391.2 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_3/Shape | x1381.6 y15.2 8.8x9.5 | x1381.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1383.2 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_2/Shape | x1373.6 y15.2 8.8x9.5 | x1373.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1375.2 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_1/Shape | x1365.6 y15.2 8.8x9.5 | x1365.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1367.2 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_0/Shape | x1357.6 y15.2 8.8x9.5 | x1357.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1359.2 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_4/Shape | x1304.8 y15.2 8.8x9.5 | x1304.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1306.4 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_3/Shape | x1296.8 y15.2 8.8x9.5 | x1296.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1298.4 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_1/Shape | x1280.8 y15.2 8.8x9.5 | x1280.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1282.4 y15.2 5.6x9.6 | 1.6 |
| hud_selected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_0/Shape | x1272.8 y15.2 8.8x9.5 | x1272.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1274.4 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Por/Box | x22.4 y60.8 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Por/Box | x22.4 y275.2 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Por/Box | x22.4 y114.4 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Por/Box | x22.4 y221.5 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Por/Box | x22.4 y168 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Paper | x1136.1 y9.5 287.9x46.6 | rect 6.2 (287.9x46.6) | sprite 1 (64x64), diff 518.3% | 1036.5 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Paper | x16 y9.7 223.9x36.7 | rect 6.1 (223.9x36.7) | sprite 1 (64x64), diff 510.5% | 1021 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper | x16 y275.2 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper | x16 y60.8 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper | x16 y167.9 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper | x16 y114.4 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper | x16 y221.5 200x49.7 | rect 4 (200x49.7) | sprite 1 (64x64), diff 302% | 604.1 |
| hud_unselected | aspect | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper | x778.4 y9.7 104x36.7 | rect 2.8 (104x36.7) | sprite 1 (64x64), diff 183.4% | 366.9 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Hatch  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Hatch  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow  ×  Visual/Sub | x125.6 y178 104.8x11.6 | overlap 104.8x11.6 (77.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y156.8 228.7x78.4 · B x125.6 y178 134.6x11.6 | 77.8 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow  ×  Visual/Sub | x134.4 y124.5 96x11.5 | overlap 96x11.5 (76.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y103.1 228.7x78.5 · B x134.4 y124.5 125x11.5 | 76.8 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow  ×  Visual/Sub | x152.8 y285.3 77.6x11.6 | overlap 77.6x11.6 (73.3% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y263.9 228.7x78.5 · B x152.8 y285.3 105.8x11.6 | 73.3 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub | x158.4 y70.9 125x11.6 | x158.4 y70.9 125x11.6 (over by 67.4) | inside parent x16 y60.8 200x49.6 | 67.4 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Hatch  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg  ×  TbRight_Objective/EnemyLabel | x1324.8 y16.1 19.2x9.1 | overlap 19.2x9.1 (66.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1164.9 y13.6 179.2x12.8 · B x1324.8 y16.1 28.8x9.1 | 66.5 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Hatch  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Hatch  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow  ×  Visual/Sub | x158.4 y70.9 72x11.6 | overlap 72x11.6 (57.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y49.5 228.7x78.5 · B x158.4 y70.9 125x11.6 | 57.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | preferred 116.8x31.2 | rect 84x32 | 49.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Hatch  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub | x125.6 y178 134.6x11.6 | x125.6 y178 134.6x11.6 (over by 44.2) | inside parent x16 y167.9 200x49.6 | 44.2 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub | x134.4 y124.5 125x11.5 | x134.4 y124.5 125x11.5 (over by 43.4) | inside parent x16 y114.4 200x49.6 | 43.4 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub | x152.8 y285.3 105.8x11.6 | x152.8 y285.3 105.8x11.6 (over by 42.6) | inside parent x16 y275.2 200x49.6 | 42.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | x1104.9 y-13.5 350.3x108.8 (over by 39.2) | inside parent x1136.1 y9.7 287.9x46.4 | 39.2 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | x526.4 y-13.5 387.1x99 (over by 38.9) | inside parent x557.6 y9.7 324.7x36.9 | 38.9 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | x-15.2 y-13.5 286.3x99 (over by 38.9) | inside parent x16 y9.7 223.9x36.9 | 38.9 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Body | x1164.9 y27.9 246.5x24.5 | preferred 308.2x31.5 | rect 290x36 | 27.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y285.3 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x152.8 y285.3 105.8x11.6 · B x212.7 y275.2 3.2x49.6 | 23.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y70.9 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x158.4 y70.9 125x11.6 · B x212.7 y60.8 3.2x49.6 | 23.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y178 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x125.6 y178 134.6x11.6 · B x212.7 y167.9 3.2x49.6 | 23.3 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y124.5 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x134.4 y124.5 125x11.5 · B x212.7 y114.4 3.2x49.6 | 23.1 |
| hud_unselected | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y231.7 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x137.6 y231.7 86.6x11.5 · B x212.7 y221.5 3.2x49.7 | 23.1 |
| hud_unselected | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | -15.2,-13.5 → 271.1,85.4 | within 0,0 → 1440,810 | 22.8 |
| hud_unselected | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | 1104.9,-13.5 → 1455.2,95.2 | within 0,0 → 1440,810 | 22.8 |
| hud_unselected | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | 526.4,-13.5 → 913.6,85.4 | within 0,0 → 1440,810 | 20.3 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow | x1.6 y156.8 228.7x78.4 | x1.6 y156.8 228.7x78.4 (over by 17.7) | inside parent x16 y167.9 200x49.6 | 17.7 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow | x1.6 y49.5 228.7x78.5 | x1.6 y49.5 228.7x78.5 (over by 17.6) | inside parent x16 y60.8 200x49.6 | 17.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow | x1.6 y263.9 228.7x78.5 | x1.6 y263.9 228.7x78.5 (over by 17.6) | inside parent x16 y275.2 200x49.6 | 17.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow | x1.6 y103.1 228.7x78.5 | x1.6 y103.1 228.7x78.5 (over by 17.6) | inside parent x16 y114.4 200x49.6 | 17.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Shadow | x1.6 y210.3 228.7x78.4 | x1.6 y210.3 228.7x78.4 (over by 17.5) | inside parent x16 y221.5 200x49.7 | 17.5 |
| hud_unselected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y710.5 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.6 |
| hud_unselected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1369.8 y785.2 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | x802.4 y15.2 93.4x25 (over by 13.5) | inside parent x778.4 y9.7 104x36.9 | 13.5 |
| hud_unselected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1305.4 y785.2 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.5 |
| hud_unselected | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y775.1 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.5 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub | x137.6 y231.7 86.6x11.5 | x137.6 y231.7 86.6x11.5 (over by 8.2) | inside parent x16 y221.5 200x49.7 | 8.2 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg/Title | x1169.7 y14.2 165.9x10.6 | preferred 207.4x13.2 | rect 212x12 | 1.8 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_2/Shape | x1288.8 y15.2 8.8x9.5 | x1288.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1290.4 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_3/Shape | x1296.8 y15.2 8.8x9.5 | x1296.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1298.4 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_0/Shape | x1272.8 y15.2 8.8x9.5 | x1272.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1274.4 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_0/Shape | x1357.6 y15.2 8.8x9.5 | x1357.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1359.2 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_1/Shape | x1365.6 y15.2 8.8x9.5 | x1365.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1367.2 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_2/Shape | x1373.6 y15.2 8.8x9.5 | x1373.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1375.2 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_3/Shape | x1381.6 y15.2 8.8x9.5 | x1381.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1383.2 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_4/Shape | x1389.6 y15.2 8.8x9.5 | x1389.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1391.2 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_1/Shape | x1280.8 y15.2 8.8x9.5 | x1280.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1282.4 y15.2 5.6x9.6 | 1.6 |
| hud_unselected | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_4/Shape | x1304.8 y15.2 8.8x9.5 | x1304.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1306.4 y15.2 5.6x9.6 | 1.6 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Por/Box | x22.4 y275.2 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Por/Box | x22.4 y60.8 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Por/Box | x22.4 y221.5 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Por/Box | x22.4 y114.4 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Por/Box | x22.4 y168 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_hit | aspect | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | rect 1 (83.2x83.3) | sprite 0 (1x32), diff 3097.1% | 6194.2 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Paper | x1136.1 y9.5 287.9x46.6 | rect 6.2 (287.9x46.6) | sprite 1 (64x64), diff 518.3% | 1036.5 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Paper | x16 y9.7 223.9x36.7 | rect 6.1 (223.9x36.7) | sprite 1 (64x64), diff 510.5% | 1021 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper | x16 y167.9 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper | x16 y60.8 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper | x16 y114.4 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper | x16 y275.2 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper | x16 y221.5 200x49.7 | rect 4 (200x49.7) | sprite 1 (64x64), diff 302% | 604.1 |
| hud_hit | aspect | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper | x778.4 y9.7 104x36.7 | rect 2.8 (104x36.7) | sprite 1 (64x64), diff 183.4% | 366.9 |
| hud_hit | aspect | GameplayUIBridge_Canvas/HeadshotBadge/Flash | x0 y0 1440x810 | rect 1.8 (1440x810) | sprite 1 (512x512), diff 77.8% | 155.6 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Hatch  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Hatch  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow  ×  Visual/Sub | x125.6 y178 104.8x11.6 | overlap 104.8x11.6 (77.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y156.8 228.7x78.4 · B x125.6 y178 134.6x11.6 | 77.8 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow  ×  Visual/Sub | x134.4 y124.5 96x11.5 | overlap 96x11.5 (76.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y103.1 228.7x78.5 · B x134.4 y124.5 125x11.5 | 76.8 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow  ×  Visual/Sub | x152.8 y285.3 77.6x11.6 | overlap 77.6x11.6 (73.3% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y263.9 228.7x78.5 · B x152.8 y285.3 105.8x11.6 | 73.3 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub | x158.4 y70.9 125x11.6 | x158.4 y70.9 125x11.6 (over by 67.4) | inside parent x16 y60.8 200x49.6 | 67.4 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Hatch  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg  ×  TbRight_Objective/EnemyLabel | x1324.8 y16.1 19.2x9.1 | overlap 19.2x9.1 (66.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1164.9 y13.6 179.2x12.8 · B x1324.8 y16.1 28.8x9.1 | 66.5 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Hatch  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_hit | clipped | PlayerSelection_Canvas/UnitPanel/Head/Kr | x128 y680.5 139.7x12 | preferred 230.9x15 | rect 190x16 | 61.4 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Hatch  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_hit | clipped | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | x27.2 y618.1 83.2x83.3 (over by 59.3) | inside parent x16 y618 343.9x24 | 59.3 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow  ×  Visual/Sub | x158.4 y70.9 72x11.6 | overlap 72x11.6 (57.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y49.5 228.7x78.5 · B x158.4 y70.9 125x11.6 | 57.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | preferred 116.8x31.2 | rect 84x32 | 49.3 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Hatch  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub | x125.6 y178 134.6x11.6 | x125.6 y178 134.6x11.6 (over by 44.2) | inside parent x16 y167.9 200x49.6 | 44.2 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub | x134.4 y124.5 125x11.5 | x134.4 y124.5 125x11.5 (over by 43.4) | inside parent x16 y114.4 200x49.6 | 43.4 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub | x152.8 y285.3 105.8x11.6 | x152.8 y285.3 105.8x11.6 (over by 42.6) | inside parent x16 y275.2 200x49.6 | 42.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | x1104.9 y-13.5 350.3x108.8 (over by 39.2) | inside parent x1136.1 y9.7 287.9x46.4 | 39.2 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | x-15.2 y-13.5 286.3x99 (over by 38.9) | inside parent x16 y9.7 223.9x36.9 | 38.9 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | x526.4 y-13.5 387.1x99 (over by 38.9) | inside parent x557.6 y9.7 324.7x36.9 | 38.9 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Body | x1164.9 y27.9 246.5x24.5 | preferred 308.2x31.5 | rect 290x36 | 27.3 |
| hud_hit | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 48x13.4 | preferred 60x16.7 | rect 44.4x20 | 23.4 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y70.9 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x158.4 y70.9 125x11.6 · B x212.7 y60.8 3.2x49.6 | 23.3 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y285.3 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x152.8 y285.3 105.8x11.6 · B x212.7 y275.2 3.2x49.6 | 23.3 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y178 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x125.6 y178 134.6x11.6 · B x212.7 y167.9 3.2x49.6 | 23.3 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y124.5 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x134.4 y124.5 125x11.5 · B x212.7 y114.4 3.2x49.6 | 23.1 |
| hud_hit | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y231.7 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x137.6 y231.7 86.6x11.5 · B x212.7 y221.5 3.2x49.7 | 23.1 |
| hud_hit | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | 1104.9,-13.5 → 1455.2,95.2 | within 0,0 → 1440,810 | 22.8 |
| hud_hit | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | -15.2,-13.5 → 271.1,85.4 | within 0,0 → 1440,810 | 22.8 |
| hud_hit | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 48x13.4 | x132.8 y714.8 48x13.4 (over by 10.9) | inside parent x128 y701.1 41.9x32.8 | 21.7 |
| hud_hit | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | 526.4,-13.5 → 913.6,85.4 | within 0,0 → 1440,810 | 20.3 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow | x1.6 y156.8 228.7x78.4 | x1.6 y156.8 228.7x78.4 (over by 17.7) | inside parent x16 y167.9 200x49.6 | 17.7 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow | x1.6 y49.5 228.7x78.5 | x1.6 y49.5 228.7x78.5 (over by 17.6) | inside parent x16 y60.8 200x49.6 | 17.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow | x1.6 y263.9 228.7x78.5 | x1.6 y263.9 228.7x78.5 (over by 17.6) | inside parent x16 y275.2 200x49.6 | 17.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow | x1.6 y103.1 228.7x78.5 | x1.6 y103.1 228.7x78.5 (over by 17.6) | inside parent x16 y114.4 200x49.6 | 17.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Shadow | x1.6 y210.3 228.7x78.4 | x1.6 y210.3 228.7x78.4 (over by 17.5) | inside parent x16 y221.5 200x49.7 | 17.5 |
| hud_hit | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1369.8 y785.2 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.6 |
| hud_hit | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y710.5 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | x802.4 y15.2 93.4x25 (over by 13.5) | inside parent x778.4 y9.7 104x36.9 | 13.5 |
| hud_hit | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y775.1 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.5 |
| hud_hit | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1305.4 y785.2 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.5 |
| hud_hit | clipped | GameplayUIBridge_Canvas/HeadshotBadge/Badge/TextUnderline | x554.6 y325.8 183.7x15.3 | x554.6 y325.8 183.7x15.3 (over by 10.5) | inside parent x515.3 y277.9 212.5x86.4 | 10.5 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub | x137.6 y231.7 86.6x11.5 | x137.6 y231.7 86.6x11.5 (over by 8.2) | inside parent x16 y221.5 200x49.7 | 8.2 |
| hud_hit | clipped | PlayerSelection_Canvas/UnitPanel/Head/Code | x128 y647.7 143.7x9.1 | preferred 194.7x11.5 | rect 190x14 | 7.1 |
| hud_hit | overlap | GameplayUIBridge_Canvas/HeadshotBadge/Badge/IconBg  ×  Badge/TextUnderline | x554.6 y325.8 6x15.3 | overlap 6x15.3 (5.5% of the smaller) | none (or add LintIgnoreOverlap) — A x519.6 y306.3 41x41.1 · B x554.6 y325.8 183.7x15.3 | 5.5 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg/Title | x1169.7 y14.2 165.9x10.6 | preferred 207.4x13.2 | rect 212x12 | 1.8 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_3/Shape | x1296.8 y15.2 8.8x9.5 | x1296.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1298.4 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_1/Shape | x1280.8 y15.2 8.8x9.5 | x1280.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1282.4 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_4/Shape | x1389.6 y15.2 8.8x9.5 | x1389.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1391.2 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_3/Shape | x1381.6 y15.2 8.8x9.5 | x1381.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1383.2 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_2/Shape | x1373.6 y15.2 8.8x9.5 | x1373.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1375.2 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_1/Shape | x1365.6 y15.2 8.8x9.5 | x1365.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1367.2 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_0/Shape | x1357.6 y15.2 8.8x9.5 | x1357.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1359.2 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_4/Shape | x1304.8 y15.2 8.8x9.5 | x1304.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1306.4 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_2/Shape | x1288.8 y15.2 8.8x9.5 | x1288.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1290.4 y15.2 5.6x9.6 | 1.6 |
| hud_hit | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_0/Shape | x1272.8 y15.2 8.8x9.5 | x1272.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1274.4 y15.2 5.6x9.6 | 1.6 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Por/Box | x22.4 y275.2 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Por/Box | x22.4 y60.8 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Por/Box | x22.4 y221.5 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Por/Box | x22.4 y168 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Por/Box | x22.4 y114.4 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| hud_ended | aspect | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | rect 1 (83.2x83.3) | sprite 0 (1x32), diff 3097.1% | 6194.2 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Paper | x1136.1 y9.5 287.9x46.6 | rect 6.2 (287.9x46.6) | sprite 1 (64x64), diff 518.3% | 1036.5 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Paper | x16 y9.7 223.9x36.7 | rect 6.1 (223.9x36.7) | sprite 1 (64x64), diff 510.5% | 1021 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper | x16 y60.8 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper | x16 y114.4 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper | x16 y167.9 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper | x16 y275.2 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper | x16 y221.5 200x49.7 | rect 4 (200x49.7) | sprite 1 (64x64), diff 302% | 604.1 |
| hud_ended | aspect | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper | x778.4 y9.7 104x36.7 | rect 2.8 (104x36.7) | sprite 1 (64x64), diff 183.4% | 366.9 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Hatch  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Hatch  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow  ×  Visual/Sub | x125.6 y178 104.8x11.6 | overlap 104.8x11.6 (77.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y156.8 228.7x78.4 · B x125.6 y178 134.6x11.6 | 77.8 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow  ×  Visual/Sub | x134.4 y124.5 96x11.5 | overlap 96x11.5 (76.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y103.1 228.7x78.5 · B x134.4 y124.5 125x11.5 | 76.8 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow  ×  Visual/Sub | x152.8 y285.3 77.6x11.6 | overlap 77.6x11.6 (73.3% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y263.9 228.7x78.5 · B x152.8 y285.3 105.8x11.6 | 73.3 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub | x158.4 y70.9 125x11.6 | x158.4 y70.9 125x11.6 (over by 67.4) | inside parent x16 y60.8 200x49.6 | 67.4 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Hatch  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg  ×  TbRight_Objective/EnemyLabel | x1324.8 y16.1 19.2x9.1 | overlap 19.2x9.1 (66.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1164.9 y13.6 179.2x12.8 · B x1324.8 y16.1 28.8x9.1 | 66.5 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Hatch  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| hud_ended | clipped | PlayerSelection_Canvas/UnitPanel/Head/Kr | x128 y680.5 139.7x12 | preferred 230.9x15 | rect 190x16 | 61.4 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Hatch  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| hud_ended | clipped | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | x27.2 y618.1 83.2x83.3 (over by 59.3) | inside parent x16 y618 343.9x24 | 59.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow  ×  Visual/Sub | x158.4 y70.9 72x11.6 | overlap 72x11.6 (57.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y49.5 228.7x78.5 · B x158.4 y70.9 125x11.6 | 57.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | preferred 116.8x31.2 | rect 84x32 | 49.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Hatch  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub | x125.6 y178 134.6x11.6 | x125.6 y178 134.6x11.6 (over by 44.2) | inside parent x16 y167.9 200x49.6 | 44.2 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub | x134.4 y124.5 125x11.5 | x134.4 y124.5 125x11.5 (over by 43.4) | inside parent x16 y114.4 200x49.6 | 43.4 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub | x152.8 y285.3 105.8x11.6 | x152.8 y285.3 105.8x11.6 (over by 42.6) | inside parent x16 y275.2 200x49.6 | 42.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | x1104.9 y-13.5 350.3x108.8 (over by 39.2) | inside parent x1136.1 y9.7 287.9x46.4 | 39.2 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | x526.4 y-13.5 387.1x99 (over by 38.9) | inside parent x557.6 y9.7 324.7x36.9 | 38.9 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | x-15.2 y-13.5 286.3x99 (over by 38.9) | inside parent x16 y9.7 223.9x36.9 | 38.9 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Body | x1164.9 y27.9 246.5x24.5 | preferred 308.2x31.5 | rect 290x36 | 27.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y285.3 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x152.8 y285.3 105.8x11.6 · B x212.7 y275.2 3.2x49.6 | 23.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y178 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x125.6 y178 134.6x11.6 · B x212.7 y167.9 3.2x49.6 | 23.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y70.9 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x158.4 y70.9 125x11.6 · B x212.7 y60.8 3.2x49.6 | 23.3 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y124.5 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x134.4 y124.5 125x11.5 · B x212.7 y114.4 3.2x49.6 | 23.1 |
| hud_ended | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y231.7 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x137.6 y231.7 86.6x11.5 · B x212.7 y221.5 3.2x49.7 | 23.1 |
| hud_ended | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | -15.2,-13.5 → 271.1,85.4 | within 0,0 → 1440,810 | 22.8 |
| hud_ended | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | 1104.9,-13.5 → 1455.2,95.2 | within 0,0 → 1440,810 | 22.8 |
| hud_ended | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | 526.4,-13.5 → 913.6,85.4 | within 0,0 → 1440,810 | 20.3 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow | x1.6 y156.8 228.7x78.4 | x1.6 y156.8 228.7x78.4 (over by 17.7) | inside parent x16 y167.9 200x49.6 | 17.7 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow | x1.6 y263.9 228.7x78.5 | x1.6 y263.9 228.7x78.5 (over by 17.6) | inside parent x16 y275.2 200x49.6 | 17.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow | x1.6 y49.5 228.7x78.5 | x1.6 y49.5 228.7x78.5 (over by 17.6) | inside parent x16 y60.8 200x49.6 | 17.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow | x1.6 y103.1 228.7x78.5 | x1.6 y103.1 228.7x78.5 (over by 17.6) | inside parent x16 y114.4 200x49.6 | 17.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Shadow | x1.6 y210.3 228.7x78.4 | x1.6 y210.3 228.7x78.4 (over by 17.5) | inside parent x16 y221.5 200x49.7 | 17.5 |
| hud_ended | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1369.8 y785.2 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.6 |
| hud_ended | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y710.5 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | x802.4 y15.2 93.4x25 (over by 13.5) | inside parent x778.4 y9.7 104x36.9 | 13.5 |
| hud_ended | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y775.1 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.5 |
| hud_ended | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1305.4 y785.2 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.5 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub | x137.6 y231.7 86.6x11.5 | x137.6 y231.7 86.6x11.5 (over by 8.2) | inside parent x16 y221.5 200x49.7 | 8.2 |
| hud_ended | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 38.8x13.4 | preferred 48.6x16.7 | rect 44.4x20 | 6.3 |
| hud_ended | clipped | PlayerSelection_Canvas/UnitPanel/Head/Code | x128 y647.7 143.1x9.1 | preferred 194x11.5 | rect 190x14 | 6 |
| hud_ended | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 38.8x13.4 | x132.8 y714.8 38.8x13.4 (over by 1.7) | inside parent x128 y701.1 41.9x32.8 | 3.5 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg/Title | x1169.7 y14.2 165.9x10.6 | preferred 207.4x13.2 | rect 212x12 | 1.8 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_3/Shape | x1381.6 y15.2 8.8x9.5 | x1381.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1383.2 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_2/Shape | x1373.6 y15.2 8.8x9.5 | x1373.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1375.2 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_1/Shape | x1365.6 y15.2 8.8x9.5 | x1365.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1367.2 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_0/Shape | x1357.6 y15.2 8.8x9.5 | x1357.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1359.2 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_4/Shape | x1304.8 y15.2 8.8x9.5 | x1304.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1306.4 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_3/Shape | x1296.8 y15.2 8.8x9.5 | x1296.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1298.4 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_1/Shape | x1280.8 y15.2 8.8x9.5 | x1280.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1282.4 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_0/Shape | x1272.8 y15.2 8.8x9.5 | x1272.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1274.4 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_4/Shape | x1389.6 y15.2 8.8x9.5 | x1389.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1391.2 y15.2 5.6x9.6 | 1.6 |
| hud_ended | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_2/Shape | x1288.8 y15.2 8.8x9.5 | x1288.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1290.4 y15.2 5.6x9.6 | 1.6 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Por/Box | x32 y60.8 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Por/Box | x22.4 y275.2 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Por/Box | x22.4 y221.5 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Por/Box | x22.4 y168 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Por/Box | x22.4 y114.4 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| dialog_move | aspect | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | rect 1 (83.2x83.3) | sprite 0 (1x32), diff 3097.1% | 6194.2 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Paper | x1136.1 y9.5 287.9x46.6 | rect 6.2 (287.9x46.6) | sprite 1 (64x64), diff 518.3% | 1036.5 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Paper | x16 y9.7 223.9x36.7 | rect 6.1 (223.9x36.7) | sprite 1 (64x64), diff 510.5% | 1021 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper | x16 y275.2 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper | x25.6 y60.8 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper | x16 y167.9 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper | x16 y114.4 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper | x16 y221.5 200x49.7 | rect 4 (200x49.7) | sprite 1 (64x64), diff 302% | 604.1 |
| dialog_move | clipped | PlayerSelection_Canvas/MoveHud/Hint/Text/Sub | x647.2 y775.7 168x11.4 | preferred 482.7x14.4 | rect 225x15 | 386.5 |
| dialog_move | aspect | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper | x778.4 y9.7 104x36.7 | rect 2.8 (104x36.7) | sprite 1 (64x64), diff 183.4% | 366.9 |
| dialog_move | aspect | PlayerSelection_Canvas/Dialog/MBox/Pp | x552 y340.6 335.9x128.7 | rect 2.6 (335.9x128.7) | sprite 1 (64x64), diff 160.9% | 321.9 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Hatch  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Hatch  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow  ×  Visual/Sub | x125.6 y178 104.8x11.6 | overlap 104.8x11.6 (77.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y156.8 228.7x78.4 · B x125.6 y178 134.6x11.6 | 77.8 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow  ×  Visual/Sub | x134.4 y124.5 96x11.5 | overlap 96x11.5 (76.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y103.1 228.7x78.5 · B x134.4 y124.5 125x11.5 | 76.8 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow  ×  Visual/Sub | x152.8 y285.3 77.6x11.6 | overlap 77.6x11.6 (73.3% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y263.9 228.7x78.5 · B x152.8 y285.3 105.8x11.6 | 73.3 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub | x168 y70.9 125x11.6 | x168 y70.9 125x11.6 (over by 67.4) | inside parent x25.6 y60.8 200x49.6 | 67.4 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Hatch  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg  ×  TbRight_Objective/EnemyLabel | x1324.8 y16.1 19.2x9.1 | overlap 19.2x9.1 (66.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1164.9 y13.6 179.2x12.8 · B x1324.8 y16.1 28.8x9.1 | 66.5 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Hatch  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| dialog_move | clipped | PlayerSelection_Canvas/UnitPanel/Head/Kr | x128 y680.5 139.7x12 | preferred 230.9x15 | rect 190x16 | 61.4 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Hatch  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| dialog_move | clipped | PlayerSelection_Canvas/UnitPanelOver/Por | x27.2 y618.1 83.2x83.3 | x27.2 y618.1 83.2x83.3 (over by 59.3) | inside parent x16 y618 343.9x24 | 59.3 |
| dialog_move | clipped | PlayerSelection_Canvas/OpArt/Art/Photo | x1154.5 y84.8 299.1x299.1 | x1154.5 y84.8 299.1x299.1 (over by 29.6) | inside parent x1184.1 y84.8 239.9x299.1 | 59.2 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow  ×  Visual/Sub | x168 y70.9 72x11.6 | overlap 72x11.6 (57.6% of the smaller) | none (or add LintIgnoreOverlap) — A x11.2 y49.5 228.7x78.5 · B x168 y70.9 125x11.6 | 57.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | preferred 116.8x31.2 | rect 84x32 | 49.3 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper  ×  Visual/Sub | x168 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x25.6 y60.8 200x49.6 · B x168 y70.9 125x11.6 | 46.1 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Hatch  ×  Visual/Sub | x168 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x25.6 y60.8 200x49.6 · B x168 y70.9 125x11.6 | 46.1 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub | x125.6 y178 134.6x11.6 | x125.6 y178 134.6x11.6 (over by 44.2) | inside parent x16 y167.9 200x49.6 | 44.2 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub | x134.4 y124.5 125x11.5 | x134.4 y124.5 125x11.5 (over by 43.4) | inside parent x16 y114.4 200x49.6 | 43.4 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub | x152.8 y285.3 105.8x11.6 | x152.8 y285.3 105.8x11.6 (over by 42.6) | inside parent x16 y275.2 200x49.6 | 42.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | x1104.9 y-13.5 350.3x108.8 (over by 39.2) | inside parent x1136.1 y9.7 287.9x46.4 | 39.2 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | x-15.2 y-13.5 286.3x99 (over by 38.9) | inside parent x16 y9.7 223.9x36.9 | 38.9 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | x526.4 y-13.5 387.1x99 (over by 38.9) | inside parent x557.6 y9.7 324.7x36.9 | 38.9 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Body | x1164.9 y27.9 246.5x24.5 | preferred 308.2x31.5 | rect 290x36 | 27.3 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub  ×  Visual/Edge | x220.7 y70.9 4.8x11.6 | overlap 4.8x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x168 y70.9 125x11.6 · B x220.7 y60.8 4.8x49.6 | 23.3 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y285.3 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x152.8 y285.3 105.8x11.6 · B x212.7 y275.2 3.2x49.6 | 23.3 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y178 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x125.6 y178 134.6x11.6 · B x212.7 y167.9 3.2x49.6 | 23.3 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y124.5 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x134.4 y124.5 125x11.5 · B x212.7 y114.4 3.2x49.6 | 23.1 |
| dialog_move | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y231.7 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x137.6 y231.7 86.6x11.5 · B x212.7 y221.5 3.2x49.7 | 23.1 |
| dialog_move | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | 1104.9,-13.5 → 1455.2,95.2 | within 0,0 → 1440,810 | 22.8 |
| dialog_move | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | -15.2,-13.5 → 271.1,85.4 | within 0,0 → 1440,810 | 22.8 |
| dialog_move | offscreen | PlayerSelection_Canvas/OpArt/Art/Photo | x1154.5 y84.8 299.1x299.1 | 1154.5,84.8 → 1453.6,383.9 | within 0,0 → 1440,810 | 20.4 |
| dialog_move | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | 526.4,-13.5 → 913.6,85.4 | within 0,0 → 1440,810 | 20.3 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow | x1.6 y156.8 228.7x78.4 | x1.6 y156.8 228.7x78.4 (over by 17.7) | inside parent x16 y167.9 200x49.6 | 17.7 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow | x1.6 y103.1 228.7x78.5 | x1.6 y103.1 228.7x78.5 (over by 17.6) | inside parent x16 y114.4 200x49.6 | 17.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow | x11.2 y49.5 228.7x78.5 | x11.2 y49.5 228.7x78.5 (over by 17.6) | inside parent x25.6 y60.8 200x49.6 | 17.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow | x1.6 y263.9 228.7x78.5 | x1.6 y263.9 228.7x78.5 (over by 17.6) | inside parent x16 y275.2 200x49.6 | 17.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Shadow | x1.6 y210.3 228.7x78.4 | x1.6 y210.3 228.7x78.4 (over by 17.5) | inside parent x16 y221.5 200x49.7 | 17.5 |
| dialog_move | aspect | PlayerSelection_Canvas/MoveHud/Hint/Stripe | x612 y755.7 24x38.4 | rect 0.6 (24x38.4) | sprite 0.7 (30x44), diff 8.3% | 16.5 |
| dialog_move | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y710.5 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.6 |
| dialog_move | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1369.8 y785.2 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.6 |
| dialog_move | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1305.4 y785.2 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.5 |
| dialog_move | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y775.1 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.5 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | x802.4 y15.2 93.4x25 (over by 13.5) | inside parent x778.4 y9.7 104x36.9 | 13.5 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/SelArrow | x16 y80.9 4.8x9.5 | x16 y80.9 4.8x9.5 (over by 9.6) | inside parent x25.6 y60.8 200x49.6 | 9.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub | x137.6 y231.7 86.6x11.5 | x137.6 y231.7 86.6x11.5 (over by 8.2) | inside parent x16 y221.5 200x49.7 | 8.2 |
| dialog_move | overlap | PlayerSelection_Canvas/MoveHud/Hint/Stripe  ×  Hint/BorderBottom | x612 y791.7 24x2.4 | overlap 24x2.4 (7.5% of the smaller) | none (or add LintIgnoreOverlap) — A x612 y755.7 24x38.4 · B x612 y791.7 319.9x2.4 | 7.5 |
| dialog_move | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 38.8x13.4 | preferred 48.6x16.7 | rect 44.4x20 | 6.3 |
| dialog_move | clipped | PlayerSelection_Canvas/UnitPanel/Head/Code | x128 y647.7 143.1x9.1 | preferred 194x11.5 | rect 190x14 | 6 |
| dialog_move | clipped | PlayerSelection_Canvas/UnitPanel/Grid/MOVE/Value | x132.8 y714.8 38.8x13.4 | x132.8 y714.8 38.8x13.4 (over by 1.7) | inside parent x128 y701.1 41.9x32.8 | 3.5 |
| dialog_move | clipped | PlayerSelection_Canvas/OpArt/NameText/Text | x1198.5 y396.2 125.2x28.3 | preferred 156.6x35.4 | rect 264x34 | 2.1 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg/Title | x1169.7 y14.2 165.9x10.6 | preferred 207.4x13.2 | rect 212x12 | 1.8 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_3/Shape | x1381.6 y15.2 8.8x9.5 | x1381.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1383.2 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_2/Shape | x1373.6 y15.2 8.8x9.5 | x1373.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1375.2 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_1/Shape | x1365.6 y15.2 8.8x9.5 | x1365.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1367.2 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_0/Shape | x1357.6 y15.2 8.8x9.5 | x1357.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1359.2 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_4/Shape | x1304.8 y15.2 8.8x9.5 | x1304.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1306.4 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_3/Shape | x1296.8 y15.2 8.8x9.5 | x1296.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1298.4 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_1/Shape | x1280.8 y15.2 8.8x9.5 | x1280.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1282.4 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_0/Shape | x1272.8 y15.2 8.8x9.5 | x1272.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1274.4 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_4/Shape | x1389.6 y15.2 8.8x9.5 | x1389.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1391.2 y15.2 5.6x9.6 | 1.6 |
| dialog_move | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_2/Shape | x1288.8 y15.2 8.8x9.5 | x1288.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1290.4 y15.2 5.6x9.6 | 1.6 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Por/Box | x22.4 y60.8 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Por/Box | x22.4 y275.2 49.6x49.4 | rect 1 (49.6x49.4) | sprite 0 (1x32), diff 3109.7% | 6219.3 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Por/Box | x22.4 y114.4 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Por/Box | x22.4 y221.5 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Por/Box | x22.4 y168 49.6x49.5 | rect 1 (49.6x49.5) | sprite 0 (1x32), diff 3103.3% | 6206.5 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Paper | x1136.1 y9.5 287.9x46.6 | rect 6.2 (287.9x46.6) | sprite 1 (64x64), diff 518.3% | 1036.5 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Paper | x16 y9.7 223.9x36.7 | rect 6.1 (223.9x36.7) | sprite 1 (64x64), diff 510.5% | 1021 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper | x16 y275.2 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper | x16 y60.8 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper | x16 y167.9 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper | x16 y114.4 200x49.6 | rect 4 (200x49.6) | sprite 1 (64x64), diff 302.8% | 605.7 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper | x16 y221.5 200x49.7 | rect 4 (200x49.7) | sprite 1 (64x64), diff 302% | 604.1 |
| phase_banner | aspect | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper | x778.4 y9.7 104x36.7 | rect 2.8 (104x36.7) | sprite 1 (64x64), diff 183.4% | 366.9 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Hatch  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Paper  ×  Visual/Sub | x137.6 y231.7 78.4x11.5 | overlap 78.4x11.5 (90.5% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y221.5 200x49.7 · B x137.6 y231.7 86.6x11.5 | 90.5 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Hatch  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Paper  ×  RoundBox/Round | x802.4 y15.2 80x25 | overlap 80x25 (85.6% of the smaller) | none (or add LintIgnoreOverlap) — A x778.4 y9.7 104x36.7 · B x802.4 y15.2 93.4x25 | 85.6 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow  ×  Visual/Sub | x125.6 y178 104.8x11.6 | overlap 104.8x11.6 (77.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y156.8 228.7x78.4 · B x125.6 y178 134.6x11.6 | 77.8 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow  ×  Visual/Sub | x134.4 y124.5 96x11.5 | overlap 96x11.5 (76.8% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y103.1 228.7x78.5 · B x134.4 y124.5 125x11.5 | 76.8 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow  ×  Visual/Sub | x152.8 y285.3 77.6x11.6 | overlap 77.6x11.6 (73.3% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y263.9 228.7x78.5 · B x152.8 y285.3 105.8x11.6 | 73.3 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub | x158.4 y70.9 125x11.6 | x158.4 y70.9 125x11.6 (over by 67.4) | inside parent x16 y60.8 200x49.6 | 67.4 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Paper  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Hatch  ×  Visual/Sub | x125.6 y178 90.4x11.6 | overlap 90.4x11.6 (67.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y167.9 200x49.6 · B x125.6 y178 134.6x11.6 | 67.1 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg  ×  TbRight_Objective/EnemyLabel | x1324.8 y16.1 19.2x9.1 | overlap 19.2x9.1 (66.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1164.9 y13.6 179.2x12.8 · B x1324.8 y16.1 28.8x9.1 | 66.5 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Paper  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Hatch  ×  Visual/Sub | x134.4 y124.5 81.6x11.5 | overlap 81.6x11.5 (65.3% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y114.4 200x49.6 · B x134.4 y124.5 125x11.5 | 65.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Paper  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Hatch  ×  Visual/Sub | x152.8 y285.3 63.2x11.6 | overlap 63.2x11.6 (59.7% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y275.2 200x49.6 · B x152.8 y285.3 105.8x11.6 | 59.7 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow  ×  Visual/Sub | x158.4 y70.9 72x11.6 | overlap 72x11.6 (57.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1.6 y49.5 228.7x78.5 · B x158.4 y70.9 125x11.6 | 57.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | preferred 116.8x31.2 | rect 84x32 | 49.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Hatch  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Paper  ×  Visual/Sub | x158.4 y70.9 57.6x11.6 | overlap 57.6x11.6 (46.1% of the smaller) | none (or add LintIgnoreOverlap) — A x16 y60.8 200x49.6 · B x158.4 y70.9 125x11.6 | 46.1 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub | x125.6 y178 134.6x11.6 | x125.6 y178 134.6x11.6 (over by 44.2) | inside parent x16 y167.9 200x49.6 | 44.2 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub | x134.4 y124.5 125x11.5 | x134.4 y124.5 125x11.5 (over by 43.4) | inside parent x16 y114.4 200x49.6 | 43.4 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub | x152.8 y285.3 105.8x11.6 | x152.8 y285.3 105.8x11.6 (over by 42.6) | inside parent x16 y275.2 200x49.6 | 42.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | x1104.9 y-13.5 350.3x108.8 (over by 39.2) | inside parent x1136.1 y9.7 287.9x46.4 | 39.2 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | x526.4 y-13.5 387.1x99 (over by 38.9) | inside parent x557.6 y9.7 324.7x36.9 | 38.9 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | x-15.2 y-13.5 286.3x99 (over by 38.9) | inside parent x16 y9.7 223.9x36.9 | 38.9 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Body | x1164.9 y27.9 246.5x24.5 | preferred 308.2x31.5 | rect 290x36 | 27.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y285.3 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x152.8 y285.3 105.8x11.6 · B x212.7 y275.2 3.2x49.6 | 23.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y70.9 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x158.4 y70.9 125x11.6 · B x212.7 y60.8 3.2x49.6 | 23.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y178 3.2x11.6 | overlap 3.2x11.6 (23.3% of the smaller) | none (or add LintIgnoreOverlap) — A x125.6 y178 134.6x11.6 · B x212.7 y167.9 3.2x49.6 | 23.3 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y124.5 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x134.4 y124.5 125x11.5 · B x212.7 y114.4 3.2x49.6 | 23.1 |
| phase_banner | overlap | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub  ×  Visual/Edge | x212.7 y231.7 3.2x11.5 | overlap 3.2x11.5 (23.1% of the smaller) | none (or add LintIgnoreOverlap) — A x137.6 y231.7 86.6x11.5 · B x212.7 y221.5 3.2x49.7 | 23.1 |
| phase_banner | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbLeft/Shadow | x-15.2 y-13.5 286.3x99 | -15.2,-13.5 → 271.1,85.4 | within 0,0 → 1440,810 | 22.8 |
| phase_banner | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/Shadow | x1104.9 y-13.5 350.3x108.8 | 1104.9,-13.5 → 1455.2,95.2 | within 0,0 → 1440,810 | 22.8 |
| phase_banner | offscreen | BlindsideHUD/HUD/HUD_Topbar/TbCenter/Shadow | x526.4 y-13.5 387.1x99 | 526.4,-13.5 → 913.6,85.4 | within 0,0 → 1440,810 | 20.3 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_2/Inner/Visual/Shadow | x1.6 y156.8 228.7x78.4 | x1.6 y156.8 228.7x78.4 (over by 17.7) | inside parent x16 y167.9 200x49.6 | 17.7 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_0/Inner/Visual/Shadow | x1.6 y49.5 228.7x78.5 | x1.6 y49.5 228.7x78.5 (over by 17.6) | inside parent x16 y60.8 200x49.6 | 17.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_4/Inner/Visual/Shadow | x1.6 y263.9 228.7x78.5 | x1.6 y263.9 228.7x78.5 (over by 17.6) | inside parent x16 y275.2 200x49.6 | 17.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_1/Inner/Visual/Shadow | x1.6 y103.1 228.7x78.5 | x1.6 y103.1 228.7x78.5 (over by 17.6) | inside parent x16 y114.4 200x49.6 | 17.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Shadow | x1.6 y210.3 228.7x78.4 | x1.6 y210.3 228.7x78.4 (over by 17.5) | inside parent x16 y221.5 200x49.7 | 17.5 |
| phase_banner | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y710.5 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.6 |
| phase_banner | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1369.8 y785.2 10.2x74.7 | overlap 10.2x74.7 (13.6% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbCenter/RoundBox/Round | x802.4 y15.2 93.4x25 | x802.4 y15.2 93.4x25 (over by 13.5) | inside parent x778.4 y9.7 104x36.9 | 13.5 |
| phase_banner | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1305.4 y785.2 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1295.2 y700.3 95x94.9 · B x1305.4 y785.2 74.7x74.7 | 13.5 |
| phase_banner | overlap | GameplayUIBridge_Canvas/EndTurn/Button/Deco/Edge  ×  Deco/Edge | x1380 y775.1 74.7x10.1 | overlap 74.7x10.1 (13.5% of the smaller) | none (or add LintIgnoreOverlap) — A x1369.8 y775.1 95x94.9 · B x1380 y710.5 74.7x74.7 | 13.5 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Roster/Cards/Card_3/Inner/Visual/Sub | x137.6 y231.7 86.6x11.5 | x137.6 y231.7 86.6x11.5 (over by 8.2) | inside parent x16 y221.5 200x49.7 | 8.2 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/TitleBg/Title | x1169.7 y14.2 165.9x10.6 | preferred 207.4x13.2 | rect 212x12 | 1.8 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_2/Shape | x1288.8 y15.2 8.8x9.5 | x1288.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1290.4 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_3/Shape | x1296.8 y15.2 8.8x9.5 | x1296.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1298.4 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_0/Shape | x1272.8 y15.2 8.8x9.5 | x1272.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1274.4 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_0/Shape | x1357.6 y15.2 8.8x9.5 | x1357.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1359.2 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_1/Shape | x1365.6 y15.2 8.8x9.5 | x1365.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1367.2 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_2/Shape | x1373.6 y15.2 8.8x9.5 | x1373.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1375.2 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_3/Shape | x1381.6 y15.2 8.8x9.5 | x1381.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1383.2 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/EnemyPips/Pip_4/Shape | x1389.6 y15.2 8.8x9.5 | x1389.6 y15.2 8.8x9.5 (over by 1.6) | inside parent x1391.2 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_1/Shape | x1280.8 y15.2 8.8x9.5 | x1280.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1282.4 y15.2 5.6x9.6 | 1.6 |
| phase_banner | clipped | BlindsideHUD/HUD/HUD_Topbar/TbRight_Objective/AllyPips/Pip_4/Shape | x1304.8 y15.2 8.8x9.5 | x1304.8 y15.2 8.8x9.5 (over by 1.6) | inside parent x1306.4 y15.2 5.6x9.6 | 1.6 |
