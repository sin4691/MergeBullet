# Fonts

| 파일 | 폰트 | 쓰는 언어 | 라이선스 |
|---|---|---|---|
| NotoSans-Regular.ttf | Noto Sans (Google) | 영어·독일어·프랑스어·스페인어·포르투갈어·이탈리아어·폴란드어·러시아어 | SIL OFL 1.1 |
| NanumGothic.ttf, NanumGothicBold.ttf | 나눔고딕 (NAVER) | 한국어 | SIL OFL 1.1 |
| NotoSansJP-Regular.otf | Noto Sans JP | 일본어 | SIL OFL 1.1 |
| NotoSansSC-Regular.otf | Noto Sans SC | 중국어 간체 | SIL OFL 1.1 |
| NotoSansTC-Regular.otf | Noto Sans TC | 중국어 번체 | SIL OFL 1.1 |

- 출처: Noto Sans는 github.com/notofonts/notofonts.github.io, Noto Sans JP/SC/TC는 github.com/notofonts/noto-cjk (SubsetOTF). 라이선스 전문은 `OFL-Noto.txt`.
- `Assets/` 폴더의 UI-*.asset은 UI Toolkit용 폰트 에셋(SDF)으로, `MergeBullet > Setup Localization` 메뉴가 만든다.
  - 언어마다 폰트를 바꾸는 이유: 한자는 일본어·간체·번체마다 글자 모양이 달라서 한 폰트로 섞어 쓰면 어색하다.
  - UI-Latin은 나머지 폰트를 대체(fallback)로 갖고 있어서, 언어 목록처럼 여러 언어가 섞인 글자도 깨지지 않는다.
  - 동적(Dynamic) 모드라 실행 중에 쓰는 글자만 아틀라스에 굽는다. 에디터에서 Play하면 .asset 파일이 바뀔 수 있다.
- SIL OFL 1.1은 게임에 포함해 배포할 수 있다. 폰트 파일 자체를 단독으로 판매하지 않고, 이름을 바꿔 수정본을 배포하지 않는 조건.
  전문: https://openfontlicense.org
- 맑은 고딕 등 OS 기본 폰트는 Microsoft 라이선스라 게임에 포함하지 않는다.
