# 에디터 메뉴 (MergeBullet)

씬·프리팹·설정을 손으로 만들지 않고 메뉴(에디터 스크립트)로 만든다. 무엇을 어떻게 구성했는지가 코드에 남고, 다시 만들 수 있다.

- 코드: `Assets/_Project/Scripts/Authoring/Editor/` (`ArenaSetupMenu`, `LocalizationSetupMenu`, `AndroidSetupMenu`)
- **Setup**: 프로젝트를 처음 구성할 때 위에서부터 한 번씩. 이미 있는 것은 대부분 다시 쓰거나 건너뛴다
- **Update**: 데이터를 바꾼 뒤 다시 돌리는 것

## Setup

| 메뉴 | 하는 일 | 열어 둘 씬 |
|---|---|---|
| Arena SubScene | 아레나 SubScene(ArenaAuthoring + 벽), 총알 프리팹·머티리얼 | Game |
| Player | 플레이어 프리팹을 SubScene에 배치 | Game |
| Enemy | 기본 적(사각형) 프리팹을 Arena에 연결 | Game |
| XP Gem | 경험치 젬 프리팹을 적에 연결 | Game |
| Boss | 보스 프리팹과 보스 스포너 연결 | Game |
| Max Level Effect | 레벨 4 폭발 프리팹 | Game |
| Effects | 파티클·선 연출 프리팹과 EffectPresenter | Game |
| Boss Telegraph | 보스 돌진 예고 띠(BossTelegraph)와 머티리얼 | Game |
| HUD | 게임 HUD(UIDocument, PanelSettings, 테마) | Game |
| Lobby Scene And Build List | 로비 씬을 만들고 빌드 씬 목록을 로비 → 게임 순으로 | 아무거나 |
| Android Player Settings | 패키지 이름, 가로 화면, IL2CPP ARM64 | 아무거나 |

## Update

| 메뉴 | 언제 | 하는 일 |
|---|---|---|
| Arena Walls And Floor | Arena의 크기·통로·문 설정을 바꾼 뒤 (Game 씬에서) | 가운데 맵 둘레 벽, 각 벽 중앙의 부서지는 문, 벽 바깥을 두르는 통로와 바깥 테두리 벽, 바닥 격자를 다시 만들고 카메라 따라가기 연결 |
| Enemy Tiers | 적 등급 수치(`ArenaSetupMenu.Tiers`)를 바꾼 뒤 | 삼각형·사각형·오각형·육각형 적 프리팹과 정다각형 메시를 만들고 스폰 테이블 교체, 보스는 8각형으로 |
| Localization From CSV | `Localization/ui_strings.csv`를 고친 뒤 | 언어·문자열 표·언어별 폰트 표를 CSV에 맞추고 빠진 번역 검사 |

## 정리한 것

- `Setup Lobby And Game Scenes`의 SampleScene → Game 이전 부분은 한 번 쓰고 끝난 일이라 지웠다. 로비 씬 만들기와 빌드 씬 목록만 `Lobby Scene And Build List`로 남겼다.
