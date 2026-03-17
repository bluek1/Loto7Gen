# Loto7Gen - 로또7 번호 생성기

일본 로또7(LOTO 7) 당첨 번호 예측 프로그램입니다.
과거 추첨 데이터를 기반으로 통계적, 수학적 필터를 적용하여 확률이 높은 번호 조합을 생성합니다.

## 🚀 빠른 시작

### 1. 자동화 파이프라인 실행 (권장)

```bash
# 전체 파이프라인 실행 (데이터 수집 → 학습 → 예측)
./run_pipeline.sh

# 테스트 실행 (실제 작업 없이 검증만)
./run_pipeline.sh --dry-run

# 데이터 수집만 건너뛰기
./run_pipeline.sh --skip-fetch

# 학습만 건너뛰기
./run_pipeline.sh --skip-train
```

### 2. 수동 실행

```bash
# 번호 생성
dotnet run generate

# 당첨 확인
dotnet run verify 1 2 3 4 5 6 7
```

---

## 📦 설치 및 설정

### 필수 요구사항

- **.NET 10.0 이상** (C# 프로그램)
- **Python 3.11 이상** (데이터 수집 및 학습)
- **Bash** (자동화 스크립트)

### Python 패키지

```bash
python3 -m venv venv
source venv/bin/activate
pip install beautifulsoup4 requests numpy mlx onnx
```

> **참고:** `run_pipeline.sh`는 가상환경을 자동으로 생성/활성화합니다.

---

## 🔧 주요 기능

### 1. 데이터 수집 (`fetch_data.py`)

- ✅ 최신 로또7 추첨 결과 자동 수집
- ✅ 네트워크 에러 시 자동 재시도 (최대 3회)
- ✅ 다중 소스 지원 (백업 URL)
- ✅ 실시간 데이터 검증 (범위, 중복, 개수)
- ✅ 상세 로깅 (`fetch_data.log`)
- ✅ 자동 백업 및 복구

**수동 실행:**
```bash
python3 fetch_data.py
```

### 2. 데이터 검증

#### A. 설정 파일 검증 (`validate_config.py`)

```bash
python3 validate_config.py
```

- `config.json`의 모든 파라미터 타입 및 범위 검증
- 잘못된 설정 발견 시 상세 에러 메시지
- 기본 설정 파일 생성 기능

#### B. 히스토리 데이터 검증 (`validate_history.py`)

```bash
python3 validate_history.py
```

- `history.json`의 모든 추첨 결과 검증
- 통계 분석 (출현 빈도, 평균 합계, 홀짝 비율 등)
- 데이터 무결성 확인

### 3. LSTM 모델 학습 (`train_lstm.py`)

```bash
python3 train_lstm.py
```

- 과거 데이터를 기반으로 LSTM 신경망 학습
- ONNX 형식으로 모델 저장 (`loto7_lstm.onnx`)
- SafeTensors 형식으로 가중치 저장

### 4. 번호 생성 (C# 프로그램)

```bash
dotnet run generate
```

다음 5가지 조합 전략으로 번호를 생성합니다:

| 전략 | 설명 |
|------|------|
| **Combo_1_2_3** | 총합 + 고저 비율 + 홀짝 비율 |
| **Combo_3_4** | 홀짝 비율 + 가중치 적용 |
| **Combo_4_5_6** | 가중치 + 연속수 + 마르코프 체인 |
| **Combo_1_5_6** | 총합 + 연속수 + 마르코프 체인 |
| **Combo_2_6** | 고저 비율 + 마르코프 체인 |

**생성된 예측 번호는 `predictions.json`에 저장됩니다.**

---

## 📊 자동화 파이프라인 (`run_pipeline.sh`)

### 실행 단계

1. **환경 검증**
   - Python 가상환경 확인/생성
   - config.json 검증

2. **데이터 수집**
   - 최신 추첨 결과 수집
   - 기존 데이터 백업
   - 수집된 데이터 즉시 검증

3. **모델 학습**
   - 기존 모델 백업
   - LSTM 재학습

4. **번호 생성**
   - C# 프로젝트 빌드
   - 예측 번호 생성

5. **정리 및 보고**
   - 오래된 백업 삭제 (30일 이상)
   - 실행 결과 요약

### 매주 자동 실행 (Cron)

```bash
# Cron 편집
crontab -e

# 매주 금요일 19:30 실행
30 19 * * 5 cd /path/to/Loto7Gen && ./run_pipeline.sh >> cron.log 2>&1
```

---

## 📁 프로젝트 구조

```
Loto7Gen/
├── Program.cs                  # C# 메인 프로그램
├── Loto7Gen.csproj            # C# 프로젝트 파일
├── config.json                # 설정 파일
├── history.json               # 과거 추첨 데이터
├── predictions.json           # 생성된 예측 번호
│
├── fetch_data.py              # 데이터 수집 스크립트
├── validate_config.py         # config.json 검증
├── validate_history.py        # history.json 검증
├── train_lstm.py              # LSTM 모델 학습
│
├── run_pipeline.sh            # 자동화 파이프라인 (⭐)
│
├── loto7_lstm.onnx           # 학습된 모델 (ONNX)
├── loto7_lstm.safetensors    # 모델 가중치
│
├── design.md                  # 설계 문서
├── SECURITY_REPORT.md        # 보안 강화 보고서
└── README.md                  # 이 파일
```

---

## 🔒 보안 및 안정성

### 데이터 무결성
- ✅ 모든 입력 데이터 엄격 검증
- ✅ 숫자 범위 제한 (1~37)
- ✅ 중복 방지
- ✅ 타입 검증

### 백업 및 복구
- ✅ 모든 중요 파일 자동 백업 (타임스탬프)
- ✅ 실패 시 자동 롤백
- ✅ 오래된 백업 자동 정리 (30일)

### 로깅
- ✅ 모든 작업 상세 기록
- ✅ 파일 로그 (`fetch_data.log`)
- ✅ 콘솔 실시간 출력

자세한 내용은 [`SECURITY_REPORT.md`](./SECURITY_REPORT.md) 참조.

---

## 🧪 테스트

### 1. 설정 검증
```bash
python3 validate_config.py
```

### 2. 히스토리 검증
```bash
python3 validate_history.py
```

### 3. 데이터 수집 테스트
```bash
python3 fetch_data.py
```

### 4. 파이프라인 테스트 (DRY RUN)
```bash
./run_pipeline.sh --dry-run
```

### 5. 전체 파이프라인
```bash
./run_pipeline.sh
```

---

## 📈 통계 분석

`validate_history.py` 실행 시 다음 통계를 확인할 수 있습니다:

- 전체 추첨 횟수
- 번호별 출현 빈도
- 가장 많이/적게 나온 번호 Top 5
- 평균 합계 및 범위
- 평균 홀짝 비율
- 평균 고저(1-18 vs 19-37) 비율

---

## ⚙️ 설정 파일 (`config.json`)

```json
{
  "Scoring": {
    "FrequencyWeight": 1.24,
    "ColdBonusThreshold": 5,
    "ColdBonusValue": 11.62,
    "HotPenaltyThreshold": 1,
    "HotPenaltyValue": 3.88
  },
  "WMA": {
    "WindowSize": 79
  },
  "Markov": {
    "WindowSize": 131
  }
}
```

### 파라미터 설명

| 섹션 | 파라미터 | 설명 | 범위 |
|------|----------|------|------|
| **Scoring** | FrequencyWeight | 빈도 가중치 | 0.0 ~ 10.0 |
| | ColdBonusThreshold | 콜드 번호 임계값 | 1 ~ 50 |
| | ColdBonusValue | 콜드 보너스 값 | 0.0 ~ 100.0 |
| | HotPenaltyThreshold | 핫 번호 임계값 | 1 ~ 50 |
| | HotPenaltyValue | 핫 페널티 값 | 0.0 ~ 100.0 |
| **WMA** | WindowSize | 가중 이동 평균 윈도우 | 10 ~ 500 |
| **Markov** | WindowSize | 마르코프 체인 윈도우 | 10 ~ 500 |

---

## 🛠️ 문제 해결

### 1. Python 패키지 설치 실패

```bash
python3 -m venv venv
source venv/bin/activate
pip install --upgrade pip
pip install beautifulsoup4 requests numpy mlx onnx
```

### 2. 데이터 수집 실패

- `fetch_data.log` 확인
- 네트워크 연결 확인
- 소스 URL 접근 가능 여부 확인

### 3. 설정 파일 손상

```bash
# 기본 설정 생성
python3 validate_config.py
# 프롬프트에서 'y' 입력
```

### 4. C# 빌드 실패

```bash
# .NET SDK 버전 확인
dotnet --version

# 프로젝트 정리 후 재빌드
dotnet clean
dotnet build
```

---

## 📝 로그 파일

- **fetch_data.log** - 데이터 수집 로그
- **cron.log** - Cron 자동 실행 로그 (선택)

---

## 🔮 향후 계획

- [ ] 알림 시스템 (Slack/Discord)
- [ ] 추가 데이터 소스 통합
- [ ] 예측 성능 추적 및 분석
- [ ] 웹 대시보드 UI
- [ ] 모바일 앱 연동

---

## 📄 라이선스

개인 사용 전용

---

## 👤 개발자

- **노예1** - 설계 및 알고리즘
- **노예2** - 보안 강화 및 자동화

---

## 📞 문의

문제가 발생하거나 개선 제안이 있으시면 이슈를 등록해 주세요.

---

**🎰 행운을 빕니다!**
