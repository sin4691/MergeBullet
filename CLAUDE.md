# Merge Bullet

Unity 6 + Entities(ECS)로 만드는 1인 개발 로그라이크 서바이버. PC(Windows)·Android 출시 목표, 포트폴리오용.

## 핵심 규칙

- 자동 발사된 총알이 벽·적에 튕김. 같은 레벨 2개가 닿으면 다음 레벨 1개로 합성
- 레벨 4가 되면 캐릭터별 특수효과(폭발, 번개 등) 후 소멸
- 오래 레벨 변화가 없는 총알은 소멸

## 기술 원칙

- 게임 로직은 ECS(ISystem + IJobEntity, Burst). MonoBehaviour는 Authoring/UI만
- 컴포넌트는 struct, 데이터만. 로직은 시스템에
- Unity Physics 사용 안 함. 튕김·충돌은 직접 계산, 합성 검사는 공간 해시
- 코드는 Assets/_Project/ 아래 Scripts/{Components,Authoring,Systems}

## 작업 방식

- 나는 면접에서 설명할 수 있어야 함. 코드를 바꾸면 무엇을 왜 바꿨는지 짧게 설명할 것
- 성능 관련 변경은 전후 수치(FPS, ms)를 기록할 것
- 커밋은 기능 단위로, 메시지는 영어로
- Library/, Temp/, Logs/는 건드리지 말 것
