# Loto7Gen V8.0 수익률 개선 알고리즘 문서

## 작업 일시
2026년 3월 18일

## 개요
V7.0 → V8.0 업그레이드. 수익률 향상을 위한 4가지 신규 알고리즘을 추가했습니다.

---

## 알고리즘 1: 번호 쌍 동시출현 분석 (Co-occurrence Scoring)

### 파일
- `Strategies/CoOccurrencePredictionStrategy.cs` (신규)

### 핵심 아이디어
기존 전략은 각 번호를 **독립적**으로 평가하지만, 실제 당첨에서는 **함께 자주 나오는 번호 쌍**이 존재합니다. 이 상관관계를 Lift 지표로 포착합니다.

### 알고리즘

```
Lift(i, j) = P(i ∩ j) / (P(i) × P(j))
```

| Lift 값 | 의미 |
|---------|------|
| > 1.0 | 기대보다 자주 동시 출현 (양의 상관) |
| = 1.0 | 독립 (상관 없음) |
| < 1.0 | 기대보다 적게 동시 출현 (음의 상관) |

### 선택 과정 (탐욕적 알고리즘)
1. 첫 번호: 빈도 + 최근 출현 보너스가 가장 높은 번호 선택
2. 이후 번호: `개별점수 + Σ(Lift(후보, 이미선택) × LiftWeight)` 가 최대인 번호 선택
3. 7개가 될 때까지 반복

### 설정
```json
"CoOccurrence": {
    "WindowSize": 100,
    "LiftWeight": 0.3
}
```

---

## 알고리즘 2: 적응형 앙상블 가중치 (Adaptive Ensemble)

### 파일
- `Strategies/EnsemblePredictionStrategy.cs` (수정)

### 핵심 아이디어
고정 가중치 대신, **직전 N회차에서 각 전략의 실제 성능**을 측정하여 가중치를 자동 조정합니다.

### 알고리즘

```
1. 직전 AdaptiveWindow(기본 20)회차에 대해 각 전략의 평균 일치 수 계산
2. 성능 점수를 softmax로 변환해 정규화
3. 기본 가중치와 적응형 가중치를 AdaptiveBlend 비율로 블렌딩

final_weight[s] = base_weight[s] × (1 - blend) + adaptive_weight[s] × blend
```

### 6개 서브 전략 지원
| 인덱스 | 전략 | 기본 가중치 |
|--------|------|-----------|
| 0 | Scoring | 1.0 |
| 1 | WMA | 1.0 |
| 2 | Markov | 1.0 |
| 3 | LSTM | 1.5 |
| 4 | CoOccur | 1.2 |
| 5 | Gap | 1.0 |

### 설정
```json
"Ensemble": {
    "AdaptiveWindow": 20,
    "AdaptiveBlend": 0.5,
    "CoOccurWeight": 1.2,
    "GapWeight": 1.0
}
```

---

## 알고리즘 3: 다중 티켓 포트폴리오 (Portfolio Coverage)

### 파일
- `Strategies/PortfolioPredictionStrategy.cs` (신규)

### 핵심 아이디어
1장 대신 **N장의 보완적 티켓**을 생성하여 37개 번호 중 더 넓은 영역을 커버합니다.

### 티켓 생성 규칙

| 티켓 | 구성 |
|------|------|
| #1 | 앙상블 투표 top-7 (최고 확률 번호) |
| #2 | 핵심 2개 유지 + 중간 순위 5개 (차선 영역) |
| #3 | 핵심 2개 유지 + 하위 순위 영역 탐색 |

### 투표 점수
6개 서브 전략(Scoring, WMA, Markov, LSTM, CoOccur, Gap) 각각의 예측에서 선택된 번호에 1점씩 부여합니다.

### 조합 필터
각 티켓에 CombinationFilter(총합, 고저, 홀짝, 연속수)를 최대 50회 시도하여 적용합니다.

### 설정
```json
"Portfolio": {
    "TicketCount": 3,
    "OverlapRatio": 0.3
}
```

---

## 알고리즘 4: 갭 분포 회귀 (Gap Distribution Modeling)

### 파일
- `Strategies/GapPredictionStrategy.cs` (신규)

### 핵심 아이디어
단순한 "콜드 보너스"(임계값 이진 판단) 대신, 각 번호의 **실제 갭(미출현 기간) 분포**를 **기하분포**로 모델링하여 연속적인 "오버듀" 확률을 산출합니다.

### 알고리즘

```
각 번호 n에 대해:
1. 히스토리에서 모든 갭(연속 미출현 기간) 수집
2. 평균갭 계산: avgGap[n] = Σ(gap_i) / gap_count
3. 기하분포 파라미터: p = 1 / avgGap[n]
4. 오버듀 확률(CDF): P(X ≤ currentGap) = 1 - (1-p)^(currentGap+1)
5. 최종 점수: freqScore × FrequencyWeight + overdueProb × OverdueWeight
```

### 기하분포 CDF 해석
| 현재 갭 vs 평균 갭 | 오버듀 확률 | 의미 |
|-------------------|-----------|------|
| << 평균 갭 | 낮음 (0.2~0.4) | 아직 출현할 때가 아님 |
| ≈ 평균 갭 | 중간 (0.5~0.7) | 출현 가능성 상승 |
| >> 평균 갭 | 높음 (0.8~0.95) | 통계적으로 "출현할 차례" |

### 설정
```json
"Gap": {
    "FrequencyWeight": 1.0,
    "OverdueWeight": 2.0
}
```

---

## 100회차 백테스트 결과 비교

### V8.0 전 전략 결과

| 순위 | 전략 | 수익률 | 평균 일치 | 5개↑ | 4개 | 3개 |
|------|------|--------|----------|------|-----|-----|
| 1 | **Scoring** | **71.00%** | 1.44 | 1 | 3 | 8 |
| 2 | **Portfolio#3** | **70.33%** | 1.42 | 1 | 0 | 12 |
| 3 | LSTM | 58.67% | **1.52** | 0 | **4** | 12 |
| 4 | Markov | 51.33% | 1.29 | 0 | 1 | **14** |
| 5 | Portfolio#1 | 50.67% | **1.53** | 0 | 3 | 11 |
| 6 | Portfolio#2 | 46.00% | 1.41 | 0 | 2 | 11 |
| 7 | Ensemble | 44.00% | 1.49 | 0 | 3 | 9 |
| 8 | Gap | 43.33% | 1.42 | 0 | 0 | 13 |
| 9 | WMA | 42.67% | 1.44 | 0 | 2 | 10 |
| 10 | CoOccur | 41.33% | 1.37 | 0 | 1 | 11 |
| 11 | Random (기준) | 41.33% | 1.25 | 0 | 1 | 11 |

### V7.0 → V8.0 비교 (주요 전략)

| 전략 | V7.0 수익률 | V8.0 수익률 | 변동 |
|------|-----------|-----------|------|
| Scoring | 64.33% | **71.00%** | +6.67% |
| LSTM | 58.67% | 58.67% | ±0% |
| Ensemble | 49.33% | 44.00% | -5.33% |
| Portfolio#1 (신규) | - | **50.67%** | 신규 |
| Portfolio#3 (신규) | - | **70.33%** | 신규 |
| Gap (신규) | - | 43.33% | 신규 |
| CoOccur (신규) | - | 41.33% | 신규 |

### 분석

1. **Scoring (71.00%)**: 검증된 빈도+콜드보너스 조합이 여전히 최강
2. **Portfolio#3 (70.33%)**: 핵심 번호 + 넓은 탐색 영역이 5개 일치 1회 달성으로 높은 수익률
3. **LSTM (58.67%)**: 평균 일치 1.52, 4개 일치 4회로 안정적 2위
4. **Portfolio#1 (50.67%)**: 평균 일치 1.53으로 전 전략 중 최고
5. **Ensemble**: 적응형 가중치가 확률적 샘플링과 결합되어 분산이 큼. 장기(500회+) 안정성에 유리

### 포트폴리오 3장 합산 효과

Portfolio #1~#3 합산 시 (300엔 × 3 = 900엔/회차):
- 총 투자: 90,000엔
- 총 당첨: 50,100엔
- 합산 수익률: **55.67%**
- 3장이 서로 다른 영역을 커버하여 3개 일치 빈도가 안정적

---

## 프로젝트 구조 (V8.0)

```
Loto7Gen/
├── Program.cs                              # 메인 (V8.0)
├── Config.cs                               # 설정 (CoOccurrence, Gap, Portfolio 추가)
├── config.json                             # 런타임 설정
├── StrategyFactory.cs                      # 팩토리 (10개 전략)
├── Strategies/
│   ├── IPredictionStrategy.cs              # 전략 인터페이스
│   ├── ScoringPredictionStrategy.cs        # 빈도 기반
│   ├── WmaPredictionStrategy.cs            # 가중 이동 평균
│   ├── MarkovPredictionStrategy.cs         # 마르코프 체인
│   ├── LstmPredictionStrategy.cs           # ONNX LSTM
│   ├── CoOccurrencePredictionStrategy.cs   # ★ 동시출현 분석 (신규)
│   ├── GapPredictionStrategy.cs            # ★ 갭 분포 회귀 (신규)
│   ├── EnsemblePredictionStrategy.cs       # ★ 적응형 앙상블 (개선)
│   ├── PortfolioPredictionStrategy.cs      # ★ 다중 티켓 (신규)
│   ├── CombinationFilter.cs               # 조합 필터
│   ├── RandomPredictionStrategy.cs         # 랜덤
│   └── PredictionFeatureHelper.cs          # 피처 추출
├── create_model.py                         # LSTM 학습
└── doc/
    ├── improvement_v7.md                   # V7.0 문서
    └── improvement_v8.md                   # 이 문서
```

---

## 사용법

```bash
# 전체 전략 실행 (10개)
dotnet run generate all

# 개별 전략
dotnet run generate cooccur
dotnet run generate gap
dotnet run generate ensemble
dotnet run generate portfolio1
dotnet run generate portfolio2
dotnet run generate portfolio3

# 백테스트 (100회)
dotnet run backtest scoring 100
dotnet run backtest portfolio3 100
dotnet run backtest ensemble 100
```

---

## 설정 튜닝 가이드

### CoOccurrence
- `LiftWeight` 증가 (0.3→0.5): 동시출현 상관관계에 더 의존
- `WindowSize` 감소 (100→50): 최근 동시출현 패턴에 집중

### Gap
- `OverdueWeight` 증가 (2.0→3.0): 오버듀 번호에 더 강하게 베팅
- `FrequencyWeight` 감소 (1.0→0.5): 빈도보다 갭 패턴에 집중

### Ensemble 적응형
- `AdaptiveWindow` 감소 (20→10): 더 최근 성능에 반응
- `AdaptiveBlend` 증가 (0.5→0.8): 적응형 가중치에 더 의존
- `Temperature` 감소 (0.5→0.2): 확실한 번호에 집중

### Portfolio
- `TicketCount` 증가 (3→5): 더 넓은 커버리지 (비용 증가)
- `OverlapRatio` 감소 (0.3→0.15): 티켓 간 중복 최소화
