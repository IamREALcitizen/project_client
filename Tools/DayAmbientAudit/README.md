# 낮 원탁·파도 렌더링 검증

`Prepare.ps1`을 프로젝트 루트에서 실행하면 Unity의 기존 컴파일 참조로 현재 게임 코드를 다시 컴파일하고,
`Logs/DayAmbientAudit/Project`에 검증 전용 프로젝트를 만듭니다. 원본 씬이나 글꼴을 저장하지 않습니다.
Unity 6000.3.11f1 Windows 환경이며, 원본 프로젝트를 한 번 컴파일해 `Library/Bee`와 `Library/ScriptAssemblies`가 있어야 합니다.

```powershell
./Tools/DayAmbientAudit/Prepare.ps1
$auditProjectPath = (Resolve-Path Logs/DayAmbientAudit/Project).Path
$auditLogPath = Join-Path (Resolve-Path Logs/DayAmbientAudit).Path 'Unity.log'
Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe' -WindowStyle Hidden -Wait -ArgumentList @('-batchmode', '-projectPath', ('"' + $auditProjectPath + '"'), '-executeMethod', 'CaptureDayAmbient.Run', '-logFile', ('"' + $auditLogPath + '"'))
Get-Content Logs/DayAmbientAudit/Output/status.txt
```

그래픽 장치가 필요하므로 `-nographics`를 사용하지 않습니다. 실행 중인 검증 프로젝트에 중복 실행하지 마세요.
프로젝트 원본에 `CaptureDayAmbient.cs`를 복사해 실행하면 빈 씬을 여므로 반드시 위 격리 프로젝트에서 실행합니다.

검증 항목:

- GPU로 서로 다른 시간의 바다를 렌더링하고 파도 영역의 픽셀 변화와 바깥 영역의 정지를 비교합니다.
- 낮/밤, 컴포넌트 비활성화/재활성화 때 배경 머티리얼이 복구되는지 확인합니다.
- 반복 명단 갱신과 투표 전환은 착석을 재시작하지 않고, 밤에서 낮으로 돌아오면 다시 착석하는지 확인합니다.
- 모든 방향의 발 위치와 좌우 반전, 착석 하강·눌림, 정면 눈깜빡임·발화 프레임, 사망자의 동작 정지를 확인합니다.
- 인원이 줄었을 때 좌석 재생성을 확인합니다.

결과는 `Logs/DayAmbientAudit/Output/status.txt`, `Ocean_0.png`, `Ocean_1.png`, `Day_00.png`~`Day_23.png`입니다.
낮 캡처는 실제 원탁·캐릭터 코드를 이용하는 독립 무대이며, 전체 HUD·실서버·모바일 기기 성능 검증은 포함하지 않습니다.
