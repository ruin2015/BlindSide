# BLINDSIDE 효과음 목록

## 넣는 방법
- 파일을 `Assets/Blindside/Resources/Audio/SFX/` 에 **표의 파일 이름**으로 넣으면 끝이다(.wav / .ogg / .mp3 모두 가능). 코드 수정은 필요 없다.
- 같은 소리를 여러 버전으로 두려면 `gun_ar_1.mp3`, `gun_ar_2.mp3` … 처럼 번호를 붙인다(최대 8개). 재생할 때마다 하나를 무작위로 고른다.
- 파일이 없는 소리는 조용하다. 단, 지금 있는 임시 합성음(「코드 연결」이 **합성음 대체**인 것)은 파일이 없으면 기존 합성음이 그대로 난다.
- 코드: `SoundBank.Play(Snd.이름)` (한 번), `SoundBank.Loop(Snd.이름, true/false)` (반복). ID 목록은 `Scripts/UI/SoundBank.cs`.

## 소리 방향 — 레퍼런스: 레인보우 식스 시즈
- **무겁고 건조하게.** 귀엽고 밝은 「뿅·띵」 계열은 쓰지 않는다. 저음이 있고 짧게 끊기는 소리.
- **총소리는 선명하게.** 녹음 잡음(쉬익 하는 바탕 노이즈)·긴 야외 메아리가 깔린 현장 녹음은 피하고, 스튜디오에서 다듬은 「cinematic / designed」 계열을 고른다. 시즈처럼 실내 교전이므로 짧은 실내 잔향 정도만 있는 것이 좋다.
- **UI는 군용 장비 느낌.** 낮은 비프, 금속성 딸깍, 짧은 데이터 잡음. 화면 전환은 짧은 글리치·휙.
- **무전은 실제 무전기처럼.** 켤 때 잡음 + 끝날 때 짧은 비프(로저 비프).
- **폭발·벽 파괴는 파편이 남는 소리.** 터진 뒤 나무·석고 파편이 떨어지는 꼬리가 있는 것.
- Pixabay에서 고를 때 팁: 제작자가 **DRAGON-STUDIO, Universfield, SoundShelfStudio, RescopicSound, Vadim_Makes_Sound** 인 것은 대체로 다듬어진 효과음이다. **freesound_community** 는 현장 녹음이 많아 잡음이 섞인 경우가 있으니 들어 보고 고른다.

## 라이선스
- Pixabay 효과음은 **Pixabay Content License**: 게임에 넣어 상업적으로 써도 되고 출처 표기는 필요 없다. 단, 소리 파일 자체를 그대로 재배포·판매하는 것은 안 된다. 각 페이지의 라이선스 표시를 받기 전에 한 번 확인한다.
- 후보는 Pixabay 검색 결과·개별 페이지에서 찾은 것이고, 직접 들어 보고 고른 것은 아니다. 링크가 검색 페이지인 것은 그 검색어로 들어가 위 방향에 맞는 것을 고르면 된다.

## 코드 연결 표시
- **연결됨**: 게임에서 이미 그 순간에 재생을 호출한다. 파일만 넣으면 바로 들린다.
- **합성음 대체**: 지금 임시 합성음이 나는 자리. 파일을 넣으면 파일 소리로 바뀐다.
- **ID만**: 이름과 규칙만 잡아 두었다. 호출 위치는 소리가 준비되면 연결한다.

---

## 1. UI (군용 장비 톤으로 통일)
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| ui_hover | 버튼·항목에 마우스를 올림 | 0.03–0.08초 | × | ID만 | [HUD 검색](https://pixabay.com/sound-effects/search/hud/) — CreatorsHome "Select 001–004" |
| ui_confirm | 결정·클릭 (지금의 `Sfx.Ui`) | 0.05–0.15초 | × | 합성음 대체 | [sci-fi interface 검색](https://pixabay.com/sound-effects/search/sci-fi%20interface/) — RescopicSound "UI Click - Menu - Modern Interface - Select" |
| ui_cancel | 취소·뒤로 | 0.05–0.15초 | × | ID만 | [sci-fi interface 검색](https://pixabay.com/sound-effects/search/sci-fi%20interface/) — 같은 RescopicSound 세트의 낮은 버전 |
| ui_deny | 비활성 버튼·불가 (지금의 `SfxNoise.Click`) | 0.1–0.2초 | × | 합성음 대체 | [computer beep 검색](https://pixabay.com/sound-effects/search/computer%20beep/) — "Error Beep Sound" |
| ui_open | 메뉴·패널 열기 | 0.2–0.4초 | × | ID만 | [futuristic ui 검색](https://pixabay.com/sound-effects/search/futuristic%20ui/) — SoundShelfStudio "UI Futuristic HUD Beep" |
| ui_close | 메뉴·패널 닫기 | 0.2–0.4초 | × | ID만 | [futuristic ui 검색](https://pixabay.com/sound-effects/search/futuristic%20ui/) |
| ui_tab | Q/E 탭·대원 전환 | 0.05–0.1초 | × | ID만 | [digital click 검색](https://pixabay.com/sound-effects/search/digital%20click/) |
| ui_scroll | 목록 스크롤 틱 (상점 휠) | 0.02–0.05초 | × | ID만 | [digital clicking 검색](https://pixabay.com/sound-effects/search/digital%20clicking/) |
| ui_hold | 길게 누르기 차오름 (SKIP) | 1–1.5초 | × | ID만 | [tactical 검색](https://pixabay.com/sound-effects/search/tactical/) — SoundShelfStudio "UI Tactical Scan Beep" |
| ui_wipe | 화면 전환 와이프 | 0.5–1초 | × | ID만 | [digital transition 검색](https://pixabay.com/sound-effects/search/digital%20transition/) — Sdanezis "Futuristic Cyber Glitch Whoosh Transition" |
| ui_glitch | 글리치 연출 | 0.2–0.5초 | × | ID만 | ["Whoosh - glitch cut" (ejah_music)](https://pixabay.com/sound-effects/whoosh-glitch-cut-474226/) |
| ui_save | 저장 알림 | 0.2–0.4초 | × | ID만 | [computer data 검색](https://pixabay.com/sound-effects/search/computer%20data/) — "Digital Data Beeps" |

## 2. 턴·흐름
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| phase_ally | 「味方フェーズ」 배너 | 0.8–1.5초 | × | ID만 | [cinematic impact 검색](https://pixabay.com/sound-effects/search/cinematic%20impact/) — Universfield "Cinematic Impact Boom" (짧은 것) |
| phase_enemy | 「敵フェーズ」 배너 (더 낮고 어둡게) | 1–2초 | × | ID만 | [braam 검색](https://pixabay.com/sound-effects/search/braam/) — BryanSantosBreton "Critical Tension - Cinematic BRAAM Impact" |
| end_turn | END TURN 버튼 | 0.3–0.6초 | × | ID만 | [tactical 검색](https://pixabay.com/sound-effects/search/tactical/) |
| move_plan | 이동 경로 확정 | 0.1–0.2초 | × | ID만 | [HUD 검색](https://pixabay.com/sound-effects/search/hud/) |
| move_cancel | 이동 취소 | 0.1–0.2초 | × | ID만 | [HUD 검색](https://pixabay.com/sound-effects/search/hud/) |
| aim_lock | 사격 조준이 적에 맞음 | 0.1–0.2초 | × | ID만 | [tactical 검색](https://pixabay.com/sound-effects/search/tactical/) — SoundShelfStudio "UI Combat Interface Ping" |
| contact | 적과 조우 (지금의 `Sfx.Alert`) | 0.3–0.6초 | × | 합성음 대체 | [beeps 검색](https://pixabay.com/sound-effects/search/beeps/) — SoundShelfStudio "UI Warning Beep" |
| ambush | 매복 등장 같은 스크립트 장면 | 1–2초 | × | ID만 | [cinematic braam 검색](https://pixabay.com/sound-effects/search/cinematic%20braam/) — "Disturb - Trailer Impact, Dark" |
| objective_fail | 달성 목표 「失敗」 | 0.4–0.8초 | × | ID만 | [military signal 검색](https://pixabay.com/sound-effects/search/military%20signal/) — "Alarm Alert Beep Buzz" |
| victory | 작전 클리어 | 2–4초 | × | ID만 | [mission success 검색](https://pixabay.com/sound-effects/search/mission%20success/) — "Win Success Mission", ["mission complete" 검색](https://pixabay.com/sound-effects/search/mission%20completed/) |
| defeat | 작전 실패 | 2–4초 | × | ID만 | [trailer impact 검색](https://pixabay.com/sound-effects/search/trailer%20impact/) — 어둡고 긴 꼬리의 임팩트 |
| rank_stamp | 결과 화면 랭크(S/A/B/C) 도장 | 0.3–0.6초 | × | ID만 | [cinematic impact 검색](https://pixabay.com/sound-effects/search/cinematic%20impact/) — 짧고 단단한 것 |
| exp_gain | 경험치 바 차오름 | 0.5–1초 | × | ID만 | [data technology 검색](https://pixabay.com/sound-effects/search/data%20technology/) |
| level_up | 레벨업 | 1–2초 | × | ID만 | [mission completed 검색](https://pixabay.com/sound-effects/search/mission%20completed/) — SoundShelfStudio "Mission Complete Chime" |
| stat_up | 스테이터스 결정·훈련 상승 | 0.3–0.6초 | × | ID만 | [sci fi ui 검색](https://pixabay.com/sound-effects/search/sci%20fi%20ui/) |
| coop_rank_up | 코옵 랭크업 연출 | 1.5–3초 | × | ID만 | [epic victory 검색](https://pixabay.com/sound-effects/search/epic%20victory/) — 짧은 것 |

## 3. 투입·무전
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| heli_loop | 헬기 대기·투입 중 로터 | 5–15초 | ○ | ID만 | [military helicopter 검색](https://pixabay.com/sound-effects/search/military%20helicopter%20sound/) — 반복되는 로터·UH-1 녹음 |
| heli_flyby | 헬기 진입·이탈 | 4–8초 | × | ID만 | ["Helicopter Sound Effect" 로터 플라이바이](https://pixabay.com/sound-effects/city-helicopter-sound-effect-241421/), ["Helicopter" (SoundReality, 약 41초)](https://pixabay.com/sound-effects/city-helicopter-129052/) |
| radio_on | 무전 시작 (딸깍 + 잡음) | 0.2–0.5초 | × | ID만 | [walkie-talkie 검색](https://pixabay.com/sound-effects/search/walkie-talkie/) — "Walkie Talkie Sfx" |
| radio_off | 무전 끝 (로저 비프) | 0.3–0.7초 | × | ID만 | [walkie-talkie 검색](https://pixabay.com/sound-effects/search/walkie-talkie/) |
| radio_static | 무전 배경 잡음 | 2–6초 | ○ | ID만 | [military-radio 검색](https://pixabay.com/sound-effects/search/military-radio/) — "Military Radio Communication", [radio 검색](https://pixabay.com/sound-effects/search/radio/) — "Radio Static" (DRAGON-STUDIO) |
| rappel | 로프 강하 마찰음 | 1–2초 | × | ID만 | ["Rope Pull And Stretch Loop 2"](https://pixabay.com/sound-effects/film-special-effects-rope-pull-and-stretch-loop-2-202953/), [rope tugging 검색](https://pixabay.com/sound-effects/search/rope%20tugging/) — "Creaking Rope Foley" |
| rappel_land | 착지 (지금의 `Sfx.Step`) | 0.2–0.4초 | × | 합성음 대체 | [heavy-fall 검색](https://pixabay.com/sound-effects/search/heavy-fall/) — 짧은 것 |
| footstep | 전투 이동 발소리 (부츠·콘크리트) | 0.2–0.4초 ×4~6 | × | ID만 | ["Soldier steps"](https://pixabay.com/sound-effects/film-special-effects-soldier-steps-27399/), [boots footsteps 검색](https://pixabay.com/sound-effects/search/boots%20footsteps/) |
| footstep_hub | 본부 실내 발소리 | 0.2–0.4초 ×4~6 | × | ID만 | [walking-on-concrete 검색](https://pixabay.com/sound-effects/search/walking-on-concrete/) — "Concrete Footsteps" |

## 4. 총기 (선명하고 건조한 것) — 종류별 공통 소리 (총별 파일이 없을 때 쓰임)
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| gun_ar | 돌격소총 한 발 (어빌리티 단발 사격 자리 포함) | 0.3–0.8초 ×3 | × | 연결됨 · 합성음 대체 | ["AK-47 Sound Effect" (Sovetsky_Rastov72)](https://pixabay.com/sound-effects/film-special-effects-ak-47-sound-effect-267042/), [assault-rifle 검색](https://pixabay.com/sound-effects/search/assault-rifle/), [realistic gunshot 검색](https://pixabay.com/sound-effects/search/realistic%20gunshot/) |
| gun_smg | SMG | 0.2–0.6초 ×3 | × | 연결됨 | [submachine gun 검색](https://pixabay.com/sound-effects/search/submachine%20gun/) |
| gun_sg | 샷건 | 0.5–1초 ×2 | × | 연결됨 | ["Cinematic Shotgun with Reload" (DRAGON-STUDIO)](https://pixabay.com/sound-effects/cinematic-shotgun-with-reload-467480/), [shotgun blast 검색](https://pixabay.com/sound-effects/search/shotgun%20blast/) — Universfield "Shotgun Blast" (약 2초) |
| gun_pst | 권총 | 0.3–0.6초 ×3 | × | 연결됨 | ["pistol shot" (mrfriends, 선명한 파열음)](https://pixabay.com/sound-effects/pistol-shot-233473), [9mm pistol 검색](https://pixabay.com/sound-effects/search/9mm%20pistol/) |
| gun_dmr | 지정사수 소총 | 0.6–1.2초 ×2 | × | 연결됨 | [rifle-shot 검색](https://pixabay.com/sound-effects/search/rifle-shot/), [single gunshot 검색](https://pixabay.com/sound-effects/search/single%20gunshot/) |
| gun_suppressed | 소음기 장착 사격 | 0.2–0.4초 ×3 | × | 연결됨 | ["Silenced Submachine gun"](https://pixabay.com/sound-effects/silenced-submachine-gun-85329), ["Five seveN gun shot and silenced"](https://pixabay.com/sound-effects/five-seven-gun-shot-and-silenced-104500/), ["Silenced Gunshot"](https://pixabay.com/sound-effects/film-special-effects-silenced-gunshot-81063/) |
| weapon_swap | 二丁持ち 총 교체 | 0.3–0.6초 | × | ID만 | ["Gun Sounds" (탄창·장전·조작 소리)](https://pixabay.com/sound-effects/film-special-effects-gun-sounds-73540/), [gun cocking 검색](https://pixabay.com/sound-effects/search/gun%20cocking/) |

### 4-1. 총기별 소리 (위 표의 종류별 파일보다 먼저 쓴다)
- 파일 이름은 **총 이름의 모델 부분에서 영숫자만 소문자로** 딴다: K-417 → `gun_k417`, M-870 → `gun_m870`, Vector → `gun_vector`.
- 소음기를 단 버전은 뒤에 `_suppressed` 를 붙인다(예: `gun_mp5_suppressed`). 없으면 공통 `gun_suppressed`, 그것도 없으면 그 총의 소리.
- 총별 파일이 없으면 종류별 파일(`gun_ar` / `gun_sg` / `gun_smg` / `gun_pst` / `gun_dmr`)로 넘어간다. 그래서 종류별 파일을 먼저 하나씩 넣고, 총별 파일은 하나씩 늘려 가도 된다.
- 연결 상태: 전부 **연결됨** (전투에서 쏘는 순간 자동으로 고른다).
- 소리 성격은 R6 시즈의 같은 계열 총을 기준으로 적었다. 같은 종류 안에서도 무게·음높이·연사감이 달라야 구분된다.

**돌격소총 (AR)**
| 총 | 파일 이름 | 소리 성격 | Pixabay 후보 |
|---|---|---|---|
| K-417 突撃銃 | gun_k417 | 7.62mm. 가장 무겁고 낮게 "쾅", 반동이 느껴지는 두꺼운 소리 | [G3 검색](https://pixabay.com/sound-effects/search/g3/) (같은 7.62mm 계열), [rifle-shot 검색](https://pixabay.com/sound-effects/search/rifle-shot/) |
| AK47 突撃銃 | gun_ak47 | 거칠고 중저음이 굵은 "탕", 금속 울림이 남는 소리 | ["AK-47 Sound Effect" (Sovetsky_Rastov72)](https://pixabay.com/sound-effects/film-special-effects-ak-47-sound-effect-267042/) |
| AR15 突撃銃 | gun_ar15 | 5.56mm. 날카롭고 가벼운 "탁" 파열음 | ["AR15 rifle shot" (50야드 녹음 — 메아리 확인)](https://pixabay.com/sound-effects/ar15-rifle-shot-from-50-yards-away-98831/), [556 검색](https://pixabay.com/sound-effects/search/556/) — "M16a1ar-15 Gunshot 556" |
| M16 突撃銃 | gun_m16 | AR15과 같은 5.56mm지만 더 단단하고 짧게 끊김 (점사 느낌) | [M16 검색](https://pixabay.com/sound-effects/search/m16/) — "M16 Rifle Shots", "BurstFire.wav" |
| SCAR 突撃銃 | gun_scar | 7.62mm. K-417보다 짧고 펀치감 있게 | [SCAR 검색](https://pixabay.com/sound-effects/search/scar/), [assault-rifle 검색](https://pixabay.com/sound-effects/search/assault-rifle/) |

**산탄총 (SG)**
| 총 | 파일 이름 | 소리 성격 | Pixabay 후보 |
|---|---|---|---|
| M-870 / M870 散弾銃 (같은 총, 같은 파일) | gun_m870 | 펌프액션. 굵은 "쿵" + 펌프를 당기는 철컥 꼬리 | ["Cinematic Shotgun with Reload" (DRAGON-STUDIO, 발사 + 펌프)](https://pixabay.com/sound-effects/cinematic-shotgun-with-reload-467480/), [shotgun 검색](https://pixabay.com/sound-effects/search/shotgun/) — "Pump Action Shotgun Cycle" |
| DB 二連散弾銃 | gun_db | 2연발. 가장 크고 무거운 한 방, 철컥 없이 둔탁하게 끝남 | [double-barrel 검색](https://pixabay.com/sound-effects/search/double-barrel/) — "Double-barrel Open"(꺾는 소리) + [shotgun blast 검색](https://pixabay.com/sound-effects/search/shotgun%20blast/) |
| SAIGA 散弾銃 | gun_saiga | 반자동. 펌프 소리 없이 빠르고 짧은 "쾅" | [semi automatic shotgun 검색](https://pixabay.com/sound-effects/search/semi%20automatic%20shotgun/) — "Saiga 12" |

**기관단총 (SMG)**
| 총 | 파일 이름 | 소리 성격 | Pixabay 후보 |
|---|---|---|---|
| V-9 短機関銃 | gun_v9 | 9mm. 가볍고 덜거덕거리는 "타타" | [uzi 검색](https://pixabay.com/sound-effects/search/uzi/) — ["Uzi burst"](https://pixabay.com/sound-effects/film-special-effects-uzi-burst-100582/) |
| MP5 短機関銃 | gun_mp5 | 9mm. 정돈되고 기계적인 "탁탁", 금속 작동음이 살짝 | [MP5 검색](https://pixabay.com/sound-effects/search/mp5/) — "Submachine Mp5 Weapon" |
| MP7 短機関銃 | gun_mp7 | 소구경. 높고 짧게 튀는 "팟팟" | [SMG 검색](https://pixabay.com/sound-effects/search/smg/) — MP7 녹음 (에어소프트 녹음이면 [submachine 검색](https://pixabay.com/sound-effects/search/submachine/)에서 대체) |
| P90 短機関銃 | gun_p90 | 소구경. 가늘고 빠른 연사 | [submachine 검색](https://pixabay.com/sound-effects/search/submachine/) — "FN P90" 버스트, [FN 검색](https://pixabay.com/sound-effects/search/fn/) — P90 (Sovetsky_Rastov72) |
| Vector 短機関銃 | gun_vector | 매우 빠른 연사, 짧고 단단한 펀치 | [automatic gun 검색](https://pixabay.com/sound-effects/search/automatic%20gun/) |

**기타**
| 총 | 파일 이름 | 소리 성격 | Pixabay 후보 |
|---|---|---|---|
| MX50 精密射撃銃 (DMR) | gun_mx50 | 단발. 무겁고 길게 울리는 "탕—" | [sniper 검색](https://pixabay.com/sound-effects/search/sniper/) — "Sniper Rifle Firing Shot" |
| P-12 拳銃 | gun_p12 | 9mm. 선명하고 짧은 "탁" | ["Glock 19 Handgun Pistol"](https://pixabay.com/sound-effects/film-special-effects-glock-19-handgun-pistol-30812/), ["pistol shot" (mrfriends)](https://pixabay.com/sound-effects/pistol-shot-233473) |

## 5. 피격·쓰러짐
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| hit_body | 명중 | 0.1–0.3초 ×3 | × | 연결됨 | [bullet 검색](https://pixabay.com/sound-effects/search/bullet/) — 몸 탄착(flesh/impact) 계열 |
| hit_headshot | 헤드샷(크리티컬) | 0.3–0.6초 | × | 연결됨 | [bullet 검색](https://pixabay.com/sound-effects/search/bullet/) + [cinematic impact 검색](https://pixabay.com/sound-effects/search/cinematic%20impact/) 을 겹쳐 무겁게 |
| hit_miss | 빗나감·도탄 | 0.3–0.8초 ×3 | × | 연결됨 | ["bullet ricochet"](https://pixabay.com/sound-effects/bullet-ricochet-108110/), [ricochet 검색](https://pixabay.com/sound-effects/search/ricochet/) — Kebabus16 "bullet ricochet on a metal surface 1–3" |
| hit_wall_pierce | 벽 관통 사격 | 0.2–0.4초 | × | ID만 | [wood break 검색](https://pixabay.com/sound-effects/search/wood%20break/) — 짧은 파열음 |
| hit_reinforced | 강화벽에 막힘 | 0.2–0.4초 | × | ID만 | [bullet hitting metal 검색](https://pixabay.com/sound-effects/search/bullet%20hitting%20metal/) — "Bullet Hit Metal" |
| hit_shield | 방패에 막힘 | 0.2–0.4초 ×2 | × | 연결됨 | [bullet hitting metal 검색](https://pixabay.com/sound-effects/search/bullet%20hitting%20metal/) — Vadim_Makes_Sound "Impact • Metal Bullet Hit Clang" |
| down | 쓰러짐 | 0.4–0.8초 ×2 | × | 연결됨 | ["body fall" (thekids15, 무거운 낙하)](https://pixabay.com/sound-effects/film-special-effects-body-fall-195147/), [body-fall 검색](https://pixabay.com/sound-effects/search/body-fall/) — "Heavy Body Fall" |
| kill_confirm | 적 제압 확정 | 0.2–0.4초 | × | ID만 | [hud 검색](https://pixabay.com/sound-effects/search/hud/) — 낮은 확인음 |
| ally_down | 아군 사망 경고 | 0.5–1초 | × | 연결됨 | [military signal 검색](https://pixabay.com/sound-effects/search/military%20signal/) — "military alarm" |
| revive | 제세동기 부활 | 0.8–1.5초 | × | ID만 | ["defibrillator" (충전 + 충격)](https://pixabay.com/sound-effects/defibrillator-6961/) |

## 6. 벽·폭발
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| wall_break | 벽 파괴 (지금의 `SfxNoise.Boom`) | 0.8–1.5초 ×2 | × | 합성음 대체 | [wall smash 검색](https://pixabay.com/sound-effects/search/wall%20smash/) — floraphonic "Wood Smash 1–5", [wall break 검색](https://pixabay.com/sound-effects/search/wall%20break/) — "Wall Impact Debris" |
| wall_break_shotgun | 샷건으로 벽 부수기 | 0.5–1초 | × | ID만 | [wood break 검색](https://pixabay.com/sound-effects/search/wood%20break/) — "Breaking Wood" (DRAGON-STUDIO) |
| explosion | 수류탄·폭파 | 1.5–3초 ×2 | × | ID만 | ["Grenade Explosion 14" (DavidDumaisAudio)](https://pixabay.com/sound-effects/grenade-explosion-14-190266/), ["grenade sound effect" (핀·바운스·폭발·파편)](https://pixabay.com/sound-effects/film-special-effects-055890-grenade-sound-effectmp3-89751/) |
| hammer | CRUSHER 해머 (지금의 `SfxNoise.Hit`) | 0.5–1초 | × | 합성음 대체 | [wall demolition 검색](https://pixabay.com/sound-effects/search/wall%20demolition/) |
| launcher | FLARE 런처 발사 | 0.6–1.2초 | × | 연결됨 | [grenade-launcher 검색](https://pixabay.com/sound-effects/search/grenade-launcher/) |
| thermite_burn | IGNIS 테르밋 연소 | 2–4초 | ○ | ID만 | [fuse 검색](https://pixabay.com/sound-effects/search/fuse/) — "fuse_burning_48khz", [electric arc 검색](https://pixabay.com/sound-effects/search/electric%20arc%20sound/) 을 겹쳐 지글거림 |
| breach_place | 브리칭 차지 부착 | 0.3–0.5초 | × | ID만 | [gun-cock 검색](https://pixabay.com/sound-effects/search/gun-cock/) — 금속 딸깍 |
| breach_beep | 기폭 직전 비프 | 0.5–1초 | × | ID만 | [beeps 검색](https://pixabay.com/sound-effects/search/beeps/) |
| reinforce | 방어 준비 벽 강화 | 0.8–1.5초 | × | ID만 | [industrial 검색](https://pixabay.com/sound-effects/search/industrial/) — 금속판·쇠 부딪힘 |

## 7. 가젯·능력
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| throw | 던지기 휙 | 0.2–0.4초 | × | ID만 | [grenades 검색](https://pixabay.com/sound-effects/search/grenades/) |
| grenade_bounce | 바닥에 떨어져 구름 | 0.3–0.8초 | × | ID만 | ["grenade sound effect"](https://pixabay.com/sound-effects/film-special-effects-055890-grenade-sound-effectmp3-89751/) 의 바운스 부분을 잘라 사용 |
| flashbang | 섬광탄 폭발 | 0.5–1초 | × | ID만 | ["Stun grenade - flashbang" (ejah_music)](https://pixabay.com/sound-effects/stun-grenade-flashbang-458535/) |
| flash_ring | 섬광 후 이명 | 2–4초 | × | ID만 | 위 flashbang 파일의 이명 부분을 잘라 사용 |
| smoke | 연막 분출 | 1.5–3초 | × | ID만 | ["Smoke Grenade Sound Effect" (Alice_soundz)](https://pixabay.com/sound-effects/smoke-grenade-sound-effect-224182/) |
| claymore_place | 클레이모어 설치 | 0.3–0.5초 | × | ID만 | [gun cocking 검색](https://pixabay.com/sound-effects/search/gun%20cocking/) |
| claymore_trigger | 클레이모어 걸림 딸깍 | 0.1–0.3초 | × | ID만 | [gun-cock 검색](https://pixabay.com/sound-effects/search/gun-cock/) |
| drone_deploy | 드론 띄우기 | 0.5–1초 | × | ID만 | [quadcopter 검색](https://pixabay.com/sound-effects/search/quadcopter/) |
| drone_loop | 드론 이동 모터 | 2–5초 | ○ | ID만 | [quadcopter 검색](https://pixabay.com/sound-effects/search/quadcopter/) — "mini quadcopter flying loop", [drone fly 검색](https://pixabay.com/sound-effects/search/drone%20fly/) |
| drone_ping | 드론이 적 발견 | 0.3–0.6초 | × | ID만 | [tactical 검색](https://pixabay.com/sound-effects/search/tactical/) — "UI Tactical Scan Beep" |
| drone_destroyed | 드론 파괴 | 0.4–0.8초 | × | ID만 | [electric-shocks 검색](https://pixabay.com/sound-effects/search/electric-shocks/) + 작은 파열음 |
| camera_switch | 감시 카메라 화면 전환 | 0.2–0.5초 | × | ID만 | [computer data 검색](https://pixabay.com/sound-effects/search/computer%20data/) — "Glitch 1 - Computer data error" |
| camera_destroyed | 카메라 파괴 | 0.3–0.6초 | × | ID만 | [electric-shocks 검색](https://pixabay.com/sound-effects/search/electric-shocks/) — "Electrical Shock (Zap)" |
| emp | EMP | 0.8–1.5초 | × | ID만 | ["EMP Blast"](https://pixabay.com/sound-effects/musical-emp-blast-107780/), ["emp blast2"](https://pixabay.com/sound-effects/musical-emp-blast2-106415/) |
| heartbeat | 심장 박동 탐지 | 1–2초 | ○ | ID만 | [heartbeat 검색](https://pixabay.com/sound-effects/search/heartbeat/), [heartbeat loop 검색](https://pixabay.com/sound-effects/search/heartbeat%20loop/) |
| heal | SERUM 힐 | 0.8–1.5초 | × | ID만 | [medical 검색](https://pixabay.com/sound-effects/search/medical/?pagi=2) — "inject sound" + 낮은 확인음 |
| item_use | 의료키트·각성제·방패 등 (지금의 `SfxNoise.Ui`) | 0.3–0.6초 | × | 합성음 대체 | [medication 검색](https://pixabay.com/sound-effects/search/medication/?pagi=2) |
| shield_up | 방패 펼치기 | 0.3–0.6초 | × | ID만 | [shield 검색](https://pixabay.com/sound-effects/search/shield/) — "Steel Impact Hit" |
| shield_down | 방패 접기 | 0.3–0.6초 | × | ID만 | [shield 검색](https://pixabay.com/sound-effects/search/shield/) |
| gas | 독가스 (지금의 `SfxNoise.Gas`) | 0.5–1초 | × | 합성음 대체 | ["Gas Grenade" (압축 가스 분출)](https://pixabay.com/sound-effects/film-special-effects-gas-grenade-95255/) |
| fire_loop | 불붙은 지역 | 3–6초 | ○ | ID만 | [fuse 검색](https://pixabay.com/sound-effects/search/fuse/) |
| zap | 전기 충격·감전 (지금의 `SfxNoise.Zap`) | 0.3–0.6초 | × | 합성음 대체 | [electric-shocks 검색](https://pixabay.com/sound-effects/search/electric-shocks/) — "Electric Discharge" (DRAGON-STUDIO) |

## 8. 인질
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| hostage_secure | 인질 확보 (결박 풀기) | 0.5–1초 | × | ID만 | [rope tugging 검색](https://pixabay.com/sound-effects/search/rope%20tugging/) |
| extract | 탈출 지점 도착 | 1–2초 | × | ID만 | [mission success 검색](https://pixabay.com/sound-effects/search/mission%20success/) — 짧은 것 |

## 9. 본부
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| text_blip | 대사 글자 출력 블립 | 0.02–0.05초 | × | ID만 | [computer terminal 검색](https://pixabay.com/sound-effects/search/computer%20terminal/) — "Terminal" |
| choice | 대화 선택지 결정 | 0.1–0.2초 | × | ID만 | [ui click 검색](https://pixabay.com/sound-effects/search/ui%20click/) — SoundShelfStudio "Sci-Fi UI Click Sound" |
| purchase | 구매 결제 | 0.3–0.6초 | × | ID만 | [computer data 검색](https://pixabay.com/sound-effects/search/computer%20data/) — "Computer Data Entry" |
| equip | 파츠 장착·총 교체 철컥 | 0.2–0.5초 | × | ID만 | [cocking-rifle 검색](https://pixabay.com/sound-effects/search/cocking-rifle/) |
| gift | 선물 주기 | 0.4–0.8초 | × | ID만 | [completion 검색](https://pixabay.com/sound-effects/search/completion/) |
| cook | 炊事支援 조리 | 1.5–3초 | × | ID만 | Pixabay 검색어 "cooking sizzle" (미확인) |
| time_pass | 반나절 경과 (午前→午後) | 0.8–1.5초 | × | ID만 | [digital transition 검색](https://pixabay.com/sound-effects/search/digital%20transition/) |

## 10. 환경음 (루프)
| 파일 이름 | 쓰이는 곳 | 길이 | 루프 | 코드 연결 | Pixabay 후보 |
|---|---|---|---|---|---|
| amb_rain | 야간·비 작전 | 20–45초 | ○ | ID만 | [rainy night 검색](https://pixabay.com/sound-effects/search/rainy%20night/) — "Rainy Night Ambience Loop", [looping rain 검색](https://pixabay.com/sound-effects/search/looping%20rain/) |
| amb_wind | 야외 바람 | 20–40초 | ○ | ID만 | [night ambience 검색](https://pixabay.com/sound-effects/search/night%20ambience/) |
| amb_indoor | 건물 안 웅웅거림 | 20–40초 | ○ | ID만 | [room ambience 검색](https://pixabay.com/sound-effects/search/room%20ambience/) — StereogenicStudio "RoomTone" |
| amb_hub | 본부 형광등·기계음 | 20–40초 | ○ | ID만 | [industrial-ambience 검색](https://pixabay.com/sound-effects/search/industrial-ambience/) — SoundReality "Industrial Hum Ambience" |
