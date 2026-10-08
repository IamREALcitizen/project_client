# SD 캐릭터 세트

첨부한 초상화 20종을 원탁 장면용 SD 캐릭터로 옮긴 에셋입니다. 각 디자인은 전신, 앉은 정면(기본·눈 감기·말하기), 앉은 옆·뒤, 원탁의 1·5·7·11시 방향을 가집니다.

`Assets/03_Prefabs/Characters`의 프리팹을 씬에 배치한 뒤 `ChibiCharacterView`의 `Pose`를 변경하면 자세가 바뀝니다. 외형만 교체하려면 `SetSkin(ChibiCharacterSkin)`을 호출합니다. 정면 대화는 `SetSpeaking(true/false)`로 입 모양과 움직임을 전환합니다.

| 원본 디자인 | 스프라이트 폴더 | 프리팹 |
|---|---|---|
| PIRATE_SPY | PirateSpy_SD | PirateSpy_Chibi |
| CREW_DOCTOR | CrewDoctor_SD | CrewDoctor_Chibi |
| PIRATE_KRAKEN | PirateKraken_SD | PirateKraken_Chibi |
| CONCEPT_SIREN | ConceptSiren_SD | ConceptSiren_Chibi |
| CREW_GUNNER | CrewGunner_SD | CrewGunner_Chibi |
| PIRATE_RAIDER | PirateRaider_SD | PirateRaider_Chibi |
| CREW_SAILOR | CrewSailor_SD | CrewSailor_Chibi |
| CREW_BOATSWAIN | CrewBoatswain_SD | CrewBoatswain_Chibi |
| CREW_GHOST | CrewGhost_SD | CrewGhost_Chibi |
| CREW_CAPTAIN | CrewCaptain_SD | CrewCaptain_Chibi |
| CREW_MONKEY | CrewMonkey_SD | CrewMonkey_Chibi |
| CREW_LOOKOUT | CrewLookout_SD | CrewLookout_Chibi |
| CREW_DRUNK | CrewDrunk_SD | CrewDrunk_Chibi |
| PIRATE_PARROT | PirateParrot_SD | PirateParrot_Chibi |
| CREW_MONKEY_ALT | CrewMonkeyAlt_SD | CrewMonkeyAlt_Chibi |
| CONCEPT_GHOST_CAPTAIN | ConceptGhostCaptain_SD | ConceptGhostCaptain_Chibi |
| PIRATE_COOK | PirateCook_SD | PirateCook_Chibi |
| THIRD_MERMAID_MALE | ThirdMermaidMale_SD | ThirdMermaidMale_Chibi |
| THIRD_MERMAID | ThirdMermaid_SD | ThirdMermaid_Chibi |
| PIRATE_COOK_FEMALE | PirateCookFemale_SD | PirateCookFemale_Chibi |

첫 번째 `PirateSpy_SD`는 자세별 PNG 10장입니다. 나머지 19종은 폴더마다 `BaseSheet.png` 6칸과 `AnglesSheet.png` 4칸을 유니티 다중 스프라이트로 등록했습니다. 각 폴더의 `*Skin.asset`이 10개 프레임을 묶습니다.

원탁 기준으로 11시·1시는 앞 대각선, 7시·5시는 뒤 대각선입니다. 캐릭터의 위치·크기·그리기 순서는 씬에서 지정합니다.
