# MapData 리팩터링 — 중단 지점과 재개 방법 (2026-09-30)

목표: 하드코딩된 맵을 MapData/DefaultMap.json으로 분리. 게임 동작은 조금도 바뀌면 안 됨. 회귀 기준 = 시드 고정 AI 대 AI 20판.

## 현재 상태
- **0단계 완료**: `Baselines/default_map.txt` (시드 1001~1020, 모드 2개). 재실행 12/12 동일 확인(결정적).
  - 모드 A `AIvsAI`: 방어 20승. 모드 B `AIvsAI+DefensePrep+RandomAttackerSpawn`: 공격 5승/방어 15승. 예외 0.
- **GameRng 통합 완료**: `Assets/Blindside/Scripts/Core/GameRng.cs`. 판정·AI 런처·랜덤 강하만 사용(연출용 Random은 그대로).
- **하네스**: `Integration/SimBatch.cs`. Play 중 execute_code로 `Blindside.Integration.SimBatch.Start(@"D:\Blind\Baselines\after.txt", 20, 1001, 100f)`.
- **2~3단계 코드는 작성됨, 컴파일 검증 전**:
  - 새 파일: `Map/MapData.cs`, `Map/MapLibrary.cs`, `Map/Editor/DefaultMapBuilder.cs`, `Rendering/MapFloorSync.cs`
  - 수정: MapGrid(MapData 생성자, Width는 static 속성), ExfilZone(All이 MapData.exfils 읽음), AttackerSpawn(ZoneOf: BuiltBounds 기준 4방향 띠), PlayerSelectionController.Deploy(BLD 삭제, 드롭구역=Out&&!crate), MatchBootstrap, DefensePrep, EnemyAI(폴백 목표), TitleSelectScreen(useHouseIntro·카메라), IntroCameraRig, HeliDeployRig, BattlefieldCamera
  - MapData에 스펙 외 `anchors` 블록 추가(공격 기본 강하점, 방어 준비 좌표, 카메라/인트로 앵커) — 최종 보고에 명시할 것.
  - 마지막 컴파일에서 나온 에러(DefaultMapBuilder.cs 변수 `a` 충돌)는 수정했으나 재컴파일은 안 함. 다른 에러가 더 있을 수 있음.
- **아직 없음**: `Assets/Blindside/Resources/DefaultMap.json` → 이 상태로 Play하면 맵 로드 실패. 먼저 생성해야 함.

## 이어서 할 일 (순서대로)
1. Unity에서 컴파일 확인(`refresh_unity force + compile` **한 번만**), 에러 있으면 수정. 완료 후 Editor.log에서 "Domain Reload Profiling" 확인 → 콘솔 비우기.
2. 메뉴 `Blindside > Map > Build DefaultMap.json` 실행(파일 생성, 읽기 전용 설정) → `Verify DefaultMap.json` → 모든 항목 PASS여야 함(원본 JS MAP 대비 520칸 일치, 구출지점 4곳, 상자 바닥 In/Out, 건물 범위·드롭구역·랜덤 띠 동등성).
3. Play 진입 → SimBatch 20판×2모드 → 새 파일과 `default_map.txt`를 비교(완전히 같아야 함. 다르면 원인 찾아 수정).
4. Console 에러 확인, 기본 맵 스크린샷 1장(메모리 `unity-playmode-testing-pitfalls`의 스크린샷 요령 참고), 최종 표(감사 항목 수 / 변환 일치 / 기준선 비교) 보고. 코드는 다시 보여주지 않기.

## 주의
- Play 중 스크립트 수정 금지, `refresh_unity` 반복 금지: 늦은 도메인 리로드가 배치를 두 번 끊었음. 배치 진행은 결과 파일 생성으로 확인(execute_code 폴링 최소화). 매치당 약 30초, 40판 약 20분.
- 이 프로젝트는 git 저장소가 아님. 작업 전 `Assets/Blindside` 백업 권장.
- 카메라 이동 한계는 원본/현재 코드에 원래 없음(휠 확대 9~42만 있음) — 그대로 두고 보고.
- 정적 벽/상자 블록과 House_Exterior, IntroOrbitCam/LookTarget은 씬에 구워져 있음(데이터 미연동) — 보고에 명시.
- 사용자 지시: 정확도가 토큰보다 우선. Edit 자동승인이 막히면 같은 파일 재시도 말고 더 작은 단위로 쪼개고, 그래도 막히면 보고 후 중단(다른 도구로 우회 금지).
