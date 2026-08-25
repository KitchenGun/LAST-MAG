# LAST MAG

LAST MAG은 데스크톱 브라우저에서 플레이하는 1인칭 무한 생존 슈팅 게임이다. 플레이어는 클래스를 선택하고 고정 로드아웃으로 계속 등장하는 적을 처치하며, 탄약 회수·무기 전환·스킬·콤보를 조합해 최고 점수를 노린다.

- 장르: 1인칭 무한 생존 슈팅
- 플랫폼: 데스크톱 WebGL (Chrome·Edge)
- 엔진: Unity 6000.3.21f1 / URP 17.3.0
- 현재 공개 버전: `0.1.26`
- 공개 플레이: [LAST MAG WebGL](https://kitchengun.github.io/wiki/games/last-mag/)

## 플레이 구성

### 클래스

매 판 시작 화면에서 하나의 클래스를 선택한다. 모든 클래스는 같은 체력·이동 속도를 사용하며, `주무기 + 권총 + 고유 스킬` 조합으로 고정된다.

| 클래스 | 주무기 | 권총 | 스킬 |
| --- | --- | --- | --- |
| `GRENADIER` | 라이플 | 권총 | 수류탄 |
| `ENGINEER` | 샷건 | 권총 | 로켓 |
| `SNIPER` | DMR | 권총 | 불릿타임 |

- 척탄병: 수류탄을 무장한 뒤 좌클릭으로 투척한다. 4.5초 후 폭발하며 90 피해·반경 5m·자기 피해 45를 적용한다.
- 공병: 로켓을 무장한 뒤 좌클릭으로 발사한다. 150 피해·반경 4m·자기 피해 75를 적용한다.
- 저격수: DMR과 관통 조준을 사용하고, 불릿타임으로 5초 동안 세계 시간을 35%로 낮춘다. 플레이어 입력은 실시간으로 유지된다.

### 조작

| 동작 | 입력 |
| --- | --- |
| 이동 | `WASD` |
| 시점 | 마우스 이동 |
| 사격 | 좌클릭 |
| 점프 | `Space` |
| 주무기 / 권총 | `1` / `2` |
| 클래스 스킬 | `F` |
| 설정·일시정지 | `Esc` 또는 창 포커스 이탈 |

게임 중 설정 패널이 열리면 전투, AI, 투사체, 오디오, 생존 점수, 콤보, 체력 회복, 스킬 타이머가 멈춘다. `BACK` 또는 `Esc`로만 재개하며, 감도·마스터 볼륨·줌 방식 변경은 즉시 저장된다.

## 전투 규칙

- 재장전·탄창·예비 탄약은 사용하지 않는다. 발사할 때 현재 무기의 보유 탄약이 1발 차감된다.
- 시작/최대 보유량은 권총 `15`, 샷건 `8`, 라이플 `50`, DMR `15`다.
- 탄약 상자는 적 사망 시 50% 확률로 생성된다. 생성된 상자 중 클래스 주무기 60%, 권총 40%로 선택된다.
- 획득량은 권총 `5`, 샷건 `3`, 라이플 `20`, DMR `5`다. DMR은 라이플과 파란색을 공유하지만 별도 탄약 슬롯과 DMR 실루엣을 사용한다.
- 적은 자폭형, 일반 근거리형, 원거리형 3종이다. 시간에 따라 동시 적 수와 생성 압박이 증가하며, 5분 기준 목표 동시 적 수는 108마리다.

### 무기 기준값

| 무기 | 피해 | 발사 속도 | 헤드샷 | 확산 |
| --- | ---: | ---: | ---: | ---: |
| 권총 | 30 | 6.75발/초 | ×2 | 0.35° |
| 샷건 | 펠릿당 12 (8펠릿) | 1.1발/초 | ×2 | 5° |
| 라이플 | 15 | 11발/초 | ×2 | 0.75° |
| DMR | 60→40→20 관통 피해 | 5.25발/초 | ×2 | 조준 0°, 힙파이어 1.5° |

## 점수와 콤보

처치 점수는 합산 방식으로 계산한다.

`기본 점수 + 헤드샷 30 + 스왑킬 70 + 콤보 보너스 10 × (콤보 - 1) + 스킬 보너스 2 × 기본 점수`

- 기본 점수: 자폭형 `50`, 근거리형 `70`, 원거리형 `100`
- 스킬 처치는 기본 점수의 3배가 적용된다.
- 스왑킬은 직접 처치 후 2초 안에 다른 총기로 직접 처치할 때만 적용된다.
- 콤보는 상한 없이 증가하고 플레이어 귀속 처치마다 5초로 갱신된다.
- 생존 점수는 초당 1점이며 콤보 보너스를 적용하지 않는다.
- 자연 자폭·비귀속 폭발은 점수와 콤보를 지급하지 않는다.

## HUD·설정

- 우측 상단: 누적 점수
- 좌측 중앙: `COMBO xN`과 5초를 나타내는 탄환 5개
- 중앙 하단: 처치 점수 피드
- 현재 무기·보유 탄약·스킬 상태·생존 시간·체력
- DMR 조준 시 FOV 45와 비네트, 관통 탄도 표시
- 설정값은 `PlayerPrefs`로 저장한다.
  - 마우스 감도: `0.00~1.00` (기본 `1.00`)
  - 마스터 볼륨: `0~100%` (기본 `100%`)
  - 줌: `TOGGLE` / `HOLD` (기본 `TOGGLE`)

UI는 1920×1080 기준의 중앙 16:9 안전 프레임을 사용하며 4:3~21:9 화면비에서 비율을 유지한다.

## 기술 구조

주요 씬은 다음 세 개다.

- `Assets/Scenes/StartScene.unity`: 클래스 선택, 설정, 시작 화면
- `Assets/Scenes/GameplayScene.unity`: 아레나 전투, 적 스폰, HUD, 일시정지
- `Assets/Scenes/ResultScene.unity`: 결과, 개인 최고 기록, 랭킹 제출

핵심 런타임 코드는 `Assets/Scripts` 아래에 있다.

- `Player`: 이동·시점·사격·무기·입력
- `Combat`: 적 체력, 탄약, 풀링, 충돌·VFX
- `Enemies`: 적 AI, 공격, 스폰, 투사체
- `Systems`: 시간, 설정, 점수, 결과 데이터
- `UI`: 시작·게임 HUD·일시정지·결과 화면
- `Audio`: BGM, 공간음, 발소리, 경고·효과음

108마리 스트레스 구간을 위해 적 종류별 36개를 선생성하고 오브젝트 풀을 재사용한다. 공간음은 24개 보이스와 중요도 큐를 사용하며, 일반 발소리는 거리·초당 예산으로 제한한다.

고주사율·고폴링 마우스 입력은 프레임 독립 회전, 최대 각속도 제한, 스파이크 폐기 로직을 사용한다. Development Build에서는 다음 진단 로그를 확인할 수 있다.

`[Mouse Input] maxDelta=... raw=... applied=... spikeDrops=...`

## 빌드·배포

Unity MCP로 WebGL Release를 다음 경로에 생성한다.

`Builds/WebGLRelease_<version>`

현재 `0.1.26` 빌드는 Unity MCP에서 성공했으며, `index.html`, `.data.unityweb`, `.framework.js.unityweb`, `.loader.js`, `.wasm.unityweb` 파일을 Wiki의 `public/games/last-mag/`에 배포한다. GitHub Pages Action이 성공하면 위 공개 링크에서 확인할 수 있다.

## 검증 현황

- Unity MCP EditMode 테스트: `22/22` 통과
- GameplayScene 참조 검증: 오류 없음
- Unity MCP WebGL Release 0.1.26: 성공, 빌드 오류 0
- GitHub Pages Action: 성공
- 공개 HTML 및 WebGL 런타임 파일: HTTP 200
- 실제 Play Mode 전투 회귀 및 브라우저 조작 테스트: `not_run`

## 최근 변경

- `29dce33` `feat: add gameplay startup countdown` — 3초 카운트다운, 입력·사격·점수 시작 동기화
- `4a8081f` `art: update AssetScene weapon display` — 무기 전시 배치 갱신
- `9bb1aba` `chore: bump Last Mag WebGL build version` — 제품 버전 `0.1.26`
- `ba13bd0` `feat: publish Last Mag WebGL build 0.1.26` — 공개 WebGL 경로 교체

## 문서

- [콘셉트 결정](Docs/00_CONCEPT_DECISIONS.md)
- [코어 루프와 한 판 구조](Docs/02_CORE_LOOP_AND_RUN.md)
- [전투·무기·탄약](Docs/04_COMBAT_WEAPONS_AMMO.md)
- [적과 난이도](Docs/05_ENEMIES_AND_DIFFICULTY.md)
- [점수·콤보·랭킹](Docs/07_SCORE_COMBO_RANKING.md)
- [UI·UX와 피드백](Docs/08_UI_UX_FEEDBACK.md)
- [시각·오디오 방향](Docs/09_VISUAL_AUDIO_DIRECTION.md)
- [프로토타입 범위](Docs/10_PROTOTYPE_SCOPE.md)
- [밸런스와 플레이테스트](Docs/11_BALANCE_PLAYTEST.md)
- [에셋 크레딧·라이선스](Docs/12_ASSET_CREDITS_AND_LICENSES.md)
