# 유니티 MCP가 연결된 Claude에게 줄 작업 지시서

아래 내용을 유니티 MCP가 연결된 Claude(Claude Desktop 또는 Claude Code)에 그대로 붙여 넣고, 함께 있는 파일들을 첨부하거나 프로젝트 폴더에 넣어 두세요.

---

유니티 프로젝트에 웹 게임 "BLINDSIDE"의 UI와 UI 애니메이션을 이식해 줘. 참고 파일은 다음과 같아.

- `UI_ANIMATION_SPEC.md` — 모든 UI 애니메이션의 시간, 동작, 게임 이벤트 연결 규칙
- `BlindsideUIAnim.cs` — 애니메이션을 구현한 컴포넌트 모음 (namespace `Blindside.UI`)
- `00_markup_and_styles.html` — 원본 화면 구조와 CSS (배치, 크기, 색의 기준)
- `05_ui_input_intro.js` — 원본 UI 로직 (어떤 순서로 무엇을 띄우는지)
- `UNITY_PORTING_GUIDE.md` — 게임 전체 구조와 수치

## 진행 순서

작업은 단계마다 멈추고 결과를 보여 줘.

1. **준비**
   - TextMeshPro가 설치되어 있는지 확인해 줘.
   - `Assets/Blindside/Scripts/UI/`에 `BlindsideUIAnim.cs`를 넣고 컴파일 오류가 없는지 확인해 줘.
   - 폰트 Chakra Petch, Noto Sans KR, JetBrains Mono로 TMP 폰트 에셋을 만들어 줘. 폰트 파일이 없으면 나에게 알려 줘.
2. **캔버스와 공통 스프라이트**
   - Screen Space Overlay 캔버스를 만들어 줘. 기준 해상도는 1920×1080, Scale With Screen Size, Match 0.5.
   - 스프라이트 두 장을 만들어 줘: 오른쪽 위 모서리를 12px 잘라낸 흰 9-slice 패널, 해저드 줄무늬 타일 텍스처.
   - 색은 명세의 색상 토큰을 그대로 써 줘.
3. **HUD 프리팹**
   - 원본 CSS의 위치와 크기를 기준으로 만들어 줘: 상단 바(로고, 페이즈 배지, 라운드, 목표), 왼쪽 대원 목록 카드, 하단 대원 상세 패널, 명령 메뉴, 하단 안내 바, 전투 로그, 턴 종료 버튼, 화면 테두리 장식(모서리 꺾쇠, 눈금자, 스캔 라인).
   - 각 요소에는 명세의 애니메이션 컴포넌트를 붙여 줘. 예시:
     - 명령 메뉴 → `UIStaggerIn` (방향 −18,0 / 간격 0.035)
     - 턴 종료 → `UIPulse`
     - 줄무늬 → `UIStripeScroll`
4. **오버레이 프리팹**
   - 페이즈 배너(`UIPhaseBanner`), 조우 경고(`UIContactAlert` + `UIGlitchText`), 확인 창, 교전 컷인, 대원 일러스트 카드(`UIOperatorArtCard`), 헤드샷 배지(`UIHeadshotBadge`), 결과 화면을 만들어 줘.
5. **인트로 UI**
   - 레터박스, 작전명 글리치 타이틀(`UIGlitchText.PlayIn/PlayOut`), 날짜·시각·REC 타임코드, 자막(`UITypewriter`), 페이드를 만들어 줘.
6. **테스트 씬**
   - 버튼으로 각 연출을 하나씩 재생해 볼 수 있는 `UIAnimTestScene`을 만들어 줘: 페이즈 배너 3종, 조우 3종, 헤드샷(+ `HitStop.Do(0.15f)`와 `CameraPunch`), 일러스트 카드 교체.

## 지켜 줄 것

- 모든 UI 애니메이션은 unscaled time으로 돌아가야 해. 히트스톱 중에도 UI는 움직여야 하기 때문이야.
- 원본에 없는 새 디자인 요소는 추가하지 말고, 원본 CSS 값을 최대한 그대로 옮겨 줘.
- `BlindsideUIAnim.cs`에서 컴파일 오류가 나면 고쳐 주고, 무엇을 고쳤는지 알려 줘.
