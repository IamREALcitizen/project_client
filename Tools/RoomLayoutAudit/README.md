# Room UI 렌더링 검증

`CaptureRoom.cs`는 사용자 프로젝트를 Play 모드로 전환하지 않고 별도 Unity 6000.3.11f1 프로젝트에서 실행한다. `-batchmode -executeMethod CaptureRoom.Run -screen-width 1080 -screen-height 1920`으로 실행하며 `-nographics`는 사용하지 않는다.

임시 프로젝트에 아래 실제 파일과 대응하는 `.meta`를 복사한다.

- `Assets/01_Scenes/Room.unity` → `Assets/Room.unity`
- RoomLobbyLayout, RoomSettingsPanel, RoomHelmGraphic, RoomPlayerView
- RoomDtos, RoomSession, AuthSession, StatusMessageView
- DesignSpriteCatalog, ChatLogView, ChatInputBar, ChatEntry
- `Assets/Resources/RoomLobby`, `Assets/Resources/GameHud/Portraits.asset`
- `Assets/09_Image/Portrait`, `Assets/Fonts`, `Assets/TextMesh Pro`
- `Assets/03_Prefabs/Room`, CommonScroll, ChatLogPanel, RoomItem 프리팹
- 이 폴더의 `CaptureRoom.cs`, `PrepareRoomArt.cs` → `Assets/Editor/`

패키지는 원본 프로젝트의 로컬 `com.unity.ugui` 캐시와 Unity UI, IMGUI, JSON, ImageConversion, Physics, Physics2D 모듈을 사용한다. 통신 컨트롤러는 실행하지 않는다. 씬에 남은 미복사 스크립트/아트 참조는 검증용 프로젝트에만 한정된다.

`Output/status.txt`의 PASS와 PNG를 함께 확인한다. 검사 범위는 참가자/빈자리 수, 최대 인원 감소, 스크롤, 방장 관리 콜백, 참가자 권한, 한글 glyph, 설정 창, 원래 채팅 UI의 열기/닫기, 세로/가로 렌더링이다. 실제 API·멀티클라이언트 통신은 별도 플레이 검증 대상이다.

다듬은 시안은 원형 초상화 마스크, 왕관/체크/대기 아이콘, 긴 본인 닉네임과 배지 간격도 확인하며 총 9장의 PNG를 저장한다. `PrepareRoomArt`는 새 이미지의 투명 여백을 Sprite rect에서 제외하고 9-slice border를 설정한다. PNG 픽셀을 변경하지 않는다. 아트를 새로 가져온 경우 검증 프로젝트의 Refined PNG `.meta`를 원래 리소스 디렉터리로 복사한다.

## 직업 구성 항해일지

같은 임시 프로젝트에 아래 실제 파일을 추가하고 `-executeMethod CaptureRoles.Run`으로 실행한다.

- `Assets/01_Scenes/Lobby.unity` → `Assets/Lobby.unity`
- `CreateRoomRoleEditor.cs`, `RoleCompositionLayout.cs`
- `RoleSetupDtos.cs`, `RoleSetupRules.cs`, `GameConstants.cs`, `GameDtos.cs`
- `Assets/Resources/RoomLobby/VoyageLogbook.png` 및 대응 `.meta`
- `CaptureRoles.cs` → `Assets/Editor/CaptureRoles.cs`

기존 로비의 `Popup_JobCustom` 구조에 실제 편집기를 연결하고 샘플 직업 데이터로 검사한다. 런타임과 분리된 편집 모드 검사이므로 EventSystem/Raycaster의 OnEnable을 명시적으로 호출한다. 서버나 로비 통신 컨트롤러는 실행하지 않는다. 결과는 `RolesOutput/status.txt` 및 7장의 PNG에 저장한다. 원본 설정과 편집 복사본의 분리, 취소, 단일 적용 콜백, 랜덤 전환/후보 저장, 직접 구성 복원, 잘못된 다른 인원 구성의 차단, 4–12명 저장 표, 스크롤, 고정 하단, 실제 버튼 raycast를 확인한다.

## 룸씬 진입 경로 회귀 검사

`CaptureRoomRoles.Run`은 실제 `Room.unity`에 직렬화된 `RoomSettingsPopup`을 열고, 그 안의 `Button_Custom`을 눌러 항해일지까지 진입한다. 로비의 편집기만 따로 열어 검사하면 룸씬 연결 누락을 발견할 수 없으므로 이 경로를 별도로 유지한다.

위 임시 프로젝트의 Room 씬과 공유 스크립트를 최신 파일로 갱신하고 다음 파일을 추가한다.

- 실제 `RoomSettingsPopup.cs`, `CreateRoomPopup.cs`, `SceneType.cs`, `RoomDtos.cs`, `RoomSession.cs`, `AuthSession.cs`, `ApiResult.cs` 및 `.meta`
- `CaptureRoomRoles.cs` → `Assets/Editor/`
- **검증 전용** `RoomRoleApiStub.cs` → 임시 프로젝트의 `Assets/Scripts/` (실제 게임 Assets에 넣지 않는다)
- Room 씬이 참조하는 기존 UI Sprite와 `.meta` (직업 구성 진입 화면의 누락 아트 방지)

이 검사에서 RoomUIController의 설정 열기 콜백은 같은 `RoomSettingsPopup.Open` 호출로 연결한다. API/실시간 세션은 스텁으로 대체하며 실제 서버나 유저 방을 변경하지 않는다. `RoomRolesOutput/status.txt` 및 PNG에 결과를 남긴다. 직업 목록 실패/재시도, 14개 직업, 저장된 `roleSetup` JSON, 비활성 진입 버튼, 구 UI 콜백 해제, 저장 실패 시 편집 유지, 중복 저장 방지, 성공 후 설정 창 복귀, 기본 설정 입력 보존, 랜덤 요청 본문, 닫힌 창의 늦은 응답 무시, 방장 권한, 버튼 raycast를 확인한다.

## 로비의 방 만들기 진입 경로 검사

`CaptureLobbyRoles.Run`은 실제 `Lobby.unity`의 `CreateRoomPopup.Open` → `Background/GameMode/Button_Custom` → 항해일지 순서로 검사한다. `CaptureRoles`가 공유 편집기 자체를 검사하는 것과 달리, 씬 계층 변경으로 버튼 연결이 빠지는 문제를 찾아낸다.

위 임시 프로젝트에서 `Lobby.unity`, 실제 `CreateRoomPopup.cs`와 `.meta`, 검증용 `RoomRoleApiStub.cs`를 갱신하고 `CaptureLobbyRoles.cs`를 `Assets/Editor/`에 넣는다. 룸씬과 동일한 UI 리소스 외에 로비 씬이 참조하는 기존 Sprite도 복사한다. Sprite 참조를 추출할 때는 `m_Sprite: \{fileID: [-0-9]+, guid: ([a-f0-9]{32})`처럼 줄을 넘지 않는 패턴을 사용한다.

`LobbyRolesOutput/status.txt` 및 PNG에 결과를 남긴다. 진입 버튼 표시·raycast, 기존 모드 탭 숨김, 목록 실패/재시도/늦은 응답 무시, 입력 인원 반영, 14개 직업, 취소·적용, 방 이름/인원 유지, 랜덤 후보/직접 구성 복원, 방 생성 전 불필요한 저장 방지, 생성 후 직업 구성 본문 전달, 저장 실패 시 생성 방 정리/재시도, 한 번의 씬 전환을 검증한다. API와 씬 전환은 스텁이며 실서버 저장 검증은 아니다.
