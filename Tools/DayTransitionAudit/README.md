# 낮·밤 전환 컷신 렌더링 검증

`CaptureTransitions.cs`는 사용자 프로젝트를 건드리지 않고, 별도 Unity 6000.3.11f1 프로젝트에서 `DayTransitionView`를 실제로 그려 PNG로 저장합니다.
`-batchmode -executeMethod CaptureTransitions.Run -screen-width 1080 -screen-height 1920`로 실행하고, `-nographics`는 쓰지 않습니다.

임시 프로젝트에 넣는 파일 (대응하는 `.meta` 포함):

- `Assets/02_Scripts/GameUI/DayTransitionView.cs`, `DayTransitionSettings.cs`, `NightFxSprites.cs` → `Assets/Scripts/`
- 이 폴더의 `CaptureTransitions.cs` → `Assets/Editor/`
- `Assets/Resources/DayTable/Deck.png`, `Assets/Resources/GameHud/PhasePlaque.png`, `ParchmentPanel.png`, `Assets/Resources/DayTransition/*`
  → 같은 경로
- `Assets/Fonts/MalgunGothic SDF.asset`, `malgun.ttf`, `Assets/TextMesh Pro` → 같은 경로
- 시험용 그림 → `Assets/Art/`: `09_Image/SceneBackgrounds/Night/PIRATE_SHARED_Night.png`, `09_Image/Portrait/CREW_DRUNK.png`,
  `09_Image/Characters/CrewDrunk_SD/CrewDrunk_BaseSheet.png`
- `Packages/manifest.json`: `com.unity.ugui` 2.0.0과 imageconversion·imgui·jsonserialize·ui·uielements 모듈

`Output/`에 장면별 PNG(사망·보호·무사·처형·처형 없음, 처형은 1080×2340 화면도)와 `status.txt`(PASS/실패 이유, 장면별 멈춤·끝 시각)가 나옵니다.
검사: Resources 그림 5장 로드, 양피지 9-slice, 컷신이 캔버스 맨 위, 끝난 뒤 완전히 투명, 글꼴에 없는 글자,
시간 설정 파일 로드(기본값), 구간을 두 배로 늘리면 같은 그림이 그만큼 늦게 나오는지, 걷힘 시간, 게임 종료 시 마지막 구간 생략.
실제 서버 페이즈와의 맞물림(다음 페이즈가 오면 걷힘, 재접속 시 재생 안 함)은 플레이로 확인할 부분입니다.
