# GameScene3 화면 캡처

`CaptureGameScene3.cs`는 GameScene3을 씬에 저장된 가짜 서버 설정(내 직업 선의)으로 Play해 화면을 PNG로 저장합니다.
열려 있지 않은 프로젝트(원본이 닫혀 있으면 원본, 아니면 ParrelSync 복제본)의 `Assets/Editor/`에 복사해 실행하고, 끝나면 지웁니다.
`-nographics`는 쓰지 않습니다.

```
Unity.exe -batchmode -projectPath <프로젝트> -executeMethod CaptureGameScene3.Run -auditOut <폴더> -logFile <로그>
```

- `<폴더>`에 `Night_1920`, `Night_2400`, `NightPicked_1920`(지훈을 보호 대상으로 고름), `Day_1920`, `Day_2400`, `Vote_1920` PNG와
  `status.txt`(PASS/실패 이유)가 나옵니다. 화면 높이 2400은 긴 휴대폰에서 채팅 창이 늘어나는 모양을 봅니다.
- 캔버스를 카메라 하나로 RenderTexture(가로 1080)에 그립니다. 시간은 가짜 서버의 기본 페이즈 길이(밤 20초, 결과 5초, 낮 15초,
  투표 15초)에 맞춰 찍습니다.
- `-executeMethod CaptureGameScene3.Dress`는 구운 GameScene3에 항해 시안을 입히고(Tools > Game Scene > 항해 시안 입히기와 같음) 저장합니다.
- Play를 거치면 TextMesh Pro의 동적 글꼴 에셋(`Assets/Fonts/*SDF.asset`, `CabinFont.asset`)이 비워진 채 저장될 수 있습니다.
  실행 뒤 `git status`로 확인하고 되돌리세요.
- 서버가 없어 채팅에 "로그인 실패" 알림 줄이 하나 생깁니다. 화면 확인용이며 실제 서버·멀티 클라이언트 동작은 플레이로 확인합니다.
