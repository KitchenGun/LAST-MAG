# 디자이너 PC용 Codex 실행 프롬프트

아래 전체 내용을 디자이너의 Codex Windows 데스크톱 앱에 전달한다.

```text
REPOSITORY_URL=https://github.com/KitchenGun/Gulag-project
```

---

당신은 별도 Windows PC에서 기존 Unity 프로젝트에 합류하는 디자이너의 개발 환경을 온보딩한다. PowerShell을 우선 사용하고, 각 단계의 검증이 끝난 뒤에만 다음 단계로 진행하라.

## 고정값

```text
PROJECT_NAME=Gulag-project
UNITY_VERSION=6000.3.21f1
UNITY_MODULE=Web Build Support
MCP_NAME=unityMCP
MCP_VERSION=10.1.2
MCP_COMMIT=4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50
MCP_PACKAGE_URL=https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50
MCP_SERVER_PACKAGE=mcpforunityserver==10.1.2
MCP_TRANSPORT=stdio
```

## 성공 조건

다음만 검증한다.

1. 정확한 Unity Editor와 Web Build Support 설치
2. GitHub Desktop을 통한 기존 비공개 저장소 클론
3. 깨끗한 Git/LFS 상태와 저장소 고정 버전 확인
4. Unity 프로젝트가 컴파일 오류 없이 열림
5. `unityMCP`가 고정 버전 `stdio`로 연결됨
6. MCP로 Unity Editor 상태와 Console을 읽을 수 있음

MCP 쓰기, 스크립트 생성, Play Mode, Web 빌드와 브라우저 검증은 수행하지 말고 `not_run`으로 보고하라.

## 절대 규칙

- `REPOSITORY_URL`의 비공개 저장소에 접근할 수 없으면 즉시 중단하고 권한 부여를 요청하라.
- 새 프로젝트 생성, `git init`, 저장소 Publish를 하지 마라.
- 저장소 추적 파일을 생성·수정·삭제하지 마라.
- Commit, Push, Pull Request, Merge, 태그 생성을 하지 마라.
- 기존 설치를 제거하거나 다른 Unity 버전으로 프로젝트를 열지 마라.
- 더러운 작업 트리를 정리, reset, checkout, stash하지 마라.
- 비밀정보나 개인 절대경로를 저장소에 기록하지 마라.
- Unity Hub headless CLI를 사용하지 마라. 로그인, 라이선스, Editor·모듈 설치는 Hub GUI 체크포인트로 처리하라.
- 인증 정보나 라이선스 승인을 대신 입력하지 마라.
- 공식 문서와 실제 상태가 다르면 추측해 우회하지 마라.

## 1. 읽기 전용 사전 점검

현재 워크스페이스와 PC를 먼저 조사하라. 아래 항목을 `충족`, `설치 필요`, `사용자 작업 필요`, `차단`으로 표로 정리하라.

- Windows 버전, 64비트 여부, 드라이브 여유 공간
- Git, Git LFS, GitHub Desktop
- Unity Hub
- Unity Editor `6000.3.21f1`
- Web Build Support
- `uv`와 사용 가능한 Python 3.10 이상
- Codex 버전과 기존 MCP 목록
- 현재 폴더가 입력된 저장소의 클론인지 여부

최소 확인 명령은 다음과 같다. 없는 명령의 실패는 설치 필요로 기록하되 전체 검사를 중단하지 마라.

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

Unity와 Web 모듈은 Hub 표시와 실제 설치 디렉터리를 함께 확인하라. Windows의 `python.exe` 실행 별칭만 존재하면 Python 설치 완료로 판정하지 마라.

## 2. 없는 도구만 설치

Git, GitHub Desktop 또는 Unity Hub가 없을 때만 설치하라. 실행 전에 각각 `winget show --id <PACKAGE_ID> --exact --source winget`으로 이름과 Publisher를 확인하라.

```powershell
winget install --id Git.Git --exact --source winget
winget install --id GitHub.GitHubDesktop --exact --source winget
winget install --id Unity.UnityHub --exact --source winget
```

이미 설치된 도구는 업데이트하거나 재설치하지 마라. Git 설치 후 `git lfs version`을 다시 확인하라.

Unity Hub가 준비되면 사용자에게 다음 GUI 작업만 요청하고 기다려라.

1. Unity 계정 로그인과 라이선스 활성화
2. Unity `6000.3.21f1` 설치
3. Web Build Support 모듈 선택
4. 설치 완료 확인

정확한 버전을 공식 경로에서 찾을 수 없으면 대체 버전을 설치하지 말고 중단하라.

`uv` 또는 Python이 없으면 임의 런타임이나 Node.js를 추가하지 마라. 프로젝트를 연 뒤 CoplayDev의 **Window → MCP for Unity** 설정 마법사와 공식 안내가 요구하는 `uv`/Python만 설치하라.

## 3. GitHub Desktop 클론 체크포인트

현재 워크스페이스가 해당 저장소의 클론이 아니라면 사용자에게 다음을 요청하고 기다려라.

1. GitHub Desktop 로그인
2. **File → Clone repository → URL**에서 `REPOSITORY_URL` 입력
3. 한글·공백·특수문자가 없는 짧은 로컬 경로 선택
4. Clone 완료
5. 클론 폴더를 Codex 데스크톱 앱의 워크스페이스로 열기
6. 같은 작업을 다시 열어 “온보딩 계속”이라고 요청하거나 이 프롬프트를 다시 전달

CLI로 대신 클론하거나 인증 정보를 입력하지 마라.

## 4. 저장소 검증

클론된 워크스페이스에서 다음을 실행하라.

```powershell
git rev-parse --show-toplevel
git remote get-url origin
git branch --show-current
git status --porcelain
git lfs install
git lfs pull
git lfs status
```

다음을 모두 검증하라.

- `origin`의 owner/repository가 `REPOSITORY_URL`과 동일하다. `.git` 접미사 차이는 무시한다.
- 현재 브랜치는 `main`이다.
- `git status --porcelain` 출력이 없다.
- `ProjectSettings/ProjectVersion.txt`가 존재하고 `m_EditorVersion: 6000.3.21f1`이다.
- `Packages/manifest.json`에 `com.unity.render-pipelines.universal`이 있다.
- `Packages/manifest.json`의 MCP URL이 `MCP_COMMIT`으로 고정되어 있다.
- `Packages/packages-lock.json`의 MCP 해석 결과가 같은 참조를 사용한다.
- `.gitignore`가 Unity 생성물과 `Build/`, `Builds/`를 제외한다.
- `.gitattributes`가 저장소의 Git LFS 규칙을 포함한다.

하나라도 다르면 저장소를 고치거나 브랜치를 바꾸지 말고, 예상값과 실제값을 보고한 뒤 중단하라.

## 5. Unity 열기와 컴파일

사용자에게 Unity Hub에서 클론 프로젝트를 추가하고 반드시 `6000.3.21f1`로 열도록 요청하라. 최초 Import와 패키지 해석이 끝날 때까지 기다려라.

- 버전 업그레이드 확인창이 나오면 승인하지 마라.
- 프로젝트가 열린 뒤 Console Error가 0인지 확인하라.
- 컴파일 Error가 있으면 오류 전문을 수집하고 저장소 파일을 수정하지 말고 중단하라.

## 6. `unityMCP` stdio 설정

Unity에서 사용자에게 다음을 요청하라.

1. **Window → MCP for Unity** 열기
2. 전송 방식 `stdio` 선택
3. **Configure All Detected Clients** 실행
4. 설정 완료 상태 확인

공식 설정 후 `~/.codex/config.toml`을 읽어 다음을 검증하라.

- 서버 이름: `unityMCP`
- `--from mcpforunityserver==10.1.2`
- 실행 엔트리: `mcp-for-unity`
- 전송: `--transport stdio`
- `startup_timeout_sec = 60`
- `default_tools_approval_mode = "writes"`

공식 설정기가 추가한 `--offline` 캐시 옵션이나 Windows 환경 항목은 보존하라.

`default_tools_approval_mode`만 빠졌다면 `config.toml`을 같은 디렉터리에 타임스탬프 백업한 후 해당 `unityMCP` 테이블에만 추가하라. 다른 설정은 변경하지 마라.

기존 `unityMCP`, `unity-local` 등 Unity MCP 중복 설정이 있거나 버전·전송 방식이 다르면 임의 삭제 또는 덮어쓰기를 하지 말고 차이를 보고하라.

## 7. 재시작 후 읽기 연결 검증

Unity Editor를 프로젝트가 열린 상태로 유지하고 사용자에게 Codex 데스크톱 앱 재시작을 요청하라. 재시작 후 같은 작업에서 계속한다.

1. `codex mcp list`에서 `unityMCP`가 enabled인지 확인한다.
2. 현재 세션에 실제 노출된 Unity MCP 도구를 찾는다.
3. 읽기 도구로 Unity Editor 상태를 조회한다.
4. 읽기 도구로 Console 로그와 Error 개수를 확인한다.

도구 이름을 추측하지 말고 현재 노출된 도구만 사용하라. GameObject, Scene, Asset, C# 파일을 생성하거나 Play Mode를 실행하지 마라.

## 8. 최종 무변경 확인

```powershell
git status --porcelain
git lfs status
```

작업 트리가 깨끗해야 한다. 변경이 있으면 삭제하거나 되돌리지 말고 파일 목록과 발생 단계를 보고하라.

## 9. 결과 보고

다음 형식으로 짧고 구체적으로 보고하라. 명령 실행 결과나 화면 확인처럼 실제 근거가 없는 항목을 `verified`로 표시하지 마라.

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

`PASS`는 여섯 성공 조건을 모두 실제로 검증했고 최종 작업 트리가 깨끗할 때만 사용하라.
