# 게임 HUD 아이콘·사망 리본 검증

`Resources` 정리 후 빠진 아이콘 13개는 원래 GUID를 유지한 채 `Assets/Resources/Sprites/flaticon/white/128`로 옮겼습니다. 낮/밤, 채팅 탭·필터, 알림 등에 쓰이는 전체 29개 아이콘의 실제 로딩, 투명도, 4의 배수 크기를 검사합니다.

사망 표시는 초상화의 원형 마스크 안에 어두운 덮개와 금색 테두리의 붉은 `사망` 리본을 그립니다. UI 도형과 TMP 텍스트를 사용하므로 별도의 리본 텍스처나 리사이즈가 필요하지 않습니다. 초상화 테두리와 닉네임은 흐리게 표시합니다.

Unity에서 원본 프로젝트를 한 번 컴파일한 뒤 PowerShell에서 실행합니다. 준비 스크립트는 최신 소스를 컴파일해 검증용 프로젝트에 복사합니다. 원본 씬과 동적 폰트는 저장하지 않습니다.

```powershell
./Tools/GameHudAudit/Prepare.ps1
$auditProject = (Resolve-Path Logs/GameHudAudit/Project).Path
$auditLog = Join-Path (Resolve-Path Logs/GameHudAudit).Path 'Unity.log'
Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe' -WindowStyle Hidden -Wait -ArgumentList @('-batchmode', '-projectPath', ('"' + $auditProject + '"'), '-executeMethod', 'CaptureGameHud.Run', '-logFile', ('"' + $auditLog + '"'))
Get-Content Logs/GameHudAudit/Output/status.txt
```

검증 결과와 1080×640 캡처는 `Logs/GameHudAudit/Output`에 생성됩니다.

- 실제 채팅 탭·필터의 스프라이트 누락 여부
- 4·8·12명에서 실제 `GameLayoutTabs` 명단 렌더링
- 사망자에게만 리본 표시, 사망→부활→재사망 갱신 및 중복 생성 방지
- 한글 글리프, 원형 마스크, 닉네임, 클릭 가로채기 여부

`CaptureGameHud.Run`은 빈 씬을 열기 때문에 준비 스크립트가 만든 검증용 프로젝트에서만 실행합니다. 전체 게임씬·서버 통신을 실행하는 검증은 아닙니다.
