# 새로 번역한 문구 (사전에 없던 것, glossary_ja.md 규칙 적용)

| 위치 | 한국어 | 日本語 |
|---|---|---|
| ResultScreen.cs:91 | 에디터로 | エディタへ |
| TitleSelectScreen.cs:265 | 구출 지점 0곳 (맵에 구출 지점이 없음) | 救出地点0か所 (マップに救出地点なし) |
| TitleSelectScreen.cs:266 | 구출 지점 1곳(EXFIL {a})까지 호송 | 救出地点1か所(EXFIL {a})まで護送 |
| TitleSelectScreen.cs:267 | 구출 지점 {n}곳(EXFIL {a}~{b}) 중 한 곳까지 호송 | 救出地点{n}か所(EXFIL {a}〜{b})のいずれかまで護送 |
| TitleSelectScreen.cs:313 | 맵 선택 ▸ | マップ選択 ▸ |
| TitleSelectScreen.cs:336 | {size} · 구출 지점 {n}곳 | {size} · 救出地点{n}か所 |
| TitleSelectScreen.cs:340 | 필수 오류 {n}건 — 시작할 수 없음 | 必須エラー{n}件 — 開始できません |
| HudPrefabBuilder.cs:302 | ① 인질을 구출 지점 {range} 중 한 곳까지 호송\n② 또는 방어팀 전원 제압 | ① 人質を救出地点{range}のいずれかまで護送\n② または防衛チームを全員制圧 |
| HudPrefabBuilder.cs:493 / HUD_Log.prefab | 전투 기록 항목 | 戦闘記録の項目 |
| BlindsideUIBuilder.cs:56 | 캔버스와 공통 스프라이트를 만들었습니다.…자세한 경로는 Console을 확인하세요. | キャンバスと共通スプライトを作成しました。…詳しいパスはConsoleを確認してください。 |
| ExfilZone.cs:51 | {n}곳(EXFIL {a}~{b}) / 1곳(EXFIL {a}) | {n}か所(EXFIL {a}〜{b}) / 1か所(EXFIL {a}) |
| MapValidator.cs:60 | 인질이 없습니다 | 人質がいません |
| MapValidator.cs:65 | 방어 시작이 {n}/{m}개입니다 (5개 필요 · 다음은 {k}번 {key}) | 防衛開始位置が{n}/{m}個です (5個必要 · 次は{k}番 {key}) |
| MapValidator.cs:67 | 구출 지점이 없습니다 (1개 이상 필요) | 救出地点がありません (1個以上必要) |
| MapValidator.cs:68 | 강하 가능 구역이 {n}칸입니다 (실외 빈 칸 {m}칸 이상 필요) | 降下可能区域が{n}マスです (屋外の空きマスが{m}マス以上必要) |
| MapValidator.cs:75 | 공격 불가능: 일반 벽으로 완전히 막혀 있음 | 攻撃不可能: 通常の壁で完全に塞がれている |
| MapValidator.cs:77 | 구출 불가능: 인질에서 구출 지점까지 경로 없음 (일반 벽으로 완전히 막혀 있음) | 救出不可能: 人質から救出地点までの経路なし (通常の壁で完全に塞がれている) |
| MapValidator.cs:85 | 방어 시작 {i}번 {key}이(가) 인질에서 {d}칸 떨어짐 ({m}칸 초과) | 防衛開始位置{i}番 {key}が人質から{d}マス離れている ({m}マス超過) |
| MapValidator.cs:95 | 감시카메라 {i}번 ({x}, {y})이(가) 벽에 완전히 둘러싸임 | 監視カメラ{i}番 ({x}, {y})が壁に完全に囲まれている |
| MapFile.cs:74 | 파일이 비어 있습니다 | ファイルが空です |
| MapFile.cs:78 | JSON 형식이 아닙니다 | JSON形式ではありません |
| MapFile.cs:81 | JSON을 읽을 수 없습니다 (…) | JSONを読み込めません (…) |
| MapFile.cs:84 | 지원하지 않는 파일 형식입니다 (format {f}, 지원: {c}) | 対応していないファイル形式です (format {f}, 対応: {c}) |
| MapFile.cs:86 | 맵 크기가 범위를 벗어났습니다 ({w}×{h}, 허용 {a}~{b}) | マップサイズが範囲外です ({w}×{h}, 許容 {a}〜{b}) |
| MapFile.cs:88 | 크기가 맞지 않습니다: 바닥 칸 {k}개, {w}×{h}에는 {n}개가 필요합니다 | サイズが合いません: 床のマス{k}個、{w}×{h}には{n}個が必要です |
| MapFile.cs:89 | 크기가 맞지 않습니다: 상자 칸 {k}개, {n}개가 필요합니다 | サイズが合いません: 木箱のマス{k}個、{n}個が必要です |
| MapFile.cs:91 | 알 수 없는 바닥 값입니다: {v} ({x}, {y}) | 不明な床の値です: {v} ({x}, {y}) |
| MapFile.cs:93 | 벽 위에 상자가 있습니다 ({x}, {y}) | 壁の上に木箱があります ({x}, {y}) |
| MapFile.cs:101 | 방어 시작 목록에 빈 항목이 있습니다 | 防衛開始位置の一覧に空の項目があります |
| MapFile.cs:102 | 알 수 없는 방어 시작 번호입니다: {s} | 不明な防衛開始位置の番号です: {s} |
| MapFile.cs:103 | 방어 시작 {s}번이 두 번 있습니다 | 防衛開始位置{s}番が重複しています |
| MapFile.cs:105 | 알 수 없는 대원 이름입니다: "{k}" (방어 시작 {s}번은 {key}) | 不明な隊員名です: "{k}" (防衛開始位置{s}番は{key}) |
| MapFile.cs:106 | 방어 시작 {s}번의 방향 값이 올바르지 않습니다 | 防衛開始位置{s}番の向きの値が正しくありません |
| MapFile.cs:108 | 구출 지점 목록에 빈 항목이 있습니다 | 救出地点の一覧に空の項目があります |
| MapFile.cs:109 | 이름 없는 맵 | 名前のないマップ |
| MapFile.cs:190 (+MapEditorScreen) | 새 맵 | 新規マップ |
| MapFile.cs:231 |  (복제) |  (複製) |
| MapFile.cs:267 | 파일을 읽을 수 없습니다 (…) | ファイルを読み込めません (…) |
| MapEditModel.cs:102 | 감시카메라가 {n}개 (최대 {m}개) | 監視カメラが{n}個 (最大{m}個) |
| MapEditModel.cs:103 | 구출 지점이 {n}곳 (최대 {m}곳) | 救出地点が{n}か所 (最大{m}か所) |
| MapEditModel.cs:107 | {what} ({x}, {y})이(가) 맵 밖에 있음 | {what} ({x}, {y})がマップの外にある |
| MapEditModel.cs:108 | {what} ({x}, {y})이(가) 벽 위에 있음 | {what} ({x}, {y})が壁の上にある |
| MapEditModel.cs:109 | {what} ({x}, {y})이(가) 상자 위에 있음 | {what} ({x}, {y})が木箱の上にある |
| MapEditModel.cs:110 | {what} ({x}, {y})이(가) 실내 바닥이 아닌 칸에 있음 | {what} ({x}, {y})が屋内の床ではないマスにある |
| MapEditModel.cs:111,128 | …이(가) 다른 오브젝트와 겹침 | …が他のオブジェクトと重なっている |
| MapEditModel.cs:117 (+MapEditorScreen:773) | 방어 시작 {i}번 | 防衛開始位置{i}番 |
| MapEditModel.cs:119 | 감시카메라 {i}번 | 監視カメラ{i}番 |
| MapEditModel.cs:123 | 구출 지점 {L}이(가) 맵 밖에 있거나 좌표가 뒤집혀 있음 | 救出地点{L}がマップの外にあるか、座標が反転している |
| MapEditModel.cs:127 | 구출 지점 {L} ({x}, {y})이(가) 실외 빈 바닥이 아님 | 救出地点{L} ({x}, {y})が屋外の空いた床ではない |
| MapEditModel.cs:401 | 이미 오브젝트가 있는 칸입니다 (서로 겹칠 수 없음) | すでにオブジェクトがあるマスです (重ねて置けません) |
| MapEditModel.cs:403 | 벽 위에는 놓을 수 없습니다 (바닥 칸만 가능) | 壁の上には置けません (床のマスのみ可能) |
| MapEditModel.cs:404 | 상자 위에는 놓을 수 없습니다 (바닥 칸만 가능) | 木箱の上には置けません (床のマスのみ可能) |
| MapEditModel.cs:409 | 맵 밖에는 놓을 수 없습니다 | マップの外には置けません |
| MapEditModel.cs:442 | 인질은 실내 바닥에만 놓을 수 있습니다 | 人質は屋内の床にのみ置けます |
| MapEditModel.cs:449 | 방어 시작은 실내 바닥에만 놓을 수 있습니다 | 防衛開始位置は屋内の床にのみ置けます |
| MapEditModel.cs:459 | 감시카메라는 최대 {n}개까지 놓을 수 있습니다 | 監視カメラは最大{n}個まで置けます |
| MapEditModel.cs:465 | 맵 밖에는 만들 수 없습니다 | マップの外には作れません |
| MapEditModel.cs:466 | 구출 지점은 최대 {n}개까지 만들 수 있습니다 | 救出地点は最大{n}個まで作れます |
| MapEditModel.cs:471 | 구출 지점은 벽·상자와 겹칠 수 없습니다 | 救出地点は壁・木箱と重ねられません |
| MapEditModel.cs:474 | 구출 지점은 실외 바닥에만 만들 수 있습니다 | 救出地点は屋外の床にのみ作れます |
| MapEditorScreen.cs:95 | 기본 저택은 읽기 전용입니다 — 불러오기 창에서 [복제]한 뒤 수정하세요 | 基本邸宅は読み取り専用です — 読み込みウィンドウで[複製]してから編集してください |
| MapEditorScreen.cs:165 | 저장하지 않은 변경이 있습니다 | 未保存の変更があります |
| MapEditorScreen.cs:165 | '{name}'의 변경 내용을 저장할까요? | '{name}'の変更内容を保存しますか？ |
| MapEditorScreen.cs:166,1008 | 저장 | 保存 |
| MapEditorScreen.cs:167 | 버리기 | 破棄 |
| MapEditorScreen.cs:183 | 기본 저택 (복제) | 基本邸宅 (複製) |
| MapEditorScreen.cs:288 | 읽기 전용 / ● 저장 안 됨 / 저장됨 / 새 맵 (미저장) | 読み取り専用 / ● 未保存 / 保存済み / 新規マップ (未保存) |
| MapEditorScreen.cs:305,324 | 저장할 수 없음 | 保存できません |
| MapEditorScreen.cs:305 | '{name}'은(는) 기본 제공 맵의 이름입니다. 다른 이름으로 저장하세요. | '{name}'は標準搭載マップの名前です。別の名前で保存してください。 |
| MapEditorScreen.cs:311 | 덮어쓰기 | 上書き |
| MapEditorScreen.cs:311 | '{stem}.json' 파일이 이미 있습니다.\n덮어쓰면 그 맵은 지금 편집 중인 맵으로 바뀝니다. 덮어쓸까요? | '{stem}.json' ファイルがすでにあります。\n上書きすると、そのマップは現在編集中のマップに置き換わります。上書きしますか？ |
| MapEditorScreen.cs:324 | 맵 파일을 쓰지 못했습니다:  | マップファイルを書き込めませんでした:  |
| MapEditorScreen.cs:330 |  (파일 이름에 쓸 수 없는 문자를 바꿨습니다: {stem}.json) |  (ファイル名に使えない文字を置き換えました: {stem}.json) |
| MapEditorScreen.cs:332 | 저장했습니다: {path} | 保存しました: {path} |
| MapEditorScreen.cs:332 |  — 필수 오류 {n}건이 있어 게임 시작에는 쓸 수 없습니다 |  — 必須エラーが{n}件あるため、ゲーム開始には使えません |
| MapEditorScreen.cs:349 | 필수 오류 {n}건 — 고친 뒤에 테스트할 수 있습니다: | 必須エラー{n}件 — 修正してからテストできます: |
| MapEditorScreen.cs:367 | 테스트할 수 없음 | テストできません |
| MapEditorScreen.cs:369 | 타이틀 화면을 찾을 수 없습니다. | タイトル画面が見つかりません。 |
| MapEditorScreen.cs:371 | 게임이 이 맵을 받아들이지 않았습니다 (Console 참고). | ゲームがこのマップを受け付けませんでした (Console参照)。 |
| MapEditorScreen.cs:438,512 | 되돌리기 {n} | 元に戻す {n} |
| MapEditorScreen.cs:710 | 좌클릭 그리기 · 드래그 직선 · Shift+드래그 사각형(벽은 테두리) · 우클릭 지우기 · 우클릭 드래그 이동 · WASD(S 제외)/방향키/휠 · Ctrl+Z/Y · G 격자 | 左クリックで描画 · ドラッグで直線 · Shift+ドラッグで四角形(壁は外周) · 右クリックで消去 · 右ドラッグで移動 · WASD(S除く)/方向キー/ホイール · Ctrl+Z/Y · G グリッド |
| MapEditorScreen.cs:716 | 인질: 실내 바닥 클릭으로 배치 (맵에 1개, 다시 찍으면 이동) · 우클릭 삭제 · Ctrl+Z 되돌리기 | 人質: 屋内の床をクリックして配置 (マップに1個、もう一度クリックで移動) · 右クリックで削除 · Ctrl+Z 元に戻す |
| MapEditorScreen.cs:717 | 방어 시작: 클릭 = 1→5번 순서로 배치 (5개가 차면 5번 이동) · 찍고 드래그 = 바라보는 방향 · 우클릭 삭제 | 防衛開始位置: クリック = 1→5番の順に配置 (5個そろうと5番を移動) · クリックしてドラッグ = 向く方向 · 右クリックで削除 |
| MapEditorScreen.cs:718 | 감시카메라: 바닥 칸 클릭 (최대 8개) · 우클릭 삭제 | 監視カメラ: 床のマスをクリック (最大8個) · 右クリックで削除 |
| MapEditorScreen.cs:719 | 구출 지점: 실외 바닥에서 드래그로 사각형 (최대 8개, A·B·C… 라벨) · 우클릭 삭제 | 救出地点: 屋外の床でドラッグして四角形 (最大8個、A·B·C… のラベル) · 右クリックで削除 |
| MapEditorScreen.cs:720 | 선택: 오브젝트를 클릭하면 속성 표시 · 우클릭 삭제 · 카메라 아래 이동은 ↓ 방향키 | 選択: オブジェクトをクリックでプロパティを表示 · 右クリックで削除 · カメラの下移動は↓方向キー |
| MapEditorScreen.cs:729 | 배치 불가 ·  | 配置不可 ·  |
| MapEditorScreen.cs:767-787 | 좌표 / 대원 / 방향 / 시야 반경 / 크기 {w} × {h}칸 | 座標 / 隊員 / 向き / 視界半径 / サイズ {w} × {h}マス |
| MapEditorScreen.cs:792 | 선택 없음 | 選択なし |
| MapEditorScreen.cs:792 | 오브젝트를 고르려면 S(선택) 도구로 클릭하세요. | オブジェクトを選ぶには、S(選択)ツールでクリックしてください。 |
| MapEditorScreen.cs:801 | 동/남동/남/남서/서/북서/북/북동 | 東/南東/南/南西/西/北西/北/北東 |
| MapEditorScreen.cs:816 | 필수 오류 {n} · 경고 {w}\n테스트 플레이 불가 | 必須エラー {n} · 警告 {w}\nテストプレイ不可 |
| MapEditorScreen.cs:817 | 필수 조건 충족 · 경고 {w}\n테스트 플레이 가능 | 必須条件クリア · 警告 {w}\nテストプレイ可能 |
| MapEditorScreen.cs:826 | … 외 {n}건 | … ほか{n}件 |
| MapEditorScreen.cs:830 | 문제가 없습니다. | 問題はありません。 |
| MapEditorScreen.cs:841 | [필수] / [경고] | [必須] / [警告] |
| MapEditorScreen.cs:856 | 일반 벽 {n}칸\n부서지는 벽 {n}칸 (패널 {k}개)\n강화벽 {n}칸 (패널 {k}개) | 通常の壁 {n}マス\n破壊可能な壁 {n}マス (パネル {k}枚)\n強化壁 {n}マス (パネル {k}枚) |
| MapEditorScreen.cs:863-874 | 지우기/실외 바닥/실내 바닥/일반 벽/부서지는 벽/방어 시작/선택/상자 | 消去/屋外の床/屋内の床/通常の壁/破壊可能な壁/防衛開始位置/選択/木箱 |
| MapEditorScreen.cs:995 | ← 나가기 | ← 戻る |
| MapEditorScreen.cs:997 | 맵 이름 | マップ名 |
| MapEditorScreen.cs:1003-1005 | 강하 구역 / 카메라 시야 / 격자 | 降下区域 / カメラ視界 / グリッド |
| MapEditorScreen.cs:1007 | 새 맵 (상단 버튼) | 新規 |
| MapEditorScreen.cs:1009 | 불러오기 | 読み込み |
| MapEditorScreen.cs:1010-1011 | 공격으로 테스트 / 방어로 테스트 | 攻撃でテスト / 防衛でテスト |
| MapEditorScreen.cs:1020-1053 | TOOLS // 도구 · OBJECTS // 오브젝트 · PROPERTIES // 속성 · WALLS // 통계 · VALIDATION // 검증 | TOOLS // ツール · OBJECTS // オブジェクト · PROPERTIES // プロパティ · WALLS // 統計 · VALIDATION // 検証 |
| MapEditorScreen.cs:1209 | MAP EDITOR // 새 맵, 기본 저택 복제, 저장된 맵 중에서 시작합니다. | MAP EDITOR // 新規マップ、基本邸宅の複製、保存済みマップのいずれかから開始します。 |
| MapEditorScreen.cs:1210 | 새 맵 · 크기 (16~40) | 新規マップ · サイズ (16〜40) |
| MapEditorScreen.cs:1214,1216 | 가로 / 세로 | 横 / 縦 |
| MapEditorScreen.cs:1221 | 새 맵 만들기 | 新規マップを作成 |
| MapEditorScreen.cs:1223 | 기본 저택 복제 | 基本邸宅を複製 |
| MapEditorScreen.cs:1224 | 저장된 맵 불러오기 | 保存済みマップを読み込む |
| MapBrowserWindow.cs:120 | 맵 선택 / 맵 불러오기 | マップ選択 / マップ読み込み |
| MapBrowserWindow.cs:122 | MAP SELECT // 플레이할 맵을 고르세요 | MAP SELECT // プレイするマップを選んでください |
| MapBrowserWindow.cs:122 | LOAD MAP // 편집할 맵을 고르세요 | LOAD MAP // 編集するマップを選んでください |
| MapBrowserWindow.cs:133-135 | 이름 / 크기 / 수정 시각 | 名前 / サイズ / 更新日時 |
| MapBrowserWindow.cs:175 | 열기 | 開く |
| MapBrowserWindow.cs:183,283 | 저장 위치:  | 保存先:  |
| MapBrowserWindow.cs:186 | 닫기 | 閉じる |
| MapBrowserWindow.cs:193 | 바깥 / 실내 | 屋外 / 屋内 |
| MapBrowserWindow.cs:231 |   (손상) |   (破損) |
| MapBrowserWindow.cs:236 | 읽기 전용 · 기본 제공 | 読み取り専用 · 標準搭載 |
| MapBrowserWindow.cs:241 | ● 사용 중 | ● 使用中 |
| MapBrowserWindow.cs:261 | 손상된 파일 ·  | 破損したファイル ·  |
| MapBrowserWindow.cs:264,295 | 이 맵을 열 수 없습니다:  | このマップを開けません:  |
| MapBrowserWindow.cs:268 | 읽기 전용 (수정하려면 복제) / 수정 {date} | 読み取り専用 (編集するには複製) / 更新 {date} |
| MapBrowserWindow.cs:276 | 필수 오류 {n}건 — 게임 시작에는 쓸 수 없습니다 (편집은 가능) | 必須エラー{n}件 — ゲーム開始には使えません (編集は可能) |
| MapBrowserWindow.cs:278 | … 외 {n}건 | … ほか{n}件 |
| MapBrowserWindow.cs:281 | 필수 오류 없음 — 게임 시작에 쓸 수 있습니다 | 必須エラーなし — ゲーム開始に使えます |
| MapBrowserWindow.cs:295 | 맵을 열 수 없음 | マップを開けません |
| MapBrowserWindow.cs:310 | 복제할 수 없음 / 맵을 복제하지 못했습니다:  | 複製できません / マップを複製できませんでした:  |
| MapBrowserWindow.cs:317 | 맵 삭제 | マップ削除 |
| MapBrowserWindow.cs:317 | '{name}' 파일을 삭제할까요?\n삭제한 맵은 되돌릴 수 없습니다. | '{name}' ファイルを削除しますか？\n削除したマップは元に戻せません。 |
| MapBrowserWindow.cs:328 | 삭제할 수 없음 / 파일을 삭제하지 못했습니다:  | 削除できません / ファイルを削除できませんでした:  |
| MapStoreVerify.cs (테스트 데이터) | 세 개의 방 / 작업 중 / 빈 맵 / 없는 맵 / 하나 | 3つの部屋 / 作業中 / 空のマップ / 存在しないマップ / ひとつ |
| TestMaps.cs:62,78,88 (테스트 맵 이름) | 작은 방 / 여러 방의 저택 / 밀폐된 방 | 小部屋 / 部屋の多い邸宅 / 密閉された部屋 |
| MarkBoard.cs:12 (주석) | 설치물 포착 | 設置物を捕捉 |
| CombatSystem.cs:339 | 인질 (Kr 대체값) | 人質 |
| BattleDirector.cs:68 | 풀 / 짧게 / 끄기 | フル / 短め / オフ |
| BattleDirector.cs:827 | 전투 연출 · {mode} | 戦闘演出 · {mode} |
| TurnManager.cs:142 (Debug.Log) | Round {r}: 공격 {a}명 / 방어 {d}명 생존 | Round {r}: 攻撃 {a}名 / 防衛 {d}名 生存 |
| GameplayUIBridge.cs:147,371 | 공격 턴 | 攻撃ターン |
| GameplayUIBridge.cs:149,371 | 부대를 전개하십시오 | 部隊を展開してください |
| GameplayUIBridge.cs:364 | 배치 단계 | 配置フェーズ |
| GameplayUIBridge.cs:364 | 공격팀 침투 지점을 선택하십시오 | 攻撃チームの侵入地点を選択してください |
| GameplayUIBridge.cs:369 | 준비 단계 | 準備フェーズ |
| GameplayUIBridge.cs:369 | 방어팀 요새화를 진행하십시오 | 防衛チームの要塞化を進めてください |
| GameplayUIBridge.cs:372 | 방어 턴 | 防衛ターン |
| GameplayUIBridge.cs:372 | 구역을 사수하십시오 | 区域を死守してください |
| GameplayUIBridge.cs:405 | 공격팀 승리 / 방어팀 승리 | 攻撃チーム勝利 / 防衛チーム勝利 |
| HudController.cs:168 | ① 인질을 구출 지점 {range} 중 한 곳까지 호송\n② 또는 방어팀 전원 제압 | ① 人質を救出地点{range}のいずれかまで護送\n② または防衛チームを全員制圧 |
| PlayerSelectionController.cs:533 (Debug.Log) | '{kr}' ({c}) 명령 — 실행 로직은 아직 연결되지 않았습니다. | '{kr}' ({c}) コマンド — 実行ロジックはまだ接続されていません。 |
| PlayerSelectionController.SupportAbility.cs:67 | 설치 가능한 구역이 없습니다 | 設置可能な区域がありません |
| MatchBootstrap.Result.cs:32 (Debug.Log) | [Match] 승자: {팀} — {reason} | [Match] 勝者: {チーム} — {reason} |
| AttackerSpawn.cs:26 | 강하 가능 구역이 부족합니다 | 降下可能区域が不足しています |
| MarkViewController.cs:138 | 설치물 포착 | 設置物を捕捉 |
| DroneOps.Move.cs:10 (주석 인용) | 적의 능력 설치물이 시야에 들어오면 표시 | 敵の能力設置物が視界に入ったら表示 |
| EncounterResolver.cs:18-20 (주석) | 나만 적을 봄 / 서로 봄 / 적만 나를 봄 | 自分だけが敵を見る / 互いに見る / 敵だけが自分を見る |
| HUD_CmdMenu.prefab | 능력 사용 / 사격 | 能力使用 / 射撃 |
| HUD_EndBtn.prefab | 종료 | 終了 |
| HUD_ConfirmDialog.prefab | 이 행동을 실행하시겠습니까? | この行動を実行しますか？ |
| HUD_Hint.prefab | 지시를 선택해주십시오 | 指示を選択してください |
| HUD_ResultScreen.prefab | 계속 / 작전 결과 통계 | 続ける / 作戦結果の統計 |
| HUD_OperatorArtCard.prefab | 돌격 · 강습 / ✦ 능력 설명 | 突撃 · 強襲 / ✦ 能力説明 |