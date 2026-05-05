# PompoEngine 실행과 사용법

이 문서는 PompoEngine을 처음 실행해서 샘플 VN을 만들고, 에디터로 수정하고, 런타임으로 확인한 뒤 Windows/macOS/Linux 배포물을 만드는 현재 기준 사용 경로입니다.

현재 기본 흐름은 에디터 우선입니다. CLI는 자동화, 검증, 릴리스 패키징, CI용 보조 경로입니다.

## 1. 설치와 저장소 확인

필요한 도구:

- .NET SDK `10.0.100`
- Windows, macOS, Linux 중 하나의 데스크톱 환경
- GitHub Actions 아티팩트를 받을 경우 GitHub 계정 또는 `gh` CLI

저장소 루트에서 먼저 복원, 빌드, 테스트를 실행합니다.

```bash
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx
dotnet test PompoEngine.slnx
```

릴리스 직전과 같은 전체 점검은 다음 스크립트로 실행합니다.

```bash
scripts/check-release-gates.sh
```

Windows PowerShell 또는 PowerShell Core에서는 다음 명령을 사용합니다.

```powershell
pwsh scripts/check-release-gates.ps1
```

이 스크립트는 restore, build, test, CLI/runtime version, docs site, repository doctor, runtime validation을 한 번에 확인합니다.

## 2. 가장 빠른 시작

샘플 프로젝트를 CLI로 만들고 에디터에서 여는 흐름입니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/MyVN --name MyVN --template sample --json
dotnet run --project src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj
```

에디터가 열리면 Dashboard에서 `/tmp/MyVN/project.pompo.json`이 있는 폴더를 열거나 Recent Projects에서 선택합니다.

에디터만으로 시작하려면 다음 명령으로 에디터를 실행한 뒤 Dashboard에서 `Create Sample Project`를 누릅니다.

```bash
dotnet run --project src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj
```

## 3. 에디터에서 제작하기

에디터의 기본 작업 순서:

1. Dashboard에서 샘플 또는 최소 프로젝트를 만들거나 기존 프로젝트 폴더를 엽니다.
2. Project/Resources 패널에서 이미지, 오디오, 폰트, 데이터 에셋 상태를 확인합니다.
3. Scene 패널에서 배경, 캐릭터 배치, 시작 그래프를 설정합니다.
4. Graph 패널에서 대사, 선택지, 조건 분기, 변수, 배경 변경, 캐릭터 표시, BGM/SFX/Voice, 세이브 포인트 같은 VN 노드를 연결합니다.
5. Inspector에서 선택한 씬 오브젝트, 에셋, 노드 속성을 수정합니다.
6. Preview에서 현재 그래프를 분리된 FNA 런타임 경로로 실행해 확인합니다.
7. Localization 탭에서 `ko`, `en` 같은 locale 문자열을 관리합니다.
8. Theme 탭에서 대사창, 선택지, 세이브 메뉴, 백로그 색상/스킨/레이아웃/애니메이션을 조정합니다.
9. Build 패널에서 빌드 프로필을 선택하고 Windows/macOS/Linux 결과 폴더를 만듭니다.
10. Console에서 검증 오류, 깨진 참조, 스크립트 컴파일 오류, 런타임 로그를 확인합니다.

Workspace 상단의 `Balanced`, `Graph Focus`, `Scene Focus`, `Review` 프리셋으로 작업 화면을 빠르게 바꿀 수 있습니다. Project, Scene, Graph, Inspector, Preview, Console은 필요하면 별도 창으로 분리해 둘 수 있습니다.

## 4. 프로젝트 검증

프로젝트를 열거나 빌드하기 전에 다음 명령으로 상태를 확인합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- localization report --project /tmp/MyVN --json
```

검증은 누락된 시작 노드, 깨진 씬/그래프/에셋 참조, 타입 불일치, 순환 호출, 잘못된 선택지 포트, 지원 locale 누락, 잘못된 UI 테마/스킨/레이아웃 값을 빌드 전에 잡습니다.

## 5. 에셋 관리

에셋 가져오기:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset import --project /tmp/MyVN --file ./background.png --type Image --asset-id bg-main --json
```

목록과 해시 검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project /tmp/MyVN --type Image --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN --json
```

외부 도구로 파일을 교체한 뒤 해시만 갱신:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset rehash --project /tmp/MyVN --json
```

미사용 에셋 삭제:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset delete --project /tmp/MyVN --asset-id unused-bg --json
```

파일은 남기고 Asset Database 항목만 삭제하려면 `--keep-file`을 붙입니다.

## 6. 빌드 프로필과 독립 실행 빌드

샘플 프로젝트에는 기본으로 `BuildProfiles/debug.pompo-build.json`와 `BuildProfiles/release.pompo-build.json`가 생성됩니다.

프로필 확인:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile show --project /tmp/MyVN --name release
```

릴리스 프로필로 현재 OS용 빌드:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --output /tmp/MyVN/Builds --json
```

플랫폼을 명시하려면 `--platform`을 추가합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --platform MacOS --output /tmp/MyVN/Builds --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --platform Linux --output /tmp/MyVN/Builds --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --platform Windows --output /tmp/MyVN/Builds --json
```

빌드 결과 검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/MyVN/Builds/MacOS/release --require-smoke-tested-locales --require-self-contained --json
```

빌드 결과에는 런타임, `Data/project.pompo.json`, 컴파일된 `*.pompo-ir.json`, 복사된 에셋, 사용자 스크립트 어셈블리, `pompo-build-manifest.json`만 들어가야 합니다. 에디터, Avalonia, 테스트, CLI 어셈블리는 릴리스 결과물에 포함되면 실패로 처리됩니다.

## 7. 런타임 직접 실행

빌드된 IR을 헤드리스로 실행해 자동 검증합니다.

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --locale ko --choices 1 --json-trace
```

FNA 창에서 인터랙티브로 실행합니다.

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --run-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --locale ko --saves /tmp/MyVN/Saves
```

런타임 입력:

- `Space`, `Enter`, 좌클릭: 대사 표시 완료 또는 다음 줄 진행
- 마우스 hover, `Up`, `Down`: 선택지/저장 슬롯 이동
- 좌클릭, `Enter`: 선택지 확정 또는 저장 슬롯 로드
- `B`: 백로그
- `F5`: 퀵 세이브
- `F6`: 선택 슬롯 세이브
- `F9`: 퀵 로드

저장 슬롯 목록:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save list --saves /tmp/MyVN/Saves --json
```

## 8. 릴리스 패키지 만들기

빌드 결과를 zip, checksum, release manifest로 묶습니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release package --build /tmp/MyVN/Builds/MacOS/release --output /tmp/MyVN/Releases --name MyVN-0.1.0-macos --json
```

검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```

공개 직전 감사:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```

서명 키가 있을 때는 다음 명령으로 릴리스 매니페스트에 서명하고 검증합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release sign --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --private-key ./release-private.pem --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify-signature --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --public-key ./release-public.pem --json
```

## 9. GitHub Actions 아티팩트 받기

저장소의 Release workflow는 Windows, macOS, Linux 샘플 런타임을 각각 패키징하고 아티팩트로 업로드합니다.

현재 외부에서 성공 확인된 최신 예시:

- Release run: `https://github.com/yoko57822/PompoEngine/actions/runs/25370242808`
- 커밋: `8dff32b805a7d00a4ce8e7c4bd6c164ff6e134ba`
- 아티팩트:
  - `PompoEngineSample-0.1.1-windows`
  - `PompoEngineSample-0.1.1-macos`
  - `PompoEngineSample-0.1.1-linux`

브라우저에서는 Actions의 해당 Release run으로 들어가 Artifacts 섹션에서 내려받습니다.

`gh` CLI로는 다음처럼 받을 수 있습니다.

```bash
gh run download 25370242808 --repo yoko57822/PompoEngine --name PompoEngineSample-0.1.1-macos --dir artifacts/downloaded-release
gh run download 25370242808 --repo yoko57822/PompoEngine --name PompoEngineSample-0.1.1-linux --dir artifacts/downloaded-release
gh run download 25370242808 --repo yoko57822/PompoEngine --name PompoEngineSample-0.1.1-windows --dir artifacts/downloaded-release
```

각 아티팩트 안에는 플랫폼별 `.zip`, `.zip.sha256`, `.release.json`가 들어갑니다. 서명 secrets가 설정된 릴리스에서는 `.zip.sig`도 함께 들어갑니다.

태그 기반 공개 릴리스는 `v*` 태그를 push할 때 생성됩니다. 수동 workflow_dispatch 실행은 플랫폼 아티팩트 검증용이며 GitHub Release draft 생성은 태그 실행에서만 수행됩니다.

## 10. 문서 사이트

로컬 문서 사이트 생성:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
```

결과물:

- `artifacts/docs-site/index.html`
- `artifacts/docs-site/pages/*.html`
- `artifacts/docs-site/pompo-docs-site.json`

공개 문서 사이트:

- `https://yoko57822.github.io/PompoEngine/`

## 11. 자주 쓰는 명령 요약

```bash
# 저장소 전체 확인
scripts/check-release-gates.sh

# 에디터 실행
dotnet run --project src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj

# 샘플 프로젝트 생성
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/MyVN --name MyVN --template sample --json

# 프로젝트 검증
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN --json

# 릴리스 빌드
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --output /tmp/MyVN/Builds --json

# 릴리스 패키지
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release package --build /tmp/MyVN/Builds/MacOS/release --output /tmp/MyVN/Releases --name MyVN-0.1.0-macos --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```
