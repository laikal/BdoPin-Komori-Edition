# BdoPin - Komori Edition

**English | [한국어](#한국어)**

A lightweight Windows utility for applying CPU affinity and process priority settings to Black Desert.

BdoPin is designed to make it easy to control which CPU threads `BlackDesert64.exe` can use, without changing the CPU configuration of the entire system.

---

## Features

- AMD Ryzen SMT-aware CPU affinity presets
- Intel P-core / E-core aware presets
- Automatic CPU vendor and topology detection
- Process priority control
- Manual Apply / Use All CPUs
- Automatic detection of `BlackDesert64.exe`
- Korean / English UI
- Lightweight native Win32 application
- No external runtime required

---

## What does BdoPin do?

BdoPin changes the **CPU affinity** and **process priority** of `BlackDesert64.exe` using standard Windows process APIs.

For example, on an AMD Ryzen processor, the **Physical Cores Only** preset can select one logical processor from each physical CPU core.

This does **not** disable SMT or Hyper-Threading globally.

Only the selected Black Desert process is affected.  
Other applications and Windows continue to use the CPU normally.

---

## Why use it?

Depending on the CPU, game workload, and Windows scheduler behavior, some systems may benefit from running Black Desert on selected physical cores instead of allowing the game to use every logical processor.

Possible effects may include:

- reduced scheduling interference
- more consistent frame times
- reduced stutter in some environments

Performance results vary by system.

BdoPin does **not** guarantee higher FPS or better performance.  
Compare your system before and after applying a preset.

---

## Administrator privileges

BdoPin itself can start normally without administrator privileges.

When you press **Apply Now**, Windows may require administrator privileges to modify `BlackDesert64.exe`.

If required, BdoPin will request elevation through the standard Windows UAC prompt.

Automatic background detection will not display a UAC prompt without direct user interaction.

---

## What BdoPin does NOT do

BdoPin does not:

- read or modify game memory
- inject DLLs
- hook the game process
- modify Black Desert game files
- install a kernel driver
- patch the game
- attempt to bypass anti-cheat systems

BdoPin only uses Windows process-management APIs to configure CPU affinity and process priority.

---

## Basic Usage

1. Start BdoPin.
2. Check the detected CPU information.
3. Select a preset.
4. Check the selected logical CPUs.
5. Select the desired process priority.
6. Start Black Desert.
7. Press **Apply Now**.

To remove the affinity restriction, use **Use All CPUs**.

---

## Download

Download the latest build from the **Releases** section of this repository.

---

## Komori Edition

Developed by **Eltax**.

I'm a Komori too, so I simply called it the **Komori Edition**.

BdoPin is an independent fan-made utility.

It is not affiliated with or endorsed by  
**Pearl Abyss, NAVER CHZZK, or Hikimori Neko.**

---

# 한국어

**[English](#bdopin---komori-edition) | 한국어**

BdoPin은 검은사막의 CPU Affinity(프로세서 선호도)와 프로세스 우선순위를 간편하게 설정하기 위한 가벼운 Windows 유틸리티입니다.

시스템 전체의 CPU 설정을 변경하는 것이 아니라, `BlackDesert64.exe`가 사용할 CPU 논리 프로세서를 선택해서 제한하는 방식입니다.

---

## 주요 기능

- AMD Ryzen SMT 구조를 고려한 CPU Affinity 프리셋
- Intel P-Core / E-Core 구조를 고려한 프리셋
- CPU 제조사 및 토폴로지 자동 감지
- 프로세스 우선순위 설정
- 수동 적용 / 전체 CPU 사용 복원
- `BlackDesert64.exe` 자동 감지
- 한국어 / 영어 UI
- 가벼운 네이티브 Win32 프로그램
- 별도의 외부 런타임 불필요

---

## BdoPin은 무엇을 하나요?

BdoPin은 Windows의 표준 프로세스 API를 사용하여 `BlackDesert64.exe`의 **CPU Affinity**와 **프로세스 우선순위**를 변경합니다.

예를 들어 AMD Ryzen 시스템에서 **Physical Cores Only** 프리셋을 선택하면 각 물리 코어에서 하나의 논리 프로세서만 선택하여 게임이 사용하도록 설정할 수 있습니다.

이 기능은 시스템 전체의 SMT 또는 Hyper-Threading을 끄는 것이 아닙니다.

설정이 적용되는 대상은 검은사막 프로세스뿐이며, Windows와 다른 프로그램들은 기존과 같이 CPU를 사용할 수 있습니다.

---

## 왜 사용하나요?

CPU 구조, 게임의 작업 부하, Windows 스케줄러 동작에 따라 일부 시스템에서는 모든 논리 프로세서를 사용하는 것보다 특정 물리 코어 위주로 게임을 실행했을 때 더 안정적인 결과를 보일 수 있습니다.

환경에 따라 다음과 같은 변화가 있을 수 있습니다.

- CPU 스케줄링 간섭 감소
- 프레임 타임 안정화
- 일부 환경에서의 순간적인 끊김 감소

단, 시스템마다 결과는 다릅니다.

BdoPin은 **FPS 상승이나 성능 향상을 보장하지 않습니다.**  
적용 전후를 직접 비교하여 자신의 시스템에 맞는 설정을 사용하는 것을 권장합니다.

---

## 관리자 권한

BdoPin 자체는 일반 사용자 권한으로 실행할 수 있습니다.

사용자가 **지금 적용** 버튼을 눌렀을 때 Windows가 `BlackDesert64.exe` 변경 권한을 허용하지 않는 환경에서는 관리자 권한이 필요할 수 있습니다.

필요한 경우 Windows의 기본 UAC 창을 통해 관리자 권한을 요청합니다.

백그라운드 자동 감지 기능 때문에 사용자 입력 없이 갑자기 UAC 창이 나타나도록 하지 않습니다.

---

## BdoPin이 하지 않는 것

BdoPin은 다음과 같은 동작을 하지 않습니다.

- 게임 메모리 읽기 또는 수정
- DLL 인젝션
- 게임 프로세스 후킹
- 검은사막 게임 파일 수정
- 커널 드라이버 설치
- 게임 코드 패치
- 안티치트 우회 시도

BdoPin은 Windows의 프로세스 관리 API를 이용하여 CPU Affinity와 프로세스 우선순위를 설정하는 기능만 수행합니다.

---

## 기본 사용법

1. BdoPin을 실행합니다.
2. 감지된 CPU 정보를 확인합니다.
3. 원하는 프리셋을 선택합니다.
4. 선택된 Logical CPU 목록을 확인합니다.
5. 원하는 프로세스 우선순위를 선택합니다.
6. 검은사막을 실행합니다.
7. **지금 적용** 버튼을 누릅니다.

Affinity 제한을 해제하고 싶다면 **전체 CPU 사용** 기능을 사용하면 됩니다.

---

## 다운로드

이 저장소의 **Releases** 메뉴에서 최신 버전을 받을 수 있습니다.

---

## Komori Edition

개발: **Eltax**

저도 코모리라서 그냥 **Komori Edition**이라고 이름 붙였습니다.

BdoPin은 개인이 제작한 독립적인 팬메이드 유틸리티입니다.

**Pearl Abyss, NAVER CHZZK, Hikimori Neko와 공식적인 제휴 또는 보증 관계가 없습니다.**
