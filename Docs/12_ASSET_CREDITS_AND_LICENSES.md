# 사용 에셋·출처·라이선스

## 문서 범위

- 확인 기준일: 2026-08-10
- 확인 대상: 빌드에 포함된 `StartScene`, `GameplayScene`, `ResultScene`과 이들이 참조하는 프리팹·머티리얼·오디오·UI
- 외부 에셋은 원본 배포 페이지와 라이선스를 기록하고, 프로젝트 제작물은 저장소 내 위치를 연결한다.
- 원본 출처나 라이선스를 확인하지 못한 파일은 배포 전 확인 항목으로 분리한다.

## 외부 에셋

| 분류 | 에셋 및 원본 링크 | 제작자·배포자 | 라이선스 | 프로젝트 내 사용 |
|---|---|---|---|---|
| UI 사운드 | [Interface & Item Sounds Pack](https://www.fab.com/listings/78e31bcc-adfc-4816-8e10-609320deeeb1) | Daydream Sound | [Fab Standard License](https://www.fab.com/eula) | 메뉴·결과 화면 클릭음 `Click_04.wav`. [로컬 안내](../Assets/Audio/ThirdParty/DaydreamSound/InterfaceAndItemSounds/README.md) |
| 피격 사운드 | [Guns & Explosions Album - Bullet Impact 14.wav](https://freesound.org/people/OGsoundFX/sounds/423107/) | OGsoundFX | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | 총알 피격음. [로컬 파일](../Assets/Audio/ThirdParty/OGSoundFX/GunsAndExplosions/Bullet%20Impact%2014.wav) |
| 폭발 사운드 | [Guns & Explosions Album - Flare gun 5-2.wav](https://freesound.org/people/OGsoundFX/sounds/423109/) | OGsoundFX | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) | 자폭형 적 폭발음. [로컬 파일](../Assets/Audio/ThirdParty/OGSoundFX/GunsAndExplosions/Flare%20gun%205-2.wav) |
| UI 폰트 | [Oxanium](https://github.com/sevmeyer/oxanium) | The Oxanium Project Authors | [SIL Open Font License 1.1](https://scripts.sil.org/OFL) | 시작·결과 UI. [로컬 라이선스](../Assets/UI/StartMenu/Fonts/OFL.txt) |
| UI 폰트 | Liberation Sans | Google Corporation, Red Hat, Inc. | [SIL Open Font License 1.1](https://scripts.sil.org/OFL) | TextMesh Pro 게임 HUD. [로컬 라이선스](../Assets/TextMesh%20Pro/Fonts/LiberationSans%20-%20OFL.txt) |

### 표기용 크레딧

> `Bullet Impact 14.wav` and `Flare gun 5-2.wav` by OGsoundFX, licensed under CC BY 4.0.
>
> Oxanium by The Oxanium Project Authors, licensed under the SIL Open Font License 1.1.
>
> Liberation Sans font data copyright Google Corporation and Red Hat, Inc., licensed under the SIL Open Font License 1.1.

Fab 에셋은 완성된 게임에 포함해 사용할 수 있지만, 원본 WAV 묶음을 독립 에셋 팩이나 소스 파일 모음으로 재배포하지 않는다.

## 출처·라이선스 확인 필요

| 에셋 | 현재 사용 | 확인 상태 | 배포 전 조치 |
|---|---|---|---|
| [FreeWeaponSounds](../Assets/Audio/ThirdParty/FreeWeaponSounds/) | 권총·샷건·라이플 총성 각 3개, 총 9개 | 원본 배포 페이지와 라이선스 파일을 저장소에서 확인할 수 없음 | 구매·다운로드 기록에서 원본 링크와 라이선스를 확인한 뒤 본 문서에 추가한다. 확인 전 공개 배포본 포함을 보류한다. |
| [Footsteps Mini Sound Pack](../Assets/Audio/ThirdParty/FootstepsMiniSoundPack/) | 금속·수풀 발소리 각 5개, 총 10개 | 원본 배포 페이지와 라이선스 파일을 저장소에서 확인할 수 없음 | 원본 링크와 라이선스를 확인하거나 출처가 명확한 음원으로 교체한다. |
| [Sewing Machine Samples](../Assets/Audio/ThirdParty/SewingMachineSamples/) | 자폭형 적 경고음 1개 | 원본 배포 페이지와 라이선스 파일을 저장소에서 확인할 수 없음 | 원본 링크와 라이선스를 확인하거나 출처가 명확한 음원으로 교체한다. |
| [ZombieHorrorPackageFree](../Assets/Audio/ThirdParty/ZombieHorrorPackageFree/) | 근접·원거리 적 공격·피격 음성 12개 | 원본 배포 페이지와 라이선스 파일을 저장소에서 확인할 수 없음 | 원본 링크와 라이선스를 확인하거나 출처가 명확한 음원으로 교체한다. |

## 프로젝트 제작 에셋

아래 항목은 외부 에셋 팩을 그대로 포함한 것이 아니라 프로젝트용으로 제작·가공된 런타임 에셋이다. 제작 원본과 사용 도구의 이용 조건은 최종 배포 전에 별도로 보관한다.

| 분류 | 저장소 위치 | 사용 내용 |
|---|---|---|
| 무기 뷰모델 | [Pistol](../Assets/Art/Viewmodels/Pistol/), [Shotgun](../Assets/Art/Viewmodels/Shotgun/), [Rifle](../Assets/Art/Viewmodels/Rifle/) | 1인칭 권총·샷건·라이플 모델과 머티리얼 |
| 적 모델 | [HumanoidBlob](../Assets/Art/Enemies/HumanoidBlob/), [SuicideBacteriophage](../Assets/Art/Enemies/SuicideBacteriophage/) | 근접·원거리·자폭형 적 모델, 애니메이션, 프리팹 |
| 인게임 UI | [InGame UI](../Assets/UI/InGame/) | 크로스헤어와 무기 실루엣 |
| 결과 UI | [Result UI](../Assets/UI/Result/) | 결과 화면 배경, 패널, 버튼 텍스처 |
| 전투 VFX | [Muzzle Flash](../Assets/VFX/Weapons/MuzzleFlash/), [Impact Sparks](../Assets/VFX/Weapons/ImpactSparks/) | 총구 화염과 피격 스파크 텍스처·머티리얼 |

## 현재 빌드에서 제외된 외부 폴더

- 미사용 오디오와 Unity 템플릿 잔재는 로컬 `Archive/UnusedResources`로 분리했다. 보존·복원 규칙은 아래 `미사용 리소스 아카이브` 절을 따른다.
- 컨셉 이미지, Blender 제작 소스, 생성 중간물은 런타임 배포 에셋이 아니므로 이 문서의 사용 목록에 포함하지 않는다.

## 2026-08-11 공개 배포 감사

| 에셋 | 현재 확인 근거 | 상태 |
|---|---|---|
| [Grenade Sound FX](https://assetstore.unity.com/packages/p/grenade-sound-fx-147490) | Unity 메타데이터의 `productId: 147490`, `packageName: Grenade Sound FX`; Unity Asset Store 표준 EULA | 구매·다운로드 계정 기록 최종 확인 필요 |
| LP Sci-Fi Interior | `Assets/Art/Environment/ThirdParty/LPSciFiInterior`에 포함되어 게임 씬에서 사용 | 원본 상품 페이지와 구매·다운로드 기록 미확인 |
| `Fading Transmission.mp3`, `Iron Horizon.mp3`, `Iron Lung Protocol.mp3`, `Iron Pulse.mp3`, `Rust and Static.mp3`, `Rust Circuit.mp3`, `dooms day.mp3`, `The Last Stand of Valhalla.mp3` | `Assets/Audio/BGM`에서 게임 씬이 사용 | 제작자·원본 링크·라이선스 미확인 |
| FreeWeaponSounds, Footsteps Mini Sound Pack, Sewing Machine Samples, ZombieHorrorPackageFree | 위의 기존 감사 표와 동일 | 원본 링크·라이선스 미확인 |

Unity Asset Store 표준 EULA는 적법하게 취득한 비제한 에셋을 완성된 게임에 포함하여 배포하는 것을 허용하지만, 저장소의 메타데이터만으로 실제 취득 계정 기록까지 증명할 수는 없다. 미확인 항목은 공개 배포의 라이선스 리스크로 계속 추적하며, 현재 사용자가 승인한 공개 WebGL 배포를 자동으로 중단하는 규칙으로 사용하지 않는다.

## 미사용 리소스 아카이브

- 빌드 씬과 프로젝트 설정·Resources·문자열 참조를 검사해 미사용 리소스는 `Archive/UnusedResources`에 원래 상대 경로와 `.meta`를 함께 보존한다.
- 아카이브는 로컬 복구용이며 Git 커밋 대상이 아니다. 복원할 때는 에셋과 동일 이름의 `.meta`를 함께 되돌린다.
- 콘셉트 아트, 제작 원본, `BlenderSource/**`, `tmp/**`, TextMesh Pro Resources와 동적 생성 참조는 정리 대상에서 제외한다.
- 마지막 정적 GUID 의존성·Resources.Load·Addressables·AssetBundle·StreamingAssets 검사는 통과했으며 Unity 재임포트·Play Mode·WebGL 재검증은 별도 실행 항목이다.
