# GULAG Desktop UI Design Principles

## 목표와 전제

- 대상: 키보드와 마우스를 사용하는 16:9 데스크탑 전용 FPS
- 기준 해상도: 1920 x 1080
- Unity Canvas: `Scale With Screen Size`, Reference Resolution `1920 x 1080`, Match `0.5`
- 서체: `Tomorrow-SemiBold`를 모든 UI의 기본 서체로 사용한다.
- 핵심 목표: 전투 중 시선을 빼앗지 않으면서, 메뉴와 결과 화면에서는 현재 상태와 다음 행동을 즉시 이해하게 한다.

컨셉아트는 컴포넌트의 상대적 위계와 밀도, 정렬, 상태 표현을 승인하기 위한 시각 기준이다. 아래 픽셀 수치는 Unity 구현 단계의 RectTransform 목표값이며, 생성형 컨셉 이미지의 픽셀을 직접 재어 구현 규격으로 사용하지 않는다.

## 리서치에서 채택한 원칙

1. **읽을 수 있는 크기가 스타일보다 우선한다.** PC 1080p의 기본 텍스트는 실제 글자 몸통 높이 18px 이상을 하한으로 보고, HUD의 중요한 숫자와 메뉴 본문은 20-24px 이상으로 설계한다.
2. **명도 대비와 형태를 함께 사용한다.** 일반 텍스트와 중요한 비문자 표시는 4.5:1 이상, 큰 텍스트는 3:1 이상을 목표로 한다. 선택/활성 상태는 색만 바꾸지 않고 2px 외곽선, 배경 명도, 좌측 마커를 함께 바꾼다.
3. **게임 전체의 탐색 구조를 반복한다.** 제목 위치, 뒤로가기 위치, 기본 버튼 높이, 포커스 표현, 행 간격을 모든 메뉴 화면에서 동일하게 유지한다.
4. **플레이 중 중앙 시야를 보호한다.** 중앙에는 조준점과 짧은 전투 피드백만 둔다. 상태 정보는 주변 시야에서 형태와 밝기 변화로 감지할 수 있게 묶되, 서로 다른 정보 덩어리는 명확히 구분한다.
5. **정보는 중요도와 사용 빈도로 배치한다.** 즉시 판단이 필요한 조준/탄약/콤보는 강하게, 확인 빈도가 낮은 점수/스킬 상태는 작고 안정적으로 표시한다.
6. **상태 변화는 즉시 보이되 장식 애니메이션은 피한다.** 입력, 선택, 재장전, 탄약 부족, 콤보 소멸처럼 의미 있는 변화에만 밝기·크기·짧은 이동을 사용한다.
7. **해상도 차이는 비균일 스트레치로 해결하지 않는다.** 기준 해상도를 두고 전체 UI를 함께 스케일하며, 가장자리 요소는 각 모서리에 앵커링하고 중앙 전투 영역은 비워 둔다.

## 1920 x 1080 디자인 토큰

### 공간과 크기

| 항목 | 규격 |
| --- | ---: |
| 기본 그리드 | 8px |
| 화면 안전 여백 | 좌우 64px, 상하 48px |
| 패널 내부 여백 | 32px |
| 패널 사이 간격 | 24px |
| 기본 버튼 높이 | 56px |
| 주요 버튼 높이 | 64px |
| 설정 기본 행 높이 | 64px |
| Result 런 통계 행 높이 | 80px |
| Result 랭킹 행 높이 | 48px |
| HUD 보조 행 높이 | 48px |
| HUD 활성 무기 행 높이 | 72px |
| 외곽선 | 기본 1px, 포커스 2px |
| 코너 컷 | 소형 6px, 패널 12px |

### 타이포그래피

| 용도 | 크기 | 비고 |
| --- | ---: | --- |
| 화면 제목 | 48px | Tomorrow SemiBold, 대문자 |
| 패널 제목 | 28px | 대문자 |
| 기본 본문/버튼 | 22px | 18px 미만 금지 |
| 보조 라벨 | 18px | 중요 정보에는 사용하지 않음 |
| HUD 타이머 | 40px | 고정폭 느낌의 숫자 정렬 |
| HUD 점수/탄약 핵심값 | 32px | 주변 시야에서 형태 식별 |
| Result 최종 점수 | 72px | 화면당 하나만 사용 |

### 색상

| 역할 | 색상 | `#081014` 위 명도 대비 |
| --- | --- | ---: |
| Panel background | `#081014` / 88% | - |
| Primary text | `#F4F7F8` | 17.82:1 |
| Secondary text | `#B9C3C8` | 10.69:1 |
| Focus / interactive | `#32D6E6` | 10.87:1 |
| Danger | `#FF6262` | 6.56:1 |
| Rifle | `#4B95FF` | 6.43:1 |
| Shotgun | `#F6C344` | 11.70:1 |

색상은 의미를 강화하는 보조 채널이며, 상태 구분을 색상에만 의존하지 않는다.

## 화면별 설계

### StartMenuCanvas / Settings

- 4/12 컬럼은 게임 타이틀과 세로 메뉴, 7/12 컬럼은 설정 패널, 1/12 컬럼은 호흡 공간으로 사용한다.
- PLAY / SETTINGS / QUIT 버튼은 모두 320 x 56px로 고정한다.
- 설정 패널은 최대 880 x 560px로 제한하고, 각 설정 행은 64px로 통일한다.
- 선택 상태는 시안, 2px 테두리, 좌측 4px 마커를 함께 사용한다.
- BACK은 패널 하단 왼쪽, 기본 버튼 규격으로 배치한다.

### CrosshairCanvas / Gameplay HUD

- 중앙 50% 영역에는 조준점 이외의 고정 UI를 두지 않는다.
- 타이머는 상단 중앙, 점수와 콤보는 우상단, 무기와 스킬은 우하단에 고정한다.
- 우측 HUD 레일 너비는 344px를 넘지 않는다.
- 비활성 무기 행 48px, 활성 무기 행 72px, 스킬 행 48px로 제한한다.
- 탄약 부족은 숫자 색상, 아이콘, 짧은 `EMPTY` 라벨을 함께 사용한다.
- 픽업 토스트는 1.5초 이내에 사라지고 중앙 조준점과 겹치지 않는다.

### ResultCanvas

- 상단은 종료 상태와 최종 점수를 한 줄의 공통 헤더 영역으로 묶는다.
- 본문은 RUN DATA / LOADOUT KILLS / GLOBAL RANKING의 3개 컬럼으로 구성한다. 좌·중 패널은 같은 높이를 쓰고, 우측은 랭킹 패널과 제출 스트립을 합친 컬럼 전체가 같은 하단 기준선에 맞도록 한다.
- 아이콘과 큰 수치를 빠르게 훑는 런/로드아웃 데이터 행은 80px, 밀도 높은 랭킹 행은 48px, 패널 제목 영역은 56px로 통일한다. 모두 8px 그리드의 배수다.
- 주 행동 RETRY는 우하단, 보조 행동 MAIN MENU는 좌하단에 배치한다.
- 사용자 입력과 SUBMIT SCORE는 랭킹 패널 바로 아래에 붙여 맥락을 유지한다.

## 피해야 할 패턴

- 18px보다 작은 중요한 텍스트
- 전체 화면을 둘러싸는 두꺼운 장식 프레임
- 모든 요소에 시안 글로우 적용
- 색상만으로 활성/비활성 상태 표현
- 중앙 조준 영역의 고정 토스트나 큰 숫자
- 화면별로 달라지는 버튼 높이, 패널 여백, 제목 위치
- 배경 이미지 위에 백플레이트 없이 직접 놓인 중요한 텍스트

## 참고 자료

- Microsoft Xbox Accessibility Guideline 101, Text display: https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101
- Microsoft Xbox Accessibility Guideline 102, Contrast: https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102
- Microsoft Xbox Accessibility Guideline 112, UI navigation: https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/112
- Microsoft Xbox Accessibility Guideline 113, UI focus handling: https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/113
- Unity Manual, Canvas Scaler: https://docs.unity3d.com/Manual/script-CanvasScaler.html
- Nielsen Norman Group, Ten Usability Heuristics: https://www.nngroup.com/articles/ten-usability-heuristics/
- Game Developer, Perceiving without looking: Designing HUDs for peripheral vision: https://www.gamedeveloper.com/design/perceiving-without-looking-designing-huds-for-peripheral-vision
