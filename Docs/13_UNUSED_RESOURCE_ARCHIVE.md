# 미사용 리소스 분리 명세

## 처리 원칙

- 처리일: 2026-08-10
- 빌드 씬 `StartScene`, `GameplayScene`, `ResultScene`의 재귀 GUID 의존성과 프로젝트 설정, `Resources`, 코드의 문자열·런타임 생성 참조를 함께 검사했다.
- `Assets/Art/Concept/**`와 UI `Concepts` 폴더는 검사·이동 대상에서 제외했다.
- 삭제하지 않고 `Archive/UnusedResources`에 기존 상대 경로와 `.meta`를 함께 보존했다.
- `Archive/`는 로컬 복구용이며 Git 커밋 대상에서 제외한다.

## 분리한 항목

| 원래 위치 | 분리 수량 | 용량 | Assets에 보존한 사용 항목 |
|---|---:|---:|---|
| `Assets/Free Pack` | WAV 49개 | 307.32MB | `Bullet Impact 14.wav`, `Flare gun 5-2.wav` |
| `Assets/Audio/ThirdParty/DaydreamSound/InterfaceAndItemSounds` | WAV 116개 | 33.87MB | `Click_04.wav`, 출처·라이선스 `README.md` |
| `Assets/Footsteps Mini Sound Pack` | WAV 80개 | 15.54MB | `MetalSteps` 5개, `Undergrowth_Mono` 5개 |
| `Assets/Sewing_Machine_(Samples)` | WAV 9개 | 15.97MB | `Sewing-Machine_Needle-Shift_01-01.wav` |
| `Assets/ZombieHorrorPackageFree` | WAV 32개 | 10.72MB | Zombie01·Zombie03의 Attack/Hurt 각 3개, 총 12개 |
| Unity 템플릿 잔재 | `SampleScene`, `Readme.asset`, `TutorialInfo` | 0.23MB | 없음 |

- 분리된 WAV: 286개
- 전체 보관소 크기: 약 383.65MB

## 이동하지 않은 미사용·보류 항목

- `Assets/Art/Enemies/Bacteriophage/**`: 현재 빌드 미참조지만 구형 모델 또는 제작 원본일 가능성이 있어 보존했다.
- `M_VM_*_Grey.mat` 3개와 `T_UI_Result_FullOverlay.png`: 미참조 변형이지만 제작 원본으로 보존했다.
- `T_UI_Input_Keyboard_R.png`: 현재 미참조지만 기존 보존 결정에 따라 유지했다.
- `BlenderSource/**`, `tmp/**`: Unity 런타임 import 범위 밖이므로 이동하지 않았다.
- `Assets/Settings/**`, TextMesh Pro Resources, 동적 생성에 사용되는 스크립트·셰이더·씬 계층 이름은 정적 씬 참조가 없어도 런타임 필수이므로 보존했다.

## 복원 방법

`Archive/UnusedResources` 아래의 파일을 동일한 상대 경로로 프로젝트 루트에 되돌리면 된다. 에셋 파일과 같은 이름의 `.meta`를 반드시 함께 복원한다.

## 검증 상태

- 정적 GUID 의존성 검사: 통과
- `Resources.Load`, Addressables, AssetBundle, StreamingAssets 검색: 직접 사용 없음
- 프로젝트 설정·TMP Resources·문자열 씬/셰이더/Input Action·프리팹 전이 의존성 보존: 확인
- Unity 재임포트·Console·Play Mode·WebGL 빌드: `not_run`
