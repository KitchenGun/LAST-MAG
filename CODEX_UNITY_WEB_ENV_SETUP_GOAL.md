# Codex Goal: Unity Web 디자이너 협업 환경 온보딩

## 1. 목적

별도 Windows PC의 디자이너가 기존 GitHub 비공개 Unity 프로젝트를 동일한 버전으로 열고, Codex에서 Unity Editor 상태와 Console을 읽을 수 있는 환경을 구축한다.

이 문서는 **협업자 온보딩 전용**이다. 새 Unity 프로젝트나 GitHub 저장소를 만들지 않는다.

| 항목 | 고정값 |
|---|---|
| Unity Project | `Gulag-project` |
| GitHub Repository | [`KitchenGun/Gulag-project`](https://github.com/KitchenGun/Gulag-project) |
| Repository Visibility | Private |
| Unity Editor | Unity 6.3 LTS `6000.3.21f1` |
| 필수 모듈 | Web Build Support |
| Rendering | 저장소에 커밋된 URP 설정 |
| Git 클라이언트 | GitHub Desktop |
| 대용량 파일 | Git LFS |
| MCP Unity 패키지 | CoplayDev MCP for Unity `v10.1.2` |
| MCP 커밋 | `4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50` |
| MCP Python 서버 | `mcpforunityserver==10.1.2` |
| MCP 전송 | 로컬 `stdio` |
| Codex MCP 이름 | `unityMCP` |
| Codex 실행 환경 | Windows 데스크톱 앱 |
| 검증 범위 | 설치, 클론, Unity 컴파일, MCP 읽기 연결 |

공식 기준:

- [Unity 6000.3.21f1 릴리스](https://unity.com/releases/editor/whats-new/6000.3.21f1)
- [CoplayDev unity-mcp v10.1.2](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.1.2)
- [CoplayDev 설치 문서](https://coplaydev.github.io/unity-mcp/getting-started/install)
- [Codex MCP 설정](https://learn.chatgpt.com/docs/extend/mcp)

---

## 2. 저장소

디자이너는 다음 비공개 저장소에 협업자로 참여한다.

```text
REPOSITORY_URL=https://github.com/KitchenGun/Gulag-project
```

디자이너 계정에 이 저장소의 접근 권한이 없으면 온보딩을 시작하지 않는다.

---

## 3. 완료 기준

다음을 모두 만족하면 이번 온보딩은 `PASS`다.

1. Unity Editor `6000.3.21f1`과 Web Build Support가 설치되어 있다.
2. GitHub Desktop으로 비공개 저장소를 클론했고 `origin`이 입력된 저장소를 가리킨다.
3. Git LFS 파일을 정상적으로 받았고 작업 트리가 깨끗하다.
4. 저장소의 Unity 버전, URP, MCP 고정 버전이 소유자 기준과 일치한다.
5. 프로젝트가 컴파일 오류 없이 열린다.
6. Codex 재시작 후 `unityMCP`가 연결된다.
7. MCP를 통해 Unity Editor 상태와 Console을 읽을 수 있다.

이번 온보딩에서 아래 항목은 실행하지 않고 반드시 `not_run`으로 보고한다.

- MCP를 통한 GameObject 또는 Asset 쓰기
- C# 스크립트 생성
- Play Mode 실행
- Development 또는 Release Web 빌드
- Chrome 또는 Edge 실행 검증
- Commit, Push, Pull Request, Merge, 태그 생성

`not_run` 항목 때문에 온보딩 결과를 `PARTIAL`로 낮추지 않는다. 위 일곱 완료 기준만으로 판정한다.

---

## 4. 저장소 소유자 선행 조건

디자이너에게 프롬프트를 전달하기 전에 저장소 소유자가 다음을 완료해야 한다.

- 비공개 저장소를 만들고 디자이너 계정에 접근 권한을 부여한다.
- 기본 브랜치를 `main`으로 설정한다.
- GitHub Free private 저장소 제한으로 `main` 브랜치 보호는 적용하지 않는다. 소유자와 협업자는 `art/*`, `dev/*`, `fix/*` 브랜치 및 리뷰 절차를 수동으로 지키고, 강제 푸시·삭제·직접 병합을 하지 않는다.
- 초기 환경 태그 `v0.1.0`은 소유자가 생성한다. 이후 릴리스 태그 정책은 소유자만 정하고 생성한다.
- `ProjectSettings/ProjectVersion.txt`를 `6000.3.21f1`로 커밋한다.
- URP 설정과 `com.unity.render-pipelines.universal` 의존성을 커밋한다.
- `Packages/manifest.json`의 MCP 패키지를 다음 커밋으로 고정한다.

```text
https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50
```

- `.gitignore`에 Unity 생성물과 `Build/`, `Builds/` 제외 규칙을 커밋한다.
- `.gitattributes`에 팀의 Git LFS와 UnityYAMLMerge 규칙을 커밋한다.
- `Assets/Scenes/`와 `Assets/Settings/`를 포함한 초기 폴더 뼈대를 커밋한다.
- 비밀키, 토큰, 개인 설치경로를 커밋하지 않는다.

위 조건이 저장소와 다르면 디자이너 Codex가 고치지 않는다. 소유자에게 불일치 내용을 보고하고 중단한다.

---

## 5. 안전 원칙

- 기존 설치를 제거하거나 다른 Unity 버전으로 프로젝트를 열지 않는다.
- `git init`, 새 저장소 Publish, Commit, Push, Merge, 태그 생성을 하지 않는다.
- 저장소 추적 파일을 생성·수정·삭제하지 않는다.
- 현재 작업 트리가 더러우면 정리하거나 되돌리지 않고 변경 파일만 보고한다.
- 개인 절대경로와 설치경로는 최종 채팅 보고에만 표시한다.
- 최신 Unity Hub에서 폐기 예정인 headless Hub CLI를 사용하지 않는다.
- Unity Hub 로그인, 라이선스 활성화, Editor·모듈 설치는 Hub GUI에서 진행한다.
- GitHub Desktop 로그인과 비공개 저장소 클론은 사용자가 직접 확인한다.
- 같은 Scene, Prefab, `.asset`, Animator Controller는 담당자 확인 없이 동시에 수정하지 않는다.
- 브랜치는 역할이 아니라 작업 성격에 따라 `art/*`, `dev/*`, `fix/*`를 사용한다.

---

## 6. 단계별 온보딩

### Phase A. 읽기 전용 사전 점검

PowerShell에서 다음 상태를 조사하고 `충족`, `설치 필요`, `사용자 작업 필요`, `차단`으로 분류한다.

- Windows 버전과 아키텍처
- 로컬 드라이브의 사용 가능 용량
- Git, Git LFS, GitHub Desktop
- Unity Hub
- Unity Editor `6000.3.21f1`
- Web Build Support
- `uv` 및 Python 3.10 이상 사용 가능 여부
- Codex CLI 또는 데스크톱 앱
- 기존 Codex MCP 설정과 `unityMCP` 중복 여부

기본 확인 명령:

```powershell
Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, OSArchitecture
Get-PSDrive -PSProvider FileSystem | Select-Object Name, Root, Used, Free
git --version
git lfs version
uv --version
uv python list
codex --version
codex mcp list
winget list --id Unity.UnityHub --exact
winget list --id Git.Git --exact
winget list --id GitHub.GitHubDesktop --exact
```

`python.exe`가 Windows 실행 별칭만 가리키는 경우 설치된 Python으로 판정하지 않는다. MCP 공식 설정기가 사용하는 `uv` 기준으로 확인한다.

### Phase B. 누락 도구 설치

설치 전 `winget show --id <PACKAGE_ID> --exact --source winget`으로 패키지 이름과 Publisher를 확인한다. 없는 항목만 설치한다.

```powershell
winget install --id Unity.UnityHub --exact --source winget
winget install --id Git.Git --exact --source winget
winget install --id GitHub.GitHubDesktop --exact --source winget
```

각 명령은 해당 도구가 없을 때만 실행한다. Git for Windows 설치 후 포함된 Git LFS를 다시 검증한다.

`uv` 또는 Python이 없으면 임의 패키지를 고르지 않는다. Unity의 **Window → MCP for Unity** 설정 마법사와 CoplayDev 공식 설치 안내를 사용한다.

Unity Hub에서는 사용자가 다음을 직접 완료한다.

1. Unity 계정 로그인 및 라이선스 활성화
2. Unity `6000.3.21f1` 설치
3. Web Build Support 모듈 선택
4. 설치 완료 후 Hub에 정확한 버전이 표시되는지 확인

정확한 버전을 공식 경로에서 찾을 수 없으면 대체 버전을 설치하지 않는다.

### Phase C. GitHub Desktop 클론

1. 사용자가 GitHub Desktop에 로그인한다.
2. **File → Clone repository → URL**에서 `REPOSITORY_URL`을 입력한다.
3. 한글·공백·특수문자가 없는 짧은 로컬 경로를 선택한다.
4. 클론을 완료한다.
5. 클론 폴더를 Codex 데스크톱 앱의 새 워크스페이스로 연다.
6. 이 온보딩 프롬프트를 같은 작업에서 계속하거나 다시 붙여 넣는다.

Codex는 GitHub Desktop을 대신해 인증 정보를 입력하지 않는다.

### Phase D. 클론 검증

클론 폴더에서 다음을 확인한다.

```powershell
git rev-parse --show-toplevel
git remote get-url origin
git branch --show-current
git status --porcelain
git lfs install
git lfs pull
git lfs status
```

검증 규칙:

- `origin`의 owner/repository가 `REPOSITORY_URL`과 같아야 한다. `.git` 접미사 차이는 허용한다.
- 현재 브랜치는 `main`이어야 한다.
- `git status --porcelain` 출력이 없어야 한다.
- `ProjectSettings/ProjectVersion.txt`의 `m_EditorVersion`은 `6000.3.21f1`이어야 한다.
- `Packages/manifest.json`에는 URP와 고정된 MCP 커밋이 있어야 한다.
- `Packages/packages-lock.json`의 MCP 해석 결과도 같은 고정 참조를 가리켜야 한다.
- 정확한 Editor 설치 아래에 Web Build Support가 있어야 하며 Hub GUI 표시와 함께 확인한다.

불일치가 있으면 파일을 고치거나 다른 브랜치로 전환하지 않는다.

### Phase E. Unity 열기와 컴파일 확인

1. Unity Hub에서 클론 프로젝트를 추가한다.
2. 반드시 `6000.3.21f1`로 연다.
3. 패키지 해석과 최초 Import가 끝날 때까지 기다린다.
4. Console의 Error 개수가 0인지 확인한다.
5. Unity가 프로젝트 버전 업그레이드를 요구하면 승인하지 않고 중단한다.

### Phase F. Codex stdio MCP 설정

Unity에서 다음 순서로 설정한다.

1. **Window → MCP for Unity**
2. 전송 방식을 `stdio`로 선택
3. **Configure All Detected Clients** 실행
4. 상태 패널과 설정 결과 확인

공식 설정기가 생성한 `~/.codex/config.toml`의 `unityMCP` 항목은 다음 의미를 만족해야 한다.

```toml
[mcp_servers.unityMCP]
command = "<감지된 uvx 실행 경로>"
args = ["--from", "mcpforunityserver==10.1.2", "mcp-for-unity", "--transport", "stdio"]
startup_timeout_sec = 60
default_tools_approval_mode = "writes"
```

`--offline` 같은 공식 설정기의 캐시 옵션과 Windows용 환경 설정은 허용한다. 다음 값은 반드시 유지한다.

- `--from mcpforunityserver==10.1.2`
- `mcp-for-unity`
- `--transport stdio`

쓰기 승인 설정을 추가해야 한다면 먼저 `config.toml`을 타임스탬프가 붙은 백업 파일로 복사하고, 다른 MCP 항목은 그대로 보존한다.

기존 `unityMCP`, `unity-local` 등 Unity MCP 중복 항목이 있으면 임의 삭제하지 않는다. 이름과 설정 차이를 보고하고 사용자 확인을 기다린다.

### Phase G. 재시작 후 읽기 연결 검증

1. Unity Editor는 프로젝트를 연 상태로 둔다.
2. Codex 데스크톱 앱을 재시작한다.
3. 같은 저장소 작업을 다시 연다.
4. `codex mcp list`에서 `unityMCP`가 enabled 상태인지 확인한다.
5. 실제로 노출된 Unity MCP 도구를 사용해 Editor 상태를 조회한다.
6. Console 로그를 읽고 Error 개수를 확인한다.

도구 이름을 추측하지 않는다. 현재 세션에 노출된 Unity MCP 도구만 사용한다.

---

## 7. 중단 조건

다음 상황에서는 우회하지 않고 `FAIL` 또는 `PARTIAL`로 보고한다.

- `origin`이 `KitchenGun/Gulag-project`를 가리키지 않는다.
- 디자이너 계정이 비공개 저장소에 접근할 수 없다.
- 클론 폴더가 Unity 프로젝트가 아니다.
- 작업 트리가 이미 더럽다.
- 저장소 Unity 버전이 `6000.3.21f1`이 아니다.
- 정확한 Unity Editor 또는 Web Build Support를 설치할 수 없다.
- 프로젝트 업그레이드가 필요하다.
- URP 또는 MCP 고정 참조가 저장소에 없다.
- Unity Console에 컴파일 Error가 있다.
- Unity MCP 설정이 다른 버전 또는 HTTP를 사용한다.
- Codex 재시작 후 MCP가 연결되지 않는다.
- 기존 설치 제거, 관리자 권한, 인증 정보 입력이 필요하다.

중단 시 오류 전문, 실행한 명령, 현재 상태, 사용자 수동 작업, 저장소 변경 여부를 보고한다.

---

## 8. 최종 보고 형식

```markdown
# Unity Web 디자이너 온보딩 결과

## 결과
- 전체: PASS / PARTIAL / FAIL
- 저장소 변경: 없음 / 있음

## 환경
| 항목 | 감지값 | 상태 | 근거 |
|---|---|---|---|
| Windows | | verified / fail | |
| Unity | | verified / fail | |
| Web Build Support | | verified / fail | |
| Git / Git LFS | | verified / fail | |
| GitHub Desktop | | verified / fail | |
| uv / Python | | verified / fail | |

## 저장소
| 항목 | 감지값 | 상태 |
|---|---|---|
| origin | | |
| branch | | |
| working tree | | |
| project version | | |
| URP | | |
| MCP package pin | | |

## MCP
| 항목 | 감지값 | 상태 |
|---|---|---|
| name | unityMCP | |
| package | mcpforunityserver==10.1.2 | |
| transport | stdio | |
| Editor state read | | |
| Console read | | |

## 의도적으로 실행하지 않음
- MCP 쓰기: not_run
- C# 스크립트 생성: not_run
- Play Mode: not_run
- Web 빌드: not_run
- 브라우저 검증: not_run
- Commit / Push / Merge / Tag: not_run

## 사용자 수동 작업
-

## 차단 또는 위험
-
```
