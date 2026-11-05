# 맵 저장/불러오기/맵 선택/테스트 플레이 — 중단 지점과 재개 방법 (2026-09-30)

## 요청 (원문 요약)
저장(persistentDataPath/Maps/{이름}.json, 덮어쓰기 확인, 금지 문자 치환, 필수 오류 있어도 저장 가능/게임 시작 불가), 불러오기 창(목록·미니맵·열기/복제/삭제, 기본 저택 읽기 전용 맨 위, 손상 처리), 미저장 경고(저장/버리기/취소), 테스트 플레이(공격/방어, 미저장 포함, 오류 시 비활성+이유, 결과 화면 "에디터로", 게임은 복사본), 타이틀 맵 카드(PlayerPrefs 기억, 오류 맵 시작 불가, 승리 조건 문구), 재도전=같은 맵·진영.
최종 보고 형식: **표 + Console 에러 유무 + 맵 카드가 보이는 타이틀 스크린샷 1장만**. 정확도 > 토큰.

## 구현 상태: 코드 전부 완료, 컴파일 깨끗, Verify 4종 통과
새 파일: MapEditor/{MapObjects,MapSight,MapValidator,MapFile(MapFile+MapStore+MapPreview),ModalDialog,MapBrowserWindow}.cs, Integration/PlaySession.cs, Map/Editor/{MapObjectVerify,MapStoreVerify}.cs
수정: MapEditModel(BuildGameData/FindObjectProblem/PickAttackerDrops), MapEditorScreen(저장·불러오기·dirty·읽기전용·테스트·Suspend/Resume), TitleSelectScreen(맵 카드·Launch(bool)·Restart), ResultScreen(에디터로), MatchBootstrap(.Result: ReturnToEditor/RestoreDefaultWorld, ClearMatch에서 헬기 끔), MapWallSync(상자 동기화), MapFloorSync(비직사각 실내 바닥 메시+출구 뷰 재빌드), ExfilZone.RangeText(1곳 표기), MapData.Validate(카메라=아무 바닥), BattlefieldCamera(SKeyReserved)
메뉴 검증: Blindside/Map/ Verify Map Editor Objects · Verify Map Editor Model · Verify Map Files · Verify Wall Kinds (모두 PASS)

## Play에서 실제 클릭으로 확인 완료
저장(금지문자 "테스트맵: 방3개?"→"테스트맵_ 방3개_.json"), 덮어쓰기 창(취소/덮어쓰기), 미저장 경고 3버튼, 손상 파일 2종 "(손상)"+"이 맵을 열 수 없습니다: …", 삭제 확인/복제, 선택 기억(PlayerPrefs), 타이틀 START 공격·방어, 재도전(같은 맵·진영), 타이틀로(기본 맵 복원), 에디터 테스트 공격/방어(미저장 크레이트 반영, 게임이 맵을 망가뜨려도 에디터 JSON 동일, 에디터로 복귀 시 Undo 기록·dirty·도구·카메라 유지, 테스트 모드 재도전), 필수 오류 시 버튼 비활성+툴팁.

## 남은 일 (순서대로)
1. AI 대 AI 10판 재실행 (앞서 실행 중 중단됨, 예외 0건까지 확인): Play 진입 → 프레임 수백 후 execute_code:
   `var d = Blindside.MapEditor.MapStore.Find("테스트맵_ 방3개_").BuildGameData(); Blindside.Integration.SimBatch.StartMaps(@"D:\Blind\Baselines\ai_saved_map.txt", 10, 2001, 40f, new[]{d}, new[]{true});`
   결과 파일 생기면(약 10분) 승패/라운드/예외/over15를 표로. 끝나면 **Play 종료**(배치가 카메라 끄고 40배속).
   (선택) 기본 맵 회귀 3판: `SimBatch.Start(경로, 3, 1001)` 결과를 Baselines/default_map.txt 앞 3줄×2모드와 비교.
2. 사용자의 "전체 검증" 절차 문서화(표에 넣기): 새 맵(방3·벽3종·카메라2·출구2) 저장 → 앱 재시작(Play 재진입: 타이틀이 선택 맵 기억 확인됨) → 불러오기 → 공격 1판 → 방어 1판. 자동으로 돈 것/사람이 해야 하는 것(실제 마우스로 끝까지 플레이) 구분해서 표기.
3. 정리(필수): `Assets/Blindside/Scripts/Map/Editor/_UiDrive.cs(+.meta)` 삭제; 테스트 맵 `…\LocalLow\DefaultCompany\Blind\Maps\`의 `테스트맵_ 방3개_.json`, `good.bak` 삭제(또는 사용자에게 묻기); PlayerPrefs "Blindside.SelectedMap" 를 "" 로; `Captures/title_map_card.png`(에디터 창이 겹침)를 `title_map_card-1.png`로 교체; 다른 임시 캡처(title_map_card_v1/v2, custom_map_game_*, editor_after_test_return 등)는 필요 시 삭제. 정리 후 재컴파일 + 4종 Verify + Console 확인.
4. 최종 보고: 표(자동검증 통계, AI 10판, 수동 절차) + Console 에러 유무 + 타이틀 스크린샷 1장(`Captures/title_map_card-1.png`).

## 주의
- Play 중 스크립트 수정/refresh 금지(도메인 리로드). 컴파일은 Play 종료 후.
- 사용자가 Play 창을 직접 만질 수 있음(에디터 창이 저절로 열려 있던 적 있음). 입력 테스트 때는 `UiDrive.Init()`가 실제 포인터를 끄므로 **항상 `UiDrive.Cleanup()` 후 종료**(장치·Input 설정 복구). 자세한 요령은 메모리 map-editor-ui-testing.
- 종료 후 편집 모드에서 TitleSelect 오브젝트가 active인지 확인(Play 중 비활성으로 만든 적 있음).
- execute_code: CodeDom C# 6 — 타입 정의/로컬함수 불가, File.Delete 패턴 차단(삭제는 Bash).
- 알려진 잔여 사항: 없음(헬기 잔상·OVERLAY 라벨 수정은 컴파일 완료, Play 재확인은 안 함).
