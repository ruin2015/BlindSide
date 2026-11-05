# UI spec — target sizes for Blindside > UI > Lint

Sizes are canvas units: the menu canvas at UITheme.menuReference (1440×810), the HUD canvases at UITheme.hudReference.
Values come from the mockup (blindside_ui_kit/ui_target.html) through UITheme.title / UITheme.hud.

`element` is matched against the end of each element's hierarchy path (canvas name first), one `/`-separated segment at a time:
`*` matches any characters inside a segment, `**` matches any number of segments. An empty `w` or `h` is not checked.
The lint reports a difference of 5% or more; rows that match no element in any preview state are listed separately.

| element | w | h | source |
|---|---|---|---|
| TitleSelect/Right | 696 | | .tr width |
| TitleSelect/Left | 600 | | .tl width |
| Left/LogoBar | 400 | 10 | .lbar |
| SideCards/Side_* | 343 | 176 | title.sideCard (.sd) |
| MapRow/MapCard | 520 | 74 | title.mapCard (.mp) |
| MapCard/Preview | 92 | 70 | .mp .pic |
| MapRow/MapEditorButton | 166 | 74 | title.editorButton (.ed) |
| WinStartRow/WinBox | 343 | 106 | title.winCondition (.wb) |
| WinStartRow/StartButton | 343 | 106 | title.startButton (.start) |
| StartButton/Arrow | 86 | 106 | .start .ar |
| Right/SquadHeader | 696 | 32 | .sqh |
| SquadList/* | 696 | 60 | title.squadRow (.sq) |
| SquadList/*/Badge | 72 | 60 | title.squadFace (.sq .por) |
| SquadList/*/Stats | 196 | 60 | title.squadStats (.sq .s) |
| TitleSelect/Spotlight | 474 | 330 | title.spotlight (#spot) |
| HUD_Topbar/TbLeft | 280 | 46 | hud.logoWidth × topbarHeight |
| TbCenter/PhaseBadge | 290 | 46 | hud.phaseWidth |
| TbCenter/RoundBox | 130 | 46 | hud.roundWidth |
| HUD_Topbar/TbRight_Objective | 360 | 58 | hud.objective |
| Cards/Card_* | 250 | 62 | hud.rosterCard (.rc) |
| Card_*/**/Por | 62 | 62 | hud.rosterFace (.rc .por) |
| HUD/HUD_Log | 400 | | hud.logWidth |
| HUD_Log/**/Entry | 400 | 24 | hud.logLineHeight |
| CmdMenu/Cmd_* | 226 | 40 | hud.commandButton (#cmd button) |
| PlayerSelection_Canvas/UnitPanel | 430 | 190 | hud.unitPanel (#up) |
| UnitPanel/Head | 430 | 68 | #up .hd |
| Head/Lv | 92 | 68 | #up .hp |
| UnitPanelOver/Por | 104 | 104 | hud.unitPanelFace (#up .pt) |
| PlayerSelection_Canvas/OpArt | 300 | 500 | hud.artCard (#oc) |
| OpArt/**/FileTag | 300 | 30 | hud.artCardTag (#oc .tag) |
| OpArt/**/ColorBar | 300 | 5 | #oc .nm border |
| GameplayUIBridge_Canvas/EndTurn | 300 | 56 | hud.endTurn (#end) |
| EndTurn/Button/Arrow | 60 | 56 | #end .a |
| MoveHud/Hint | 400 | 48 | hud.hint |
| GameplayUIBridge_Canvas/PhaseBanner | | 140 | hud.bannerHeight |
| Dialog/MBox | 420 | | hud.dialogWidth |
| MBox/**/Mh | 420 | 46 | .mbox .mh |
| MBox/**/Mbtns | 420 | 52 | .mbox .mbtns |

## ignore

Intended exceptions: `rules` is a comma-separated list of rule ids (OFFSCREEN, TEXT_OVERFLOW, TEXT_OVERLAP, CLIPPED, ASPECT,
SIBLING_OVERLAP, LAYOUT_FORCED, FONT_MIN, SPEC, TEXT_TRUNCATED, OUTSIDE_PARENT) or `*` for all. Elements carrying a LintIgnoreOverlap
component are excused in code instead (SIBLING_OVERLAP / TEXT_OVERLAP; with `bounds`, also OUTSIDE_PARENT / CLIPPED / OFFSCREEN).

| element | rules | reason |
|---|---|---|
| **/Shadow | OUTSIDE_PARENT, SIBLING_OVERLAP, OFFSCREEN | box-shadow falloff, wider than its box by design |
| **/SelArrow | OUTSIDE_PARENT | .rcard.sel::before arrow sticks out of the card |
| HUD/HUD_Topbar/TbCenter/RoundBox | SIBLING_OVERLAP | #roundBox{margin-left:-14px} tucks under the phase badge |
| **/Face | CLIPPED | the face is cover-fitted (top pinned) and cropped by its slot |
| OpArt/Art/Photo | CLIPPED | the illustration is cover-fitted and cropped by the art window |
| **/Paper | ASPECT | the paper gradient is a stretched 1-D ramp |
