# 망자들의 밤 방 검증

사망한 플레이어는 직업·진영과 무관하게 `DEAD_SHARED` 방에서 모든 사망자와 만납니다. 기존 유령선 배경을 사용합니다.
5명부터 최대 4명씩 여러 줄로 배치하며, 내 캐릭터는 앞줄에 있습니다. 이후 사망자는 합류하고 부활자는 빠집니다.
살아 있는 플레이어의 직업·해적·세이렌 방 규칙은 유지됩니다.

Unity에서 프로젝트를 한 번 컴파일한 다음 프로젝트 루트의 PowerShell에서 실행합니다.
준비 스크립트는 현재 소스를 컴파일하고 `Logs/NightRoomAudit/Project`에 필요한 코드·아트의 사본을 만듭니다.
원본 씬과 동적 폰트 에셋은 저장하지 않습니다.

```powershell
./Tools/NightRoomAudit/Prepare.ps1
$projectPath = (Resolve-Path Logs/NightRoomAudit/Project).Path
$logPath = Join-Path (Resolve-Path Logs/NightRoomAudit).Path 'Unity.log'
Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe' -WindowStyle Hidden -Wait -ArgumentList @('-batchmode', '-projectPath', ('"' + $projectPath + '"'), '-executeMethod', 'CaptureDeadRoom.Run', '-logFile', ('"' + $logPath + '"'))
Get-Content Logs/NightRoomAudit/NightDeadRoomOutput/status.txt
```

`-nographics`는 사용하지 않습니다. `CaptureDeadRoom.Run`은 빈 씬을 열므로 반드시 검증용 사본에서만 실행합니다.

- 기존 및 신규 `NightRoomRulesTests`의 NUnit 테스트 22개를 실행합니다.
- 사망자의 진영·세이렌 팀·접선 이력, 명단과 `/me` 갱신 시차, 첫 사망자, 부활, 추가 사망, 12명 구성 등을 검사합니다.
- 실제 `NightRoomView`로 1~12명 배치와 명단 재갱신, 사망 시 이전 능력 연출 취소, 생존 동료 제거, 낮/밤 결과 표시, 새 게임 초기화를 확인합니다.
- 1·4·8·12명 화면을 `NightDeadRoomOutput/Dead_*.png`로 저장합니다. 전체 HUD나 실서버 통신을 포함한 캡처는 아닙니다.
