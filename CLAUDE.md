# CLAUDE.md

<!-- 이 프로젝트에만 해당하는 내용만 적는다. 코딩 지침, CHANGELOG 규칙, 멀티 에이전트 규칙, 스킬은 구글 드라이브 AgentSync의 전체 메모리에서 공통으로 불러온다. -->
<!-- 0_Startup을 복사해 새 프로젝트를 만들면 아래 칸을 채우고, 쓸 내용이 없는 칸은 지운다. -->

## 프로젝트 개요
<!-- 전시명, 설치 장소, 씬 흐름 -->

## 하드웨어·외부 연동
- 1존에는 좌·우 PC 2대가 들어가고, 조명·장치를 움직이는 아두이노 1대를 두 PC가 함께 쓴다.
- 아두이노는 USB 시리얼이 아니라 랜선(LAN)으로 연결되며 UDP로 명령을 받는다. Template의 `ArduinoManager`(COM 포트 스캔)는 쓰지 않고 `Network/ArduinoUdpClient` 가 보낸다. 주소·포트는 `StreamingAssets/Json/Arduino.json`.
- 명령은 레벨 번호를 붙인 문자열이다: 레벨을 골라 스토리가 나올 때 `n_enter`, 결과 씬 입장 때 양호면 `n_success`·보통·부족이면 `n_fail`, enter 뒤 결과 씬 다음 버튼이나 무입력 타임아웃(move_idle_timeout) 때 `n_exit` 한 번. 두 PC가 같은 명령을 보내면 아두이노가 쌓아 차례로 실행한다.
- 아두이노는 받은 명령을 보낸 PC의 IP·포트로 그대로 돌려주고(`1_exit` → `1_exit`), Unity는 로그로만 남긴다.

## 공통 규칙의 예외
<!-- 예: 씬 매니저는 GameManagerBase 대신 MonoBehaviour 유지 -->
