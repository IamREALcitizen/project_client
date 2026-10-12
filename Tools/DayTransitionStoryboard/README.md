# 낮·밤 전환 연출 스토리보드

밤 결과(NIGHT_RESULT)와 처형(EXECUTION) 때 보여 줄 컷신 시안입니다. 결과 이미지는 `Docs/UI/DayTransition_*.png`에 있습니다.

| 파일 | 조건 |
| --- | --- |
| DayTransition_A_Killed.png | 밤사이 사망 (`killedPlayerId ≠ 0`) |
| DayTransition_B_Protected.png | 선의 보호로 생존 (`protectedByDoctor`) |
| DayTransition_C_Quiet.png | 아무 일도 없음 |
| DayTransition_D_Executed.png | 투표로 처형 (`executedPlayerId ≠ 0`) |

`storyboard.html`이 프로젝트의 실제 에셋(갑판 배경, 원탁, 캐릭터 시트, 초상화, HUD 프레임)을 합성해 그립니다.
칼자국·붕대 띠 같은 효과는 CSS/SVG로 그린 자리표시입니다. 최종 아트로 바꿀 때 구도 참고용입니다.

처형 ③ 널빤지·④ 첨벙은 스토리보드 합성본을 바탕으로 Gemini로 다시 그린 그림(`art/D3_Plank.png`, `art/D4_Splash.png`, 576×1024)을 씁니다.
오른쪽 아래 Gemini 별 표시는 바로 오른쪽 영역을 복제해 덮었습니다. 메모·자막·지훈님 스킨이 그림에 들어 있어 게임에 그대로 쓸 수는 없습니다.

```bash
bash Tools/DayTransitionStoryboard/render.sh              # Docs/UI에 4장 (1.5배)
bash Tools/DayTransitionStoryboard/render.sh out 1 kill/3  # 컷 하나만 확인
```

Chrome(없으면 Edge) 헤드리스로 찍습니다.
