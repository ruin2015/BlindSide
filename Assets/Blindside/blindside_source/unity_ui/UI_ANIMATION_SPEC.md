# BLINDSIDE — UI 애니메이션 유니티 이식 명세

웹 버전의 CSS 애니메이션을 유니티 uGUI로 옮기기 위한 명세입니다. 함께 있는 `BlindsideUIAnim.cs`가 아래 동작을 모두 구현하므로, 유니티 MCP가 연결된 Claude에게는 "이 명세와 스크립트를 기준으로 UI 프리팹을 만들고 컴포넌트를 붙여 달라"고 요청하면 됩니다.

## 공통 규칙

- **기본 이징:** CSS의 `cubic-bezier(.2,.9,.2,1)`(빠르게 튀어나와 부드럽게 멈춤)입니다. 스크립트에서는 `UIEase.Tactical`(1 − (1−t)⁴)로 근사했습니다.
- **UI 시간:** 모든 UI 애니메이션은 `Time.unscaledDeltaTime`으로 돌립니다. 헤드샷 히트스톱이 `Time.timeScale = 0`을 걸어도 UI는 계속 움직여야 하기 때문입니다.
- **`steps(n)` 애니메이션:** 뚝뚝 끊기는 연출(깜빡임, 글리치)은 보간하지 않고 값을 즉시 바꿉니다.
- **clip-path 리빌:** 왼쪽→오른쪽으로 드러나는 연출은 `RectMask2D`의 `padding` 값을 줄여서 구현합니다.
- **사선 컷 모서리:** 모서리가 잘린 패널은 9-slice 스프라이트로 처리합니다. 오른쪽 위 모서리를 8~14px 잘라낸 흰 판 이미지를 쓰면 됩니다.

### 색상 토큰

| 토큰 | 값 | 용도 |
|---|---|---|
| paper | `#ECEEF0` | 밝은 금속판 패널 |
| ink | `#14161A` | 먹색 블록, 헤더 |
| ink2 | `#23272C` | 보조 먹색 |
| blu | `#1F96E8` | 아군, 주요 버튼 |
| foe | `#E5364D` | 적, 경고 |
| acc | `#FFC81E` | 노란 강조, 해저드 |
| dimp | `#6A7078` | 밝은 패널 위 보조 글자 |

### 폰트

| 용도 | 폰트 |
|---|---|
| 영문 제목 | Chakra Petch Bold |
| 한글 | Noto Sans KR (Bold / Black) |
| 코드 라벨 | JetBrains Mono |

세 폰트 모두 TextMeshPro 폰트 에셋으로 만들어 두면 됩니다.

## 애니메이션 목록

| 이름 | 쓰이는 곳 | 시간 / 지연 | 동작 | 스크립트 |
|---|---|---|---|---|
| dropIn | 상단 바 3블록 | 0.5s, 지연 0 / 0.08 / 0.16 | 위로 24px에서 내려오며 페이드인 | `UIAnim.SlideFade(offset (0,24))` |
| slideInL | 왼쪽 대원 목록 카드 | 0.45s, 카드마다 60ms 간격 | 왼쪽 40px에서 페이드인 | `UIStaggerIn` (방향 왼쪽) |
| slideInR | 타이틀 투입 대원, 카메라 패널 | 0.35~0.45s | 오른쪽 40px에서 페이드인 | `UIStaggerIn` (방향 오른쪽) |
| panelIn | 대원 상세 패널(교체 시), 확인 창, 결과 화면 | 0.3~0.5s | 왼쪽부터 오른쪽으로 드러남 + 투명도 0.4→1 | `UIAnim.ClipReveal` |
| cmdIn | 명령 메뉴 버튼 | 0.3s, 버튼마다 35ms 간격 | 왼쪽 18px에서 미끄러져 나타남 | `UIStaggerIn` (간격 0.035) |
| hintIn | 하단 안내 바 | 0.3s | 아래 20px에서 올라옴 | `UIAnim.SlideFade(offset (0,-20))` |
| logIn | 전투 로그 줄 | 0.3s | 오른쪽 20px에서 나타남 | `UIAnim.SlideFade` |
| blink | REC 표시, 적 행동 중, 안내 바 표식 | 1s 주기, 2단계 | 투명도 1 ↔ 0.35 뚝뚝 | `UIBlink` |
| pulse | 턴 종료 버튼(전원 행동 완료 시) | 1.2s 반복 | 파란 외곽 빛이 0→12px 퍼지며 사라짐 | `UIPulse` |
| stripes | 작전 개시 버튼 하단 노란 점선, 경고 줄무늬 | 1.2~1.4s 반복 | 줄무늬 텍스처가 옆으로 흐름 | `UIStripeScroll`(RawImage uvRect) |
| bannerIn | 페이즈 배너 (ATTACKER PHASE 등) | 1.7s | 0~14%: 가운데에서 좌우로 펼쳐짐 → 82%까지 유지 → 위아래로 접히며 사라짐 | `UIPhaseBanner` |
| textSlide | 배너 큰 글자, 결과 제목 | 1~1.7s | 왼쪽 60px + 자간 넓게 → 20% 지점에 제자리 | `UIPhaseBanner`에 포함 |
| contactIn | "CONTACT 적과 조우했습니다!" | 1.1s | 1.3배→1배로 박히듯 등장, 85%까지 유지 후 위로 사라짐 | `UIContactAlert` |
| shakeX | 조우 경고의 ! 마크 | 0.4s × 2회 | 좌우 ±4px 흔들림 | `UIAnim.Shake` |
| glitch | CONTACT 글자 | 0.5s × 2회 | 파랑/빨강 잔상이 ±3px 좌우로 튐 | `UIGlitchText` (단발 모드) |
| combatIn | 교전 컷인 | 0.35s | 아래 30px에서 올라옴 | `UIAnim.SlideFade` |
| critPulse | 헤드샷 순간의 교전 컷인 가운데 칸 | 0.5s | 흰색 → 빨강 → 먹색 | `UIAnim.ColorFlash` |
| artIn | 대원 일러스트 카드 | 0.6s | 오른쪽부터 드러남 + 밝게 번쩍 → 좌우 흔들리며 정지 | `UIOperatorArtCard` |
| hsIn | HEADSHOT! 배지 | 1.5s | 2.4배 + 회전 −10°에서 쾅 박힘 → 0.92 → 1.06 → 1, 이후 떠 있다가 1.15s부터 0.35s 페이드아웃 | `UIHeadshotBadge` |
| hsSpin | 배지 조준경 아이콘 | 0.6s | −120° + 1.6배에서 제자리로 회전 | `UIHeadshotBadge`에 포함 |
| hsFlash | 헤드샷 화면 플래시 | 0.45s | 대상 위치 중심 방사형 집중선 + 흰→빨강 플래시 후 사라짐 | `UIHeadshotBadge.Flash` |
| glA / glB | 작전명, 날짜, HEADSHOT 글자 | 1.1~2.6s 반복 | 빨강·파랑 복제 글자가 가끔 잘린 띠 모양으로 ±5px 튐 | `UIGlitchText` (반복 모드) |
| glIn / glOut | 인트로 작전명 타이틀 | 0.9s / 0.7s | 기울어지며 깜빡이다 정착 / 찌그러지며 사라짐 | `UIGlitchText.PlayIn/PlayOut` |
| barIn | 인트로 레터박스 | 0.7s | 높이 0 → 11% | `UIAnim.Height` |
| typeIn | 인트로 자막 큰 글자 | 1.2s, 30단계 | 한 글자씩 타이핑 | `UITypewriter`(TMP maxVisibleCharacters) |
| capIn | 인트로 자막 태그/보조문 | 0.5~0.6s | 왼쪽 24px에서 나타남 | `UIAnim.SlideFade` |
| scanY | 화면 스캔 라인 | 9s 반복 | 옅은 파란 띠가 위→아래로 지나감 | `UIScanLine` |
| bounce | 유닛 머리 위 ! | 0.32s 왕복 | 위로 6px 통통 | `UIBounce` |
| logoIn / growX | 타이틀 로고 글자, 밑줄 | 0.7s(글자마다 45ms) / 0.9s | 아래에서 올라옴 / 폭 0→100% | `UIStaggerIn` / `UIAnim.Width` |

## 게임 이벤트와 연출 연결

| 게임 이벤트 | 재생할 연출 |
|---|---|
| 페이즈 시작 | `UIPhaseBanner.Play(영문, 한글, 부제, 색)` 1.7초. 아군 페이즈는 blu, 적 페이즈는 foe, 준비·배치 단계는 acc |
| 아군 선택 | 대원 상세 패널 `ClipReveal` + 일러스트 카드 `artIn` + 명령 메뉴 `UIStaggerIn` |
| 적 조우 | 카메라 이동 → `UIContactAlert` 1.1초. 테두리 색: 내가 먼저 발견 blu, 상호 조우 acc, 기습당함 foe |
| 사격 | 교전 컷인 `combatIn` → 발마다 체력바 줄어듦(0.5s) → 결과 문구 |
| 헤드샷 | 배지 + 화면 플래시 → `HitStop.Do(0.15f)` → 카메라 흔들림 0.32 + 줌 펀치(높이 −7% 후 0.32초 복귀) → 교전 컷인 `critPulse` |
| 전원 행동 완료 | 턴 종료 버튼 `UIPulse` 시작 |
| 인트로 | 레터박스 `barIn` → 작전명 `PlayIn`(1.7초 유지) → `PlayOut` → 자막 `typeIn` → 날짜·시각·REC 타임코드 갱신 |
