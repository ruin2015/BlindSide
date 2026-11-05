BLINDSIDE UI 킷 (일본어판 목업 v5)
- ui_target.html        : UI 목표 목업 (1440x810 기준, 일본어). 서체 내장. 같은 폴더의 operator_photos가 필요. Chrome으로 열기
                          · 위쪽 버튼: 타이틀/게임 화면, 글리치, 피격, 페이즈 배너, 확인 창, "UI 100/90/80/70%"(게임 HUD 배율)
- operator_photos/      : 대원 상반신(KEY.png)·얼굴(KEY_face.png) 20장
- BSTactical-Bold.ttf   : 직접 제작한 전용 서체 v2 (영문·숫자·기호 + 아이콘 ✦✚✛✹⬢▣◎⌖➜ + 화살표 ←→↑↓, 9도 기울임 내장)
- make_font.py          : 서체 생성 스크립트 (필요: python, fonttools, shapely)
- charset_ja_used.txt   : 일본어 UI에서 실제로 쓰는 글자 목록 (TMP 정적 아틀라스용)
- charset_ja_jis1.txt   : 위 + 히라가나·가타카나 + JIS 第一水準 漢字 2,965자 (앞으로 문구가 늘어도 대비)
- ko_ja_dictionary.tsv  : 한국어 → 일본어 문구 사전 (Unity의 한글 문구를 일본어로 바꿀 때 사용)
- glossary_ja.md        : 용어집·문체 규칙
- ui_spec.md          : 목업의 크기·색·모션 수치만 모은 작은 명세 (Claude Code는 큰 ui_target.html 대신 이 파일을 먼저 읽을 것)
