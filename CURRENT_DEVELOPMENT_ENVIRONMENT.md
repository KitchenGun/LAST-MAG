# Gulag-project 현재 개발 환경

최종 확인: 2026-08-06

## 저장소

- 원격: `https://github.com/KitchenGun/Gulag-project.git` (private)
- 기본 브랜치: `main`
- 초기 환경 태그: `v0.1.0`
- Git LFS와 UnityYAMLMerge 규칙은 `.gitattributes`에 있다.
- Unity 생성물과 `Build/`, `Builds/`는 `.gitignore`에서 제외한다.
- GitHub Free private 저장소 제한으로 `main` 보호 규칙은 적용하지 않는다. 협업자는 수동으로 PR·리뷰 절차를 지키며 강제 푸시, 삭제, 직접 병합을 하지 않는다.

## Unity

- Unity Editor: `6000.3.21f1` (`c02631ffc030`)
- 필수 모듈: Web Build Support
- 프로젝트 이름: `Gulag-project`
- 렌더 파이프라인: URP `17.3.0`
- 기준 Scene/설정: `Assets/Scenes/`, `Assets/Settings/`

## Codex와 Unity MCP

- MCP 서버 이름: `unityMCP`
- 서버 패키지: `mcpforunityserver==10.1.2`
- 전송 방식: `stdio`
- Unity 패키지: `com.coplaydev.unity-mcp`
- 고정 커밋: `4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50`
- 로컬 Codex 설정은 `uvx --from mcpforunityserver==10.1.2 mcp-for-unity --transport stdio`를 사용한다.
- MCP 쓰기 도구는 Codex의 `writes` 승인 절차가 있을 때만 사용한다.
- 로컬 Codex 설정, 설치 경로, 토큰은 저장소에 커밋하지 않는다.

## 협업 규칙

- 작업 성격에 따라 `art/*`, `dev/*`, `fix/*` 브랜치를 사용한다.
- 같은 Scene, Prefab, `.asset`, Animator Controller를 수정하기 전 파일 경로와 담당자를 공유한다.
- 온보딩 PASS 후 명시된 디자인 작업은 Unity UI 또는 승인된 `unityMCP` 쓰기 도구로 Scene, Prefab, `.asset`을 수정할 수 있다.
- C# 스크립트 생성, Play Mode, Web Build, Commit, Push, Merge, Tag는 별도 요청이 있어야 한다.

## 검증 상태

- verified: Unity와 Web Build Support 설치, private `main`, URP/MCP 고정, Unity 컴파일, `unityMCP` stdio, Editor 상태 읽기, Console 오류 0건
- not_run: Web Build, MCP 쓰기, C# 스크립트 생성, Play Mode, 브라우저 검증

## 디자이너 온보딩

디자이너에게는 `DESIGNER_CODEX_UNITY_ONBOARDING_PROMPT.md`만 전달한다. 이 문서는 소유자와 팀의 현재 개발 환경 기준이다.
