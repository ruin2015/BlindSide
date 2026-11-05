# UI Lint report (v2, live)

Screen 1920x1080 · 4 issues · generated 2026-10-04 00:40

Severity (for ordering only): clipped = px over (text ×1.5, masked parent ×2) · offscreen = px × 1.5 · overlap = % of the smaller rect · aspect = diff% × 2 · fontsize = (11 − size) × 10 · size = diff% × 2 · layout = 10 (Control Child Size) / diff% × 2 / 5 (ContentSizeFitter).

## Unique causes (4 groups, top 4 by max severity)

Operator names and card numbers are normalised to OP / N before grouping.

| # | 종류 | 요소명(정규화) | 건수 | 상태 수 | 최대 심각도 | 대표 측정값 | 대표 기대값 |
|---|---|---|---|---|---|---|---|
| 1 | offscreen | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel | 1 | 1 | 395.8 | 1548.2,1327.6 → 1587.8,1343.8 | within 0,0 → 1920,1080 |
| 2 | offscreen | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel/Border | 1 | 1 | 395.8 | 1548.2,1327.6 → 1551.4,1343.8 | within 0,0 → 1920,1080 |
| 3 | offscreen | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel/Text | 1 | 1 | 394.2 | 1556.7,1328.7 → 1582.5,1342.8 | within 0,0 → 1920,1080 |
| 4 | clipped | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel | 1 | 1 | 263.8 | x1548.2 y1327.6 39.6x16.2 (over by 263.8) | inside parent x0 y0 1920x1080 |

## Count by canvas

| 상태 | 건수 | clipped | overlap | offscreen | aspect | fontsize | size | layout |
|---|---|---|---|---|---|---|---|---|
| BlindsideHUD | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| BlindsideCanvas | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| GameplayUIBridge_Canvas | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Marks_Canvas | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| PlayerSelection_Canvas | 4 | 1 | 0 | 3 | 0 | 0 | 0 | 0 |
| BattleDirector_Canvas | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| ScreenFXCanvas | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **합계** | **4** | **1** | **0** | **3** | **0** | **0** | **0** | **0** |

## Issues (all)

| 상태 | 종류 | 요소명 | 위치 | 측정값 | 기대값 | 심각도 |
|---|---|---|---|---|---|---|
| PlayerSelection_Canvas | offscreen | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel | x1548.2 y1327.6 39.6x16.2 | 1548.2,1327.6 → 1587.8,1343.8 | within 0,0 → 1920,1080 | 395.8 |
| PlayerSelection_Canvas | offscreen | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel/Border | x1548.2 y1327.6 3.2x16.2 | 1548.2,1327.6 → 1551.4,1343.8 | within 0,0 → 1920,1080 | 395.8 |
| PlayerSelection_Canvas | offscreen | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel/Text | x1556.7 y1328.7 25.8x14.1 | 1556.7,1328.7 → 1582.5,1342.8 | within 0,0 → 1920,1080 | 394.2 |
| PlayerSelection_Canvas | clipped | MatchBootstrap/PlayerSelection_Canvas/MoveHud/EndedTags/HostageLabel | x1548.2 y1327.6 39.6x16.2 | x1548.2 y1327.6 39.6x16.2 (over by 263.8) | inside parent x0 y0 1920x1080 | 263.8 |

## 미매칭 목록 (LintSpec 표에 없는 Blindside 컴포넌트, 27종)

- Chip ×12
- CornerBrackets ×1
- CutCornerGraphic ×3
- HazardStripe ×5
- HudController ×1
- IntroCaption ×1
- KitBannerStrip ×1
- KitCommandMenu ×1
- KitRoster ×1
- KitShadow ×3
- KitSweep ×8
- KitTint ×7
- LetterboxBars ×1
- LintIgnoreClip ×13
- SegmentedBar ×5
- SlantedGraphic ×59
- SlantedMask ×6
- TacticalPanel ×33
- UIContactAlert ×1
- UIGlitchImage ×7
- UIGlitchName ×2
- UIGlitchText ×5
- UIHeadshotBadge ×1
- UIPhaseBanner ×1
- UIPulse ×1
- UIScreenFX ×1
- UIThemeCanvas ×3
