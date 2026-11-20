## [2.1.1](https://github.com/metalsky/Bible2PPT/releases/tag/v2.1.1) (2026-11-21)

### Bug Fixes

* **합쳐진 구절 PPT 생성 누락 수정** — 개역개정(4판)에서 2~3개의 절이 하나의 `dataSec="18-19"` range로 제공됨.
  기존에는 range의 **첫 번호**(18)로만 저장되어, 요청 범위가 range 내부에 있는 절(예: `신6:19`, `시92:2-3`)을
  요청하면 해당 구절이 PPT에 **누락**(빈 슬라이드/생성 실패)되었음.
  - 신규 **`MergedVerses`** 정적 클래스(`Bible2PPT.Services.BibleService`)로 range(첫 번호~마지막 번호)를
    단일 출처로 관리 (`신6:18-19` 등 11개 장).
  - PPT 빌드 구절 필터(`Builder.cs`)에 `MergedVerses.VerseInRange` 적용: 요청 범위가 range에
    오버플랩하면 합쳐진 구절 포함 + **null 구절(NRE)** 방어.
  - `BibleService.IsMergedVerseChapter`도 동일 클래스로 통합 (캐시 갱신 로직 유지).
  - **단위 테스트 25개** 추가(`MergedVersesTests`), 전체 테스트 **58/58 통과**.
* UI 글자 잘림/깨짐 재스캔 및 **중복 코드 일관화**(`UiText` static class 통합).

### Verification

* 빌드: `dotnet build Bible2PPT.sln -c Release` → 오류 0
* 테스트: `dotnet test` → **58/58 통과**
* godpia: 실제 사이트에서 합쳐진 구절 11개 장 확인(range 값·대소문자) + 파싱 검증
* 릴리스 실행 파일: `build/Bible2PPT/Release/net8.0-windows/Bible2PPT.exe` (Office interop 포함)

## [2.1.0](https://github.com/metalsky/Bible2PPT/compare/v2.0.4...v2.1.0) (2026-11-20)

### New Features

* 갓피아 성경 검색 보완 — 신규 도메인(`https://www.godpia.com`) + 엔드포인트 교체,
  합쳐진 구절(`dataSec="18-19"` range) 파싱 보강
* 갓피아 파싱 로직을 **static 파서**로 분리하고 **HTTP 재시도/백오프**
  (최대 3회, 지수적 백오프 1초/2초, `HttpClient` 타임아웃 30초, 재시도 초과 시
  `WebException` → 기존 UI 재시도 다이얼로그 유지) 도입
* 갓피아 파싱 **단위 테스트**(`Bible2PPT.Services.BibleService.Tests`, HTML 픽처 기반 10개) 추가
* **빌드 산출물 `build/` 폴더 분리** (`Directory.Build.props`): 전체 `bin/`/`obj/`를
  루트 `build/<프로젝트명>/`으로 일괄 이동 (소스 폴더와 산출물 분리, git 관리 용이)
* 설정 메뉴 **다크/라이트 테마** 선택 (달/해 아이콘)
* 설정 메뉴 **생성 PPT 파일(최근 2건)·DB 파일명/크기/리비전/저장시간/템플릿 정보** 표시
* **DB 편집기** (좌측 메뉴 DB 아이콘) — 번역/책/장/절 트리 탐색, 구절 편집,
  다른이름으로 저장, 복원, 리비전/버전 관리, 잘못된 DB 읽기 방지
* `build-activity.log` 등 빌드 활동 기록 포함, **세부 문서화**
  (`docs/기능_명세서.md`, `docs/작업_이력.md`, `docs/개선_안.md`)
* **GitHub API 기반 버전관리** (`metalsky/Bible2PPT`, 태그 `v2.1.0`, 릴리스 노트)

### Bug Fixes

* 갓피아 기존 도메인(`http://bible.godpia.com`, 삭제됨)으로 인한 **검색 에러 수정**
* "오프라인 저장" 버튼 글자 잘림 수정 (버튼 높이 확보)

### Breaking Changes

* 대상 프레임워크 **.NET 8.0-windows** (이전 버전은 .NET Framework 4.8)

---


### Bug Fixes

* fixup d188f62f327780cc78a3d3b07c07649d101bec6f ([7c3ad4a](https://github.com/sunghwan2789/Bible2PPT/commit/7c3ad4a46d6dcb66b4d8c716987674805b67395b))

## [2.0.3](https://github.com/sunghwan2789/Bible2PPT/compare/v2.0.2...v2.0.3) (2022-02-04) [YANKED]


### Bug Fixes

* 실행 파일이 네트워크 폴더에 있을 때 오류 없이 종료되는 현상 수정 ([d188f62](https://github.com/sunghwan2789/Bible2PPT/commit/d188f62f327780cc78a3d3b07c07649d101bec6f)), closes [#87](https://github.com/sunghwan2789/Bible2PPT/issues/87)

## [2.0.1](https://github.com/sunghwan2789/Bible2PPT/compare/v2.0.0...v2.0.1) (2021-05-24)


### Bug Fixes

* PPT 만들기 후 화면 전환 기능 삭제 ([#85](https://github.com/sunghwan2789/Bible2PPT/issues/85)) ([fb3a905](https://github.com/sunghwan2789/Bible2PPT/commit/fb3a905ac85cf1b479a5ff46adef4516e71938fb))

# [2.0.0](https://github.com/sunghwan2789/Bible2PPT/compare/v1.4.0...v2.0.0) (2020-10-04)


### Bug Fixes

* 대상 프레임워크 .NET Framework 4.8로 변경 ([#66](https://github.com/sunghwan2789/Bible2PPT/issues/66)) ([b7d24e8](https://github.com/sunghwan2789/Bible2PPT/commit/b7d24e8c578e6da00abf3d0c05de966a633a85f9))
* 성경 소스 응답 없을 때 자세한 오류 안내 ([#82](https://github.com/sunghwan2789/Bible2PPT/issues/82)) ([9d762a1](https://github.com/sunghwan2789/Bible2PPT/commit/9d762a113873050e5f4fa245389445c50f2deef1))
* 코드 분석기 추가 및 권장 사항 적용 ([#74](https://github.com/sunghwan2789/Bible2PPT/issues/74)) ([d164237](https://github.com/sunghwan2789/Bible2PPT/commit/d164237d6dda885529ff87d714edc1b6a16b21fa))


### Features

* app.config으로 DB connection string 설정 지원 ([#76](https://github.com/sunghwan2789/Bible2PPT/issues/76)) ([85a6325](https://github.com/sunghwan2789/Bible2PPT/commit/85a632587a7604f8983ef76e8601e47c2b2a1f5e)), closes [#44](https://github.com/sunghwan2789/Bible2PPT/issues/44)
* 장별 시작 절 번호 `[CPAS]`, 끝 번호 `[CPAE]` 치환자 활성화 ([#83](https://github.com/sunghwan2789/Bible2PPT/issues/83)) ([b022586](https://github.com/sunghwan2789/Bible2PPT/commit/b0225868b50f6a2361fc230d92326f53dbad81d8))


### Reverts

* Revert "refactor: Jobs 이름 변경 (#78)" (#79) ([437c09c](https://github.com/sunghwan2789/Bible2PPT/commit/437c09cbf825ec07c2a3bc5bdc1529497cc97f3e)), closes [#78](https://github.com/sunghwan2789/Bible2PPT/issues/78) [#79](https://github.com/sunghwan2789/Bible2PPT/issues/79)


### BREAKING CHANGES

* .NET Framework 4.8

## [1.4.0](https://github.com/sunghwan2789/Bible2PPT/compare/v1.3.2...v1.4.0) (2020-07-29)


### 새로운 기능

* 스플래시 스크린 추가 ([#41](https://github.com/sunghwan2789/Bible2PPT/pull/41))
* PPT 제작 기록 관리 기능 추가 ([#39](https://github.com/sunghwan2789/Bible2PPT/pull/39)) (고석찬님 제안 [#17](https://github.com/sunghwan2789/Bible2PPT/issues/17))
* 줄 수를 기준으로 슬라이드를 분할하는 기능 추가 ([#59](https://github.com/sunghwan2789/Bible2PPT/pull/59)) (이상문님, 어르신을 위한제안님 제안 [#7](https://github.com/sunghwan2789/Bible2PPT/issues/7))


### 기타 변경 사항

* 성경 데이터 내려받기 속도 제한 ([#42](https://github.com/sunghwan2789/Bible2PPT/pull/42))
* 코드 클린업 ([#46](https://github.com/sunghwan2789/Bible2PPT/pull/46))
* 빌드 대상 성경 목록 순번 세로 정렬 가운데로 변경 ([#57](https://github.com/sunghwan2789/Bible2PPT/pull/57))
* PPT 만든 날짜의 기준 시간대를 로컬 시간대로 변경 ([#60](https://github.com/sunghwan2789/Bible2PPT/pull/60))

## [1.3.2](https://github.com/sunghwan2789/Bible2PPT/compare/v1.3.1...v1.3.2) (2019-06-29)


* 여러 성경 사용 시 안정성 개선 및 캐시를 필수로 사용하도록 변경 ([#36](https://github.com/sunghwan2789/Bible2PPT/pull/36))

## [1.3.1](https://github.com/sunghwan2789/Bible2PPT/compare/v1.3...v1.3.1) (2019-06-24)


* 비어있는 페이지는 넘기도록 알고리즘 수정 ([#27](https://github.com/sunghwan2789/Bible2PPT/pull/27))
* 성경 소스가 응답이 없으면 재시도 여부를 묻기 ([#28](https://github.com/sunghwan2789/Bible2PPT/pull/28))

## [1.3.0](https://github.com/sunghwan2789/Bible2PPT/compare/v1.2...v1.3) (2019-06-23) [YANKED]


* 레이아웃 재배치 및 이름 정리 ([#23](https://github.com/sunghwan2789/Bible2PPT/pull/23))
* 여러 성경으로 PPT 만들기 기능 추가 ([#26](https://github.com/sunghwan2789/Bible2PPT/pull/26)) (전주현님, 유승원님 제안 [#15](https://github.com/sunghwan2789/Bible2PPT/issues/15))
  * `[BODY1]` ~ `[BODY9]` 템플릿 치환자 추가
* GOODTV 성경 소스 추가 ([#19](https://github.com/sunghwan2789/Bible2PPT/pull/19)) (안창혁님 제안 [#3](https://github.com/sunghwan2789/Bible2PPT/issues/3))
* 갓피아 성경에서 신명기 30장 9-10절 처리 못하는 문제 해결 ([#18](https://github.com/sunghwan2789/Bible2PPT/pull/18), [#20](https://github.com/sunghwan2789/Bible2PPT/pull/20)) (작은빛님 제보 [#16](https://github.com/sunghwan2789/Bible2PPT/issues/16))
* 엔티티 이름 변경 및 async/await 사용 ([#21](https://github.com/sunghwan2789/Bible2PPT/pull/21))
  * http://go.microsoft.com/fwlink/?linkid=221217 추가 설치 필요

## [1.2.0](https://github.com/sunghwan2789/Bible2PPT/compare/1.1.2...v1.2) (2018-02-28)


* 오프라인 캐시 기능 추가 ([#6](https://github.com/sunghwan2789/Bible2PPT/pull/6), [#9](https://github.com/sunghwan2789/Bible2PPT/pull/9)) (김충만님, 궁금님 제안 [#4](https://github.com/sunghwan2789/Bible2PPT/issues/4))
* 성경 구절 소스를 선택하는 기능 추가 및 갓피아 성경 소스 추가 ([#5](https://github.com/sunghwan2789/Bible2PPT/pull/5))
* 테마와 슬라이드 마스터를 지원하도록 템플릿 파일 수정 ([26b3954](https://github.com/sunghwan2789/Bible2PPT/commit/26b3954abe4f6a6d5cd466794482307c7c064b98))
* PPT 만들기 전에 모든 성경 구절 검색어를 확인하도록 변경 ([#8](https://github.com/sunghwan2789/Bible2PPT/pull/8))

## [1.1.2](https://github.com/sunghwan2789/Bible2PPT/compare/1.1.1...1.1.2) (2018-02-23)


* PPT 작성 중 발생하는 오류를 상세하게 알리도록 변경
* GitHub 버튼 추가, 리소스 정리 ([791967d](https://github.com/sunghwan2789/Bible2PPT/commit/791967dee26b877499eca094bd915e86188dac51))
* 대상 프레임워크를 .NET Framework 4 Client Profile로 하향 ([353ebd3](https://github.com/sunghwan2789/Bible2PPT/commit/353ebd38d584881f265e859b95f7cb54881f34a1))

## [1.1.1](https://github.com/sunghwan2789/Bible2PPT/compare/1.1...1.1.1) (2017-01-27)


* 대상 프레임워크를 .NET Framework 4로 내림 ([7ea86e5](https://github.com/sunghwan2789/Bible2PPT/commit/7ea86e57345b745b1bd61d66d7f7ec52d8f1d669))
  * 하위 버전 닷넷 프레임워크에서 파워포인트 초기화를 할 수 없는 문제 해결

## [1.1.0](https://github.com/sunghwan2789/Bible2PPT/compare/1.0.3.2...1.1) (2017-01-26)


* 파일을 장별로 나누는 기능 추가 ([#1](https://github.com/sunghwan2789/Bible2PPT/pull/1)) ([@flatninth](https://github.com/flatninth))

## [1.0.3.2](https://github.com/sunghwan2789/Bible2PPT/compare/1.0.3.1...1.0.3.2) (2015-04-15)


* 소제목 제거를 제대로 못하는 것 수정 ([bb8156b](https://github.com/sunghwan2789/Bible2PPT/commit/bb8156b6fb7e396b119c706249a448e81abd05ea)) (nlife님 제보 [#](https://bloodcat.tistory.com/278#comment11119682))

## 1.0.3.1 (2014-10-05)


* 제목 검색 상자에서 방향키 누를 때 발생하는 오류 처리
* 진행상황 간락하게 변경
* 절 번호를 입력하지 않아도 되게 변경 및 이에 대한 설명 추가

## 1.0.3.0 (2014-08-10)


* 제목 검색기능 추가
* 템플릿 파일 이름 변경

## 1.0.2.0 (2014-05-20)


* 프로그램 종료할 때 모든 파워포인트 창이 같이 닫히는 문제 해결

## 1.0.1.0 (2014-05-18)


* 템플릿: 치환자 변경, 추가
* PPT 만드는 중에 파워포인트 보이지 않게 변경
* 진행상황 보는 기능 추가
* PPT 만드는 중에 취소하는 기능 추가
* 프로그램 아이콘 추가
* 설정 기억기능 추가

## 1.0.0.0 (2014-05-11)


* 배포
