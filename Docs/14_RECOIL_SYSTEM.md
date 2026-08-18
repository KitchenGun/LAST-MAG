# Wolfenstein형 반동 시스템 현행 구현

> 방향: 실제 조준 반동은 제어 가능하게 유지하고, 강한 무게감은 Viewmodel과 짧은 Fire Impulse가 담당한다.

## 1. 시스템 구성

| 구분 | 소유 코드 | 명중 방향 영향 |
| --- | --- | --- |
| Aim Recoil | `FirstPersonController` | 영향 있음 |
| Fire Impulse | `FirstPersonController` | 영향 없음 |
| 총기 Viewmodel 반동 | `WeaponViewmodelController` | 영향 없음 |
| 로켓 런처 반동 | `PlayerSkillController` | 영향 없음 |
| 피격 Aim Punch | `FirstPersonController` | 영향 없음 |

```text
Aim Pitch = Mouse Pitch - Aim Recoil Pitch
Aim Yaw   = Mouse Yaw + Aim Recoil Yaw

Visual Camera Pitch = Aim Pitch - Fire Impulse Pitch - Hit Aim Punch Pitch
Visual Camera Yaw   = Aim Recoil Yaw + Fire Impulse Yaw + Hit Aim Punch Yaw
Visual Camera Roll  = Fire Impulse Roll

Projectile Direction = Aim Forward + Weapon Spread
```

Fire Impulse와 피격 Aim Punch가 카메라를 움직여도 발사 Ray는 별도로 계산한 Aim 방향을 사용한다.

## 2. Aim Recoil 프로필

아래 값은 코드 기본값과 `PF_Player` 직렬화 값이 일치하는 현재 기준이다. 회복 시작 시간은 발사 시점부터 잰다.

| 무기 | Pitch | Yaw | Pitch Random | Soft Cap | Hard Cap | Kick | 회복 시작 | Return | 연사 잔류 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 권총 | 2.2° | 0.35° | ±10% | 3.5° | 4.5° | 0.055초 | 0.10초 | 0.12초 | 20% |
| 라이플 | 2.3° | 0.32° | ±12% | 6.0° | 7.0° | 0.045초 | 0.10초 | 0.12초 | 35% |
| DMR | 3.2° | 0.45° | ±10% | 4.8° | 6.0° | 0.065초 | 0.18초 | 0.20초 | 25% |
| 샷건 | 4.6° | 0.85° | ±15% | 4.5° | 5.5° | 0.075초 | 0.28초 | 0.29초 | 30% 미사용 |
| 로켓 | 5.6° | 1.10° | ±15% | 5.5° | 6.5° | 0.10초 | 0.28초 | 0.42초 | 미사용 |

단발 Aim Recoil 강도는 `로켓 > 샷건 > DMR > 권총 > 라이플`이다. 라이플은 연사 누적으로 단발 무기보다 높은 최종 Pitch에 도달할 수 있다.

## 3. 발사 샘플

탄약 소비가 확정된 실제 발사마다 `RecoilSample` 하나를 생성한다.

```text
Pitch = BasePitch × Random(1 - Range, 1 + Range)
Yaw   = min(BaseYaw × Random(0.75, 1.0), Pitch × 0.4) × Direction
```

- Pitch는 항상 위쪽이다.
- 권총·샷건·DMR·로켓의 Direction은 발사마다 좌우를 새로 선택한다.
- 라이플은 0.25초 이내 연사를 하나의 Burst로 본다.
- 라이플은 4발 동안 같은 Yaw 방향을 유지하고 다음 4발은 반대 방향으로 전환한다.
- 모든 샘플에서 `|Yaw| ≤ Pitch × 0.4`를 보장한다.
- 같은 샘플의 수직 배율과 좌우 방향을 Viewmodel과 Fire Impulse가 공유한다.
- 빈 탄창, 발사 간격 미충족, 커서 잠금용 첫 클릭은 샘플을 생성하지 않는다.

## 4. Aim Recoil 상태와 곡선

```text
Idle → Kick → Hold → Return → Idle
          └── 재발사: 현재 보이는 값에서 새 Kick
```

### Kick

- 현재 실제 Aim Recoil에서 새 목표로 상승한다.
- `EaseOutQuad = 1 - (1 - t)²`를 사용한다.
- 발사 직후 빠르게 움직이고 목표에 가까워질수록 느려진다.

### Hold

- Kick 종료 후 목표값을 유지한다.
- 마지막 발사 시점부터 해당 무기의 회복 시작 시간이 지난 뒤 Return으로 전환한다.

### Return

- 단발은 복귀 시작 순간의 실제 오프셋에서 0으로 돌아간다.
- 연속 사격은 복귀 시작값의 무기별 잔류 비율까지만 돌아간다.
- `EaseOutCubic = 1 - (1 - t)³`를 사용한다.
- 초기에 빠르고 끝으로 갈수록 느려진다.
- 과거 카메라 방향을 저장하지 않으므로 플레이어가 마우스로 보정한 각도를 다시 끌어올리지 않는다.
- 연속 사격 Return 완료 시 잔류 Pitch·Yaw를 현재 기본 조준각에 넘기고 Aim Recoil 오프셋을 0으로 만든다. 이 프레임에서 화면 방향은 바뀌지 않는다.

## 5. 누적 반동과 Cap

재발사 시 이전 목표가 아니라 현재 화면에 실제 적용된 Aim Recoil만 사용한다.

```text
Start = Current Visible Aim Recoil
CapRatio = clamp01(CurrentPitch / SoftCap)
AddScale = lerp(1.0, 0.25, CapRatio)
TargetPitch = min(StartPitch + SamplePitch × AddScale, HardCap)
TargetYaw = StartYaw + SampleYaw
```

| 재발사 시점 | 처리 |
| --- | --- |
| Kick 중 | 미도달 목표는 버리고 현재 실제 값에 새 샘플 추가 |
| Hold 중 | 유지 중인 값에 새 샘플 추가 |
| Return 중 | 이미 복구된 양은 되살리지 않고 남은 값에 새 샘플 추가 |
| Idle | 0에서 새 샘플 시작 |

- Soft Cap에 가까울수록 한 발이 추가하는 Pitch가 최소 25%까지 감소한다.
- Hard Cap은 비정상적인 Pitch 누적만 차단한다.
- Yaw는 라이플 방향 전환과 자연 회복으로 한 방향 무한 누적을 억제한다.
- 재발사하면 Kick 진행도와 회복 시작 기준 시각을 새 발사 기준으로 갱신한다.
- 무기 교체만으로 진행 중인 Aim Recoil 프로필은 바꾸지 않는다.
- 교체 후 실제 발사한 순간에만 현재 잔여값에 새 무기의 Cap과 시간값을 적용한다.
- Return 중 재발사하면 예정된 잔류 반영을 취소하고 현재 보이는 오프셋에서 새 반동을 시작한다.

## 6. 단발·연속 사격 관계

| 무기 | 입력 | RPM | 발사 간격 | 연속 판정 | 반동 체감 |
| --- | --- | ---: | ---: | ---: | --- |
| 권총 | 클릭당 1발 | 405 | 약 0.148초 | 0.24초 | 2발 이상 연속 사격 종료 시 20% 잔류 |
| 샷건 | 클릭당 1회 | 66 | 약 0.909초 | 항상 단발 | 기본 자세 복귀 |
| 라이플 | 누르는 동안 자동 | 660 | 약 0.091초 | 0.32초 | 연사 종료 시 35% 잔류 |
| DMR | 클릭당 1발 | 315 | 약 0.190초 | 0.42초 | 2발 이상 연속 사격 종료 시 25% 잔류 |
| 로켓 | 스킬 발사 | 쿨다운 | 해당 없음 | 없음 | 강한 단발 반동 후 약 0.70초 복귀 |

- 권총·샷건·DMR은 발사 간격에 막힌 클릭을 예약하지 않는다.
- 라이플은 버튼을 누르는 동안 재시도하고 다음 발사 가능 시각에 자동 발사한다.
- 성공한 같은 무기 발사만 연속 판정 시간을 갱신한다. 다른 무기의 실제 발사는 새 Burst의 첫 발이며, 탄약 부족·발사 간격 차단·커서 잠금 클릭은 판정에 포함하지 않는다.
- 샷건은 반복 발사해도 항상 단발이며 로켓은 잔류 반동을 사용하지 않는다.
- 공통 발사 제한은 일시정지에 대응하는 `GameplayClock.Now`를 사용하며 무기 교체나 SettingsPanel로 우회할 수 없다.

## 7. Fire Impulse

Fire Impulse는 실제 조준과 분리된 짧은 카메라 충격이다.

| 무기 | Pitch | Yaw | Roll | 지속시간 |
| --- | ---: | ---: | ---: | ---: |
| 권총 | 0.35° | 0.05° | 0.12° | 0.08초 |
| 라이플 | 0.25° | 0.05° | 0.12° | 0.06초 |
| DMR | 0.50° | 0.10° | 0.18° | 0.10초 |
| 샷건 | 0.75° | 0.15° | 0.28° | 0.13초 |
| 로켓 | 1.00° | 0.20° | 0.35° | 0.16초 |

- 발사 순간 최대값을 즉시 적용하고 `EaseOutCubic`으로 0까지 감쇠한다.
- Yaw와 Roll은 Aim Recoil 샘플의 좌우 방향과 수평 강도를 공유한다.
- 새 발사 시 현재 Impulse 대신 새 발사의 짧은 Impulse로 갱신한다.
- 명중 Ray는 Fire Impulse를 제외한 Aim Rotation으로 계산한다.
- 무기 교체만으로 이미 시작된 Fire Impulse를 끊지 않는다.

## 8. Viewmodel 반동

| 무기 | 후퇴 | 좌우 이동 | Pitch | 최대 Yaw | 최대 Roll |
| --- | ---: | ---: | ---: | ---: | ---: |
| 권총 | 0.065m | 0.018m | 5.0° | 0.8° | 1.2° |
| 라이플 | 0.060m | 0.013m | 4.0° | 0.9° | 1.3° |
| DMR | 0.120m | 0.027m | 6.5° | 1.2° | 1.6° |
| 샷건 | 0.180m | 0.040m | 11.0° | 2.0° | 2.5° |
| 로켓 런처 | 0.140m | 0.040m | 9.0° | 2.2° | 3.0° |

```text
후퇴·Pitch = 무기 기본값 × Sample VerticalScale
좌우 이동 = 무기 기본값 × Sample HorizontalScale × Direction
Yaw·Roll = 무기 기본값 × Sample HorizontalScale × Direction
```

- 발사마다 현재 위치·회전 속도에 같은 `RecoilSample`의 후퇴·Pitch·Yaw·Roll 충격을 추가한다.
- 위치는 각주파수 `32`, 감쇠비 `0.70`, 회전은 각주파수 `24`, 감쇠비 `0.65`의 스프링을 사용한다. 위치가 먼저 풀리고 회전은 기본 자세를 약하게 한 번 지나친 뒤 안정된다.
- `Time.deltaTime`을 최대 `1/120초` 단위로 나눠 적분하며 한 프레임 최대 8단계까지만 처리한다. 불릿타임 중에는 스프링 운동도 35% 속도를 따른다.
- 카메라 Aim 수치와 Viewmodel 기본 수치는 분리돼 있어 조준은 제어 가능하지만 무기는 크게 움직인다.
- 재발사 시 현재 모델 속도를 초기화하지 않고 새 충격을 더한다.
- 모델 오프셋과 속도는 무기별 기준값의 1.5배 범위로 제한해 연사 중 화면 이탈과 클리핑을 막는다.
- 이동 스웨이·보빙과 발사 반동은 최종 모델 자세에서 합산한다.
- 무기 교체 시 이전 모델은 즉시 기본 자세로 복구하지만 Aim Recoil은 계속 회복한다.
- 카메라 Pitch Clamp가 Aim Recoil을 줄여도 Viewmodel은 원래 샘플을 사용한다.

## 9. 로켓 런처

- 실제 로켓 발사 시 Aim Recoil, Fire Impulse, 런처 Viewmodel이 같은 샘플을 사용한다.
- 런처 모델은 `후퇴 0.14m / 좌우 0.04m / Pitch 9° / 최대 Yaw 2.2° / 최대 Roll 3°`의 속도 충격을 적용한다.
- 일반 Viewmodel과 같은 스프링으로 움직이며 `0.28초 회복 시작 + 0.42초 Return`인 총 0.70초 동안 표시한다.
- 이 시간 동안 공격과 숫자키 무기 전환은 잠기며 이동과 조준은 허용한다.
- 투사체가 즉시 폭발해도 런처 반동은 끝까지 진행한다.
- 비활성화 시 런처 자세와 입력 잠금을 즉시 초기화한다.

## 10. 피격 Aim Punch

| 피해 원인 | Pitch | 최대 Yaw |
| --- | ---: | ---: |
| 자폭형 적 | 2.8° | 1.2° |
| 근거리 인간형 적 | 1.7° | 0.75° |
| 원거리 인간형 적 | 0.8° | 0.35° |

- 강도는 `자폭형 > 근거리 > 원거리`다.
- 상승 Lerp 속도는 30, 복귀 Lerp 속도는 8이다.
- 연속 피격은 현재 목표 Pitch에 추가한다.
- Pitch 3° Soft Cap에 접근할수록 추가량이 25%까지 감소한다.
- 최종 Hard Cap은 Pitch 4°, Yaw ±2°다.
- 총기 Aim Recoil과 별도 상태로 카메라에 합산한다.
- 명중 Ray와 Viewmodel에는 영향을 주지 않는다.
- 수류탄·로켓 자해와 기타 피해 원인의 Aim Punch는 현재 0이다.

## 11. 불릿타임

- Aim Recoil과 Fire Impulse는 `GameplayClock.Now`를 사용해 불릿타임에는 실시간, SettingsPanel 일시정지 중에는 정지한다.
- 총기 Viewmodel과 로켓 런처 스프링, 총구 화염·연기·DMR 탄도 파티클은 scaled time을 사용해 35% 속도를 따른다.
- 총기 RPM도 `GameplayClock.Now`를 사용한다.
- 불릿타임 `timeScale = 0.35`에서도 조준·발사속도·Aim Recoil은 실제 시간 속도를 유지하고 모델 및 파티클 표현만 느려진다.
- 배경음악 AudioSource의 Pitch도 `0.35`로 낮춰 재생속도를 세계 시간과 맞춘다.
- 플레이어 이동·점프·중력과 월드·적은 35% 속도를 따른다.
- 피격 Aim Punch는 `Time.deltaTime` 기반이므로 불릿타임 중 35% 속도로 진행한다.

## 12. 명중 방향과 확산

```text
Projectile Direction = Mouse Aim + Aim Recoil + Weapon Spread
```

- 권총: 수평 확산 `±0.35°`
- 라이플: 수평 확산 `±0.75°`
- DMR: 별도 확산 없음
- 샷건: 카메라 Aim 전방 5° 원뿔 안에서 8개 펠릿 생성
- Fire Impulse, Hit Aim Punch, Viewmodel 위치·Pitch·Yaw·Roll은 명중 방향에서 제외한다.
- 발사 기준은 화면 중앙에 고정한다. DMR은 일반 모드에서 조준선을 숨기고 FOV 45 정조준 중에만 표시한다.

## 13. Pitch 가시 범위

마우스 Pitch는 `-80°~80°`다.

```text
availablePitch = max(0, MousePitch + 80°)
```

- 현재 Aim Recoil, Kick 시작, 목표, Return 시작 Pitch를 모두 가시 범위로 제한한다.
- 한계를 넘어간 숨은 Pitch는 즉시 폐기하며 나중에 다시 나타나지 않는다.
- Pitch 여유가 없어도 Yaw는 적용한다.
- Fire Impulse, Viewmodel, Hit Aim Punch는 이 Clamp와 별도다.

## 14. 초기화 조건

다음 상황에서는 Aim Recoil, Fire Impulse, Hit Aim Punch, Viewmodel 반동과 로켓 런처 반동을 초기화한다.

- `FirstPersonController` 비활성화
- 커서 잠금 해제
- 플레이어 사망과 결과 씬 전환

`PlayerSkillController` 비활성화 시 로켓 런처 표시와 입력 잠금을 초기화하고 불릿타임의 `timeScale`과 `fixedDeltaTime`도 복원한다.

## 15. 조정 위치

- Aim Recoil·Fire Impulse·피격 Aim Punch: `Assets/Scripts/Player/FirstPersonController.cs`
- 일반 총기 Viewmodel: `Assets/Scripts/Player/WeaponViewmodelController.cs`
- 로켓 런처 Viewmodel: `Assets/Scripts/Player/PlayerSkillController.cs`
- 프리팹 직렬화 값: `Assets/Prefabs/Player/PF_Player.prefab`

코드 기본값과 프리팹 직렬화 값이 다르면 프리팹 값이 우선한다. 반동 밸런스 변경 시 둘을 함께 수정한다.

## 16. 필수 검증

- 모든 샘플이 `Pitch > 0`, `|Yaw| ≤ Pitch × 0.4`를 만족하는지 확인한다.
- 라이플 20발 연사에서 Aim Pitch가 7° Hard Cap을 넘지 않는지 확인한다.
- Soft Cap 접근 후에도 Viewmodel 반동과 Fire Impulse가 유지되는지 확인한다.
- Return 중 재발사에서 이미 복구된 반동이 되살아나지 않는지 확인한다.
- 권총·라이플·DMR 단발은 0까지 복구되고 연속 사격만 각각 20%·35%·25%가 현재 기본 조준각에 남는지 확인한다.
- 잔류 반영 프레임에서 화면이 순간 이동하지 않고, 판정 시간을 넘긴 다음 성공 발사는 새 단발이 되는지 확인한다.
- 무기 교체만으로 이전 Aim Recoil의 회복 프로필이 바뀌지 않는지 확인한다.
- Fire Impulse와 Hit Aim Punch가 발사 Ray 방향에 영향을 주지 않는지 확인한다.
- Viewmodel Yaw·Roll이 명중 방향에 영향을 주지 않는지 확인한다.
- 위쪽 Pitch 한계에서 폐기된 반동이 다시 나타나지 않는지 확인한다.
- 불릿타임에서 RPM과 Aim Recoil은 실제 시간 속도를 유지하고 Viewmodel·총구 효과·탄도 파티클·배경음악은 35% 속도로 느려지는지 확인한다.
- 연속 피격 Pitch·Yaw가 Hard Cap을 넘지 않는지 확인한다.
