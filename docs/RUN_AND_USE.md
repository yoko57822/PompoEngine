# PompoEngine 실행과 사용법

이 문서는 로컬에서 PompoEngine을 실행하고, 샘플 비주얼노벨 프로젝트를 만들고, 에디터와 CLI로 검증/빌드하는 가장 짧은 경로를 정리합니다.

## 1. 준비

필요한 도구:

- .NET SDK `10.0.100`
- macOS, Windows, Linux 중 하나의 데스크톱 환경

저장소 루트에서 의존성을 복원하고 빌드합니다.

```bash
dotnet restore PompoEngine.slnx
dotnet build PompoEngine.slnx
dotnet test PompoEngine.slnx
```

저장소 상태 점검은 다음 명령으로 실행합니다.

```bash
scripts/check-release-gates.sh
```

Windows PowerShell 또는 PowerShell Core 환경에서는 다음 명령을 사용할 수 있습니다.

```powershell
pwsh scripts/check-release-gates.ps1
```

위 스크립트는 restore, build, test, CLI/runtime version, docs site, repository doctor, runtime validation을 한 번에 실행합니다.
개별 명령으로 확인하려면 다음 순서로 실행합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- version --json
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --validate-runtime
```

## 2. 에디터 실행

Avalonia 에디터를 실행합니다.

```bash
dotnet run --project src/Pompo.Editor.Avalonia/Pompo.Editor.Avalonia.csproj
```

에디터에서 기본 흐름:

1. Dashboard에서 `Create Sample Project` 또는 `Create Minimal Project`로 프로젝트를 만듭니다.
2. Dashboard의 Recent Projects 목록에서 최근에 연 프로젝트를 다시 열 수 있습니다.
3. Project/Resources 패널에서 에셋 상태, 깨진 참조, 미사용 에셋을 확인합니다.
   `Detach Project`를 누르면 리소스 브라우저를 별도 창으로 유지하면서 Scene 또는 Graph 작업을 계속할 수 있습니다.
4. Scene 패널에서 배경, 캐릭터, 시작 그래프를 편집합니다.
   `Detach Scene`을 누르면 씬 목록, 배경/캐릭터 편집, Stage Layout, Runtime Preview를 별도 창에서 확인할 수 있습니다.
5. Workspace 탭 상단의 `Balanced`, `Graph Focus`, `Scene Focus`, `Review` 프리셋으로 패널 배치를 작업 맥락에 맞게 전환합니다.
   `Project`, `Scene`, `Graph`, `Review`, `All` Focus 버튼은 프리셋과 패널 표시 상태를 한 번에 바꿔 현재 작업에 필요한 영역만 빠르게 남깁니다.
   Project, Scene, Graph, Inspector, Console 체크박스로 현재 작업에 필요 없는 패널을 접거나 다시 펼칠 수 있습니다.
   `Save Workspace`를 누르면 현재 프리셋과 패널 표시 상태가 로컬 에디터 설정으로 저장되어 다음 실행 때 복원됩니다.
   `Detach Tools`를 누르면 Project, Graph, Inspector, Console 보조 창을 한 번에 열어 현재 Workspace 배치와 별도로 운용할 수 있습니다.
6. Graph 패널에서 편집할 그래프를 선택하거나 새 그래프를 추가하고, 대사, 선택지, 조건 분기, 배경 변경, 캐릭터 표시, BGM/SFX/Voice 오디오 노드를 연결합니다.
   `Detach Graph`를 누르면 그래프 캔버스, 노드 목록, 연결 도구, 그래프 진단을 별도 창으로 띄울 수 있습니다.
   새 `Choice` 노드는 기본으로 `left`/`right` 두 실행 출력과 같은 포트를 가리키는 두 선택지 템플릿을 만듭니다.
   각 선택지에는 `enabled` boolean 또는 bool 변수명을 가리키는 `enabledVariable`을 넣어 조건 미충족 선택지를 비활성 상태로 표시할 수 있습니다.
   각 선택지의 `port`는 실제 Choice 실행 출력 포트여야 하고 그 포트가 다음 노드로 연결되어 있어야 합니다. 끊어진 선택지는 `GRAPH014` 진단으로 빌드 전에 실패합니다.
   모든 선택지가 literal `enabled: false`인 경우 진행할 수 없는 분기이므로 `GRAPH015` 진단으로 실패합니다. 런타임 변수로 열리는 선택지는 `enabledVariable`을 사용합니다.
   `choices` 배열 항목은 JSON object여야 하며, 잘못된 항목은 `GRAPH016` 진단으로 실패합니다.
   Workspace 화면의 Project, Scene, Inspector, Graph, Console 패널 경계는 드래그해 폭과 높이를 조정할 수 있고, 프리셋과 패널 체크박스로 빠르게 작업 배치를 바꿀 수 있습니다.
   Inspector 패널의 `Detach Inspector`를 누르면 선택 노드의 텍스트와 속성 편집기를 별도 창에 띄워 Graph 캔버스를 넓게 쓸 수 있습니다.
   저장하지 않은 그래프 편집이 있으면 다른 그래프로 전환하거나 새 그래프를 만들기 전에 저장해야 합니다.
   그래프 ID를 바꾸면 씬 시작 그래프와 `CallGraph` 참조가 함께 갱신됩니다.
   씬 시작 그래프나 `CallGraph` 노드에서 참조 중인 그래프는 삭제되지 않습니다.
7. Preview 패널에서 현재 그래프를 분리된 런타임 경로로 실행해 확인합니다.
   `Detach Preview`를 누르면 같은 Preview 상태를 별도 창으로 띄워 그래프/씬 편집 화면과 나란히 확인할 수 있습니다.
8. Localization 탭에서 지원 locale을 추가/삭제하고, 선택한 locale의 문자열을 추가/수정/삭제하며 누락 값을 채웁니다.
   locale을 삭제하면 문자열 테이블의 해당 locale 값도 함께 제거됩니다.
   그래프의 `textKey`에서 참조 중인 문자열은 삭제되지 않습니다.
9. Theme 탭에서 런타임 대사창, 선택지, 세이브/백로그 오버레이, 텍스트 색상과 이미지 스킨 에셋 ID를 수정하고 `project.pompo.json`에 저장합니다.
   Animation Presets에서 `Instant`, `Subtle`, `Snappy`, `Cinematic` 중 하나를 고르면 패널 페이드, 선택지 pulse, 텍스트 reveal, 자동 진행/스킵 타이밍이 함께 채워집니다.
10. Build 패널에서 빌드 프로필을 만들거나 수정/삭제하고, 플랫폼을 선택해 독립 실행 폴더를 생성합니다.
   마지막 빌드 프로필은 삭제되지 않습니다.
   빌드 결과는 프로젝트의 `Settings/build-history.pompo.json`에 기록되어 이후에도 Build 패널과 CLI에서 확인할 수 있습니다.
11. Help 탭에서 주요 문서 경로, repository gate, project gate, release gate 명령을 빠르게 확인합니다.

Console 패널의 `Detach Console`을 누르면 프로젝트 진단과 깨진 에셋 요약을 별도 창에서 계속 확인할 수 있습니다.

## 3. CLI로 프로젝트 만들기

샘플 프로젝트를 만듭니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- init --path /tmp/MyVN --name MyVN --template sample --json
```

프로젝트 검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- validate --project /tmp/MyVN --json
```

CI나 릴리스 스크립트에서 진단과 실행 환경을 수집할 때는 `init`, `version`, `doctor`, `validate`, `build`, `profile list/show/delete/save`, `history list/clear`, `asset list/import/delete/verify/rehash`, `save list/delete`, `localization report/repair/add-locale/delete-locale`, `docs site`에 `--json`을 붙일 수 있습니다.
검증은 캐릭터 표현 ID 중복, 빈 표현 ID, 존재하지 않는 기본 표현도 빌드 전에 실패로 처리합니다.
선택지의 `enabled`가 boolean이 아니거나 `enabledVariable`이 비어 있거나 문자열이 아니면 `POMPO043` 진단으로 실패합니다.

## 4. 에셋 관리

에셋 목록 확인:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset list --project /tmp/MyVN --type Image --json
```

에셋 가져오기:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset import --project /tmp/MyVN --file ./background.png --type Image --asset-id bg-main --json
```

`assetId`는 에셋 파일명으로도 사용되므로 영문/숫자와 `-`, `_`, `.`만 사용할 수 있으며 `.`으로 시작하거나 끝날 수 없습니다.
빌드 시 `graphId`도 IR 파일명으로 사용되므로 같은 파일명 안전 규칙을 따라야 합니다.

해시 검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset verify --project /tmp/MyVN --json
```

미사용 에셋 삭제:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset delete --project /tmp/MyVN --asset-id unused-bg --json
```

씬, 캐릭터, 그래프에서 참조 중인 에셋은 삭제되지 않습니다. 메타데이터만 지우고 파일은 남기려면 `--keep-file`을 추가합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset delete --project /tmp/MyVN --asset-id unused-bg --keep-file --json
```

파일을 외부 도구로 교체한 뒤 해시만 갱신하려면 다음 명령을 사용합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- asset rehash --project /tmp/MyVN --json
```

## 5. 빌드

빌드 프로필은 에디터 Build 패널이나 CLI에서 만들 수 있습니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile list --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile save --project /tmp/MyVN --name demo --platform MacOS --app-name MyVN --version 0.1.0 --data-only --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile show --project /tmp/MyVN --name demo
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- profile delete --project /tmp/MyVN --name demo --json
```

마지막 빌드 프로필은 삭제되지 않으며, 프로필 이름은 영문/숫자와 `-`, `_`, `.`만 사용할 수 있고 `.`으로 시작하거나 끝날 수 없습니다.
`doctor --project`는 `BuildProfiles/*.pompo-build.json` 전체를 검사해 깨진 JSON, 안전하지 않은 파일명, 파일명과 내부 `profileName` 불일치를 보고합니다.
또한 `appName`, `version`, `iconPath`, `runtimeProjectPath` 같은 프로필 메타데이터 문제도 빌드 전에 보고합니다.

릴리스 빌드 프로필로 현재 OS에 맞는 결과물을 만듭니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --profile-file /tmp/MyVN/BuildProfiles/release.pompo-build.json --output /tmp/MyVN/Builds
```

플랫폼을 명시하려면 `--platform`을 추가합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --output /tmp/MyVN/Builds --platform MacOS
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build --project /tmp/MyVN --output /tmp/MyVN/Builds --platform MacOS --json
```

결과 폴더에는 런타임, 컴파일된 데이터, 에셋, 사용자 스크립트, 설정 파일만 포함되어야 하며 에디터 어셈블리는 포함되지 않아야 합니다.
프로젝트에 `Scripts/**/*.cs`가 있으면 빌드 중 `Pompo.UserScripts.dll`로 컴파일되고 manifest에 포함됩니다.
스크립트 원본 `.cs` 파일은 결과 폴더에 복사되지 않으며, 기본 설정에서는 파일 시스템, 네트워크, 프로세스 실행 API를 참조하면 빌드가 실패합니다.
필요한 경우 `project.pompo.json`의 `scriptPermissions.allowFileSystem`, `allowNetwork`, `allowProcessExecution` 값을 명시적으로 `true`로 바꿉니다.
`System.Reflection`, `System.Runtime.Loader`, `System.Type.GetType`, `System.Activator.CreateInstance` 같은 reflection/런타임 로딩 우회 경로는 권한 설정과 무관하게 차단됩니다.
커스텀 그래프 노드는 `nodeType`, `customNodeType`, `type` 중 하나에 사용자 스크립트 클래스명 또는 전체 타입명을 넣어 실행합니다.
`PompoCommandNode`는 `out` 포트로 진행하고, `PompoConditionNode`는 결과에 따라 `true` 또는 `false` 포트로 진행합니다.
에디터 Graph 패널은 프로젝트 스크립트를 컴파일해 커스텀 노드 선택기에 표시하고, `[PompoNodeInput]` 속성 메타데이터의 기본값을 노드 속성으로 채웁니다.

런타임 색상은 에디터 Theme 탭이나 `project.pompo.json`의 `runtimeUiTheme`에서 바꿀 수 있습니다. 값은 `#RRGGBB` 또는 `#RRGGBBAA` 형식입니다.
잘못된 값은 런타임에서 기본 색상으로 대체되지만, 에디터 저장, `validate`, 빌드 단계에서는 `POMPO038` 진단으로 실패합니다.

```json
{
  "runtimeUiTheme": {
    "canvasClear": "#181A20",
    "dialogueBackground": "#0C0E12DC",
    "nameBoxBackground": "#48546E",
    "choiceSelectedBackground": "#2563EBEB",
    "text": "#FFFFFF",
    "mutedText": "#CBD5E1",
    "accentText": "#93C5FD"
  }
}
```

런타임 패널 이미지는 Theme 탭이나 `project.pompo.json`의 `runtimeUiSkin`에서 바꿀 수 있습니다. 값은 프로젝트 Asset Database에 등록된 이미지 `assetId`입니다.
충분히 큰 스킨 이미지는 런타임에서 9-slice 패널로 그려 모서리 품질을 유지하고, 너무 작은 이미지는 단순 stretch로 대체합니다.
비워 두면 같은 슬롯의 `runtimeUiTheme` 색상을 사용합니다. 존재하지 않는 에셋은 `POMPO004`, 이미지가 아닌 에셋은 `POMPO005` 진단으로 실패합니다.
에디터 Theme 탭 저장도 같은 검증을 먼저 실행하므로 깨진 스킨 참조는 `project.pompo.json`에 저장되지 않습니다.
스킨 에셋은 실제 참조로 계산되므로 미사용 에셋 삭제 대상에서 제외됩니다.

```json
{
  "runtimeUiSkin": {
    "dialogueBox": {
      "assetId": "ui-dialogue-box",
      "type": "Image"
    },
    "nameBox": {
      "assetId": "ui-name-box",
      "type": "Image"
    },
    "choiceBox": {
      "assetId": "ui-choice",
      "type": "Image"
    },
    "choiceSelectedBox": {
      "assetId": "ui-choice-selected",
      "type": "Image"
    },
    "choiceDisabledBox": {
      "assetId": "ui-choice-disabled",
      "type": "Image"
    },
    "saveMenuPanel": {
      "assetId": "ui-save-menu",
      "type": "Image"
    },
    "backlogPanel": {
      "assetId": "ui-backlog",
      "type": "Image"
    }
  }
}
```

런타임 UI 배치는 에디터 Theme 탭이나 `project.pompo.json`의 `runtimeUiLayout`에서 조정할 수 있습니다. Theme 탭은 가상 캔버스 미리보기를 함께 보여주므로 대사창, 선택지, 세이브 메뉴, 백로그 영역 위치를 확인하면서 숫자를 조정할 수 있습니다.
대사창, 이름창, 세이브 메뉴, 백로그 영역은 미리보기에서 직접 드래그해 위치를 바꿀 수 있고, 우하단 핸들을 드래그해 크기를 바꿀 수 있습니다. 변경된 좌표와 크기는 같은 입력 필드에 반영됩니다.
`Reset Layout`을 누르면 기본 레이아웃 값으로 되돌릴 수 있으며, 되돌린 값을 유지하려면 Theme 탭의 저장 버튼을 눌러야 합니다.
사각형은 1920x1080 기본 가상 캔버스 좌표를 사용하며, 프로젝트의 `virtualWidth`/`virtualHeight` 안에 들어가야 합니다.
잘못된 크기나 캔버스를 벗어난 값은 `POMPO039` 진단으로 실패합니다.
에디터 Theme 탭 저장도 같은 검증을 먼저 실행하므로 캔버스를 벗어난 레이아웃은 `project.pompo.json`에 저장되지 않습니다.

```json
{
  "runtimeUiLayout": {
    "dialogueTextBox": {
      "x": 120,
      "y": 810,
      "width": 1680,
      "height": 190
    },
    "dialogueNameBox": {
      "x": 150,
      "y": 755,
      "width": 420,
      "height": 54
    },
    "choiceBoxWidth": 720,
    "choiceBoxHeight": 56,
    "choiceBoxSpacing": 14,
    "saveMenuBounds": {
      "x": 1260,
      "y": 60,
      "width": 560,
      "height": 720
    },
    "saveSlotHeight": 60,
    "saveSlotSpacing": 10,
    "backlogBounds": {
      "x": 260,
      "y": 120,
      "width": 1400,
      "height": 840
    }
  }
}
```

런타임 UI 애니메이션은 Theme 탭이나 `project.pompo.json`의 `runtimeUiAnimation`에서 조정할 수 있습니다. 현재 런타임은 패널 fade-in, 선택된 선택지 pulse, 대사 타이핑 reveal을 적용합니다.
Theme 탭의 가상 캔버스 미리보기는 animation 값을 함께 반영해 패널 fade 중간 상태, 선택지 pulse 최대 확대 상태, 텍스트 reveal 속도 값을 보여줍니다.
`panelFadeMilliseconds`, `choicePulseMilliseconds`, `textRevealCharactersPerSecond`는 0 이상이어야 하며, `choicePulseStrength`는 0부터 1 사이 값이어야 합니다. `textRevealCharactersPerSecond`를 0으로 두면 대사가 즉시 표시됩니다. 잘못된 값은 `POMPO040` 또는 `POMPO041` 진단으로 실패합니다.

```json
{
  "runtimeUiAnimation": {
    "enabled": true,
    "panelFadeMilliseconds": 160,
    "choicePulseMilliseconds": 900,
    "choicePulseStrength": 0.12,
    "textRevealCharactersPerSecond": 45
  }
}
```

런타임 자동 진행과 스킵 속도는 Theme 탭이나 `project.pompo.json`의 `runtimePlayback`에서 조정할 수 있습니다.
`autoForwardDelayMilliseconds`는 자동 진행이 다음 대사로 넘어가기 전에 기다리는 시간이고, `skipIntervalMilliseconds`는 스킵 모드에서 한 줄을 넘기는 간격입니다. 둘 다 0 이상이어야 하며, 잘못된 값은 `POMPO042` 진단으로 실패합니다.

```json
{
  "runtimePlayback": {
    "autoForwardDelayMilliseconds": 1250,
    "skipIntervalMilliseconds": 80
  }
}
```

패키징 전에 빌드 폴더와 `pompo-build-manifest.json`이 일치하는지 검증할 수 있습니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/MyVN/Builds/MacOS/release --require-smoke-tested-locales --require-self-contained
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- build verify --build /tmp/MyVN/Builds/MacOS/release --json
```

빌드 이력을 확인하거나 초기화하려면 다음 명령을 사용합니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history list --project /tmp/MyVN
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history list --project /tmp/MyVN --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- history clear --project /tmp/MyVN --json
```

## 6. 런타임 직접 실행

빌드 결과의 IR을 헤드리스로 실행해 자동 검증할 수 있습니다.

런타임 옵션 확인:

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --help
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --version --json
```

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --play-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --choices 1 --json-trace
```

`--choices`는 쉼표로 구분한 0부터 시작하는 선택지 인덱스 목록입니다. 음수나 숫자가 아닌 값은 명확한 오류로 실패합니다.

저장 슬롯까지 포함한 인터랙티브 실행:

```bash
dotnet run --project src/Pompo.Runtime.Fna/Pompo.Runtime.Fna.csproj -- --run-ir /tmp/MyVN/Builds/MacOS/release/Data/graph_intro.pompo-ir.json --choices 1 --saves /tmp/MyVN/Saves
```

인터랙티브 런타임에서는 `Space`, `Enter`, 좌클릭이 대사 타이핑 reveal을 먼저 끝까지 표시한 뒤 다음 입력에서 진행합니다.
선택지는 마우스 hover나 `Up`/`Down`으로 하이라이트를 이동하고, 좌클릭이나 `Enter`로 확정합니다.
비활성 선택지는 흐리게 표시되며 마우스 hover, 좌클릭, `Up`/`Down`, `Enter`로 확정되지 않습니다. `--choices` trace 실행에서 비활성 선택지 인덱스를 지정하면 명확한 오류로 실패합니다.
저장 슬롯은 마우스 hover나 `Up`/`Down`으로 선택하고, 슬롯 좌클릭이나 `Enter`로 로드합니다. 빈 슬롯 로드는 런타임에서 차단됩니다.

저장 슬롯 확인:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save list --saves /tmp/MyVN/Saves
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- save list --saves /tmp/MyVN/Saves --json
```

## 7. 배포 패키지

빌드 결과를 zip 패키지와 릴리스 매니페스트로 묶습니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release package --build /tmp/MyVN/Builds/MacOS/release --output /tmp/MyVN/Releases --name MyVN-0.1.0-macos --json
```

릴리스 검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```

공개 직전 릴리스 감사:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```

`release audit`는 저장소 doctor, 생성된 문서 사이트(`artifacts/docs-site`), 릴리스 매니페스트 검증을 한 번에 실행합니다. 최종 공개 전에 `docs site`를 먼저 생성하고, `release package`로 만든 `.release.json` 경로를 `--manifest`에 전달합니다.

릴리스 서명과 서명 검증:

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release sign --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --private-key ./release-private.pem --json
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release verify-signature --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --public-key ./release-public.pem --json
```

## 8. 자주 쓰는 개발 확인 명령

```bash
dotnet build PompoEngine.slnx --no-restore
dotnet test PompoEngine.slnx --no-build
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- doctor --repository --root .
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- release audit --root . --manifest /tmp/MyVN/Releases/MyVN-0.1.0-macos.release.json --require-smoke-tested-locales --require-self-contained --json
```

## 9. 문서 사이트 생성

저장소 Markdown 문서를 정적 HTML 사이트로 묶습니다.

```bash
dotnet run --project src/Pompo.Cli/Pompo.Cli.csproj -- docs site --root . --output artifacts/docs-site --json
```

결과물은 `artifacts/docs-site/index.html`, `artifacts/docs-site/pages/*.html`, `artifacts/docs-site/pompo-docs-site.json`입니다.
