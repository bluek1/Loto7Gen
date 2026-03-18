# Loto7Gen V7.0 개선 작업 문서

## 작업 일시
2026년 3월 18일

## 개요
Loto7Gen V6.0 → V7.0 업그레이드. 4가지 주요 알고리즘/구조 개선을 적용하여 예측 품질과 학습 안정성을 향상시켰습니다.

---

## 개선 1: Ensemble (앙상블) 전략 추가

### 변경 파일
- `Strategies/EnsemblePredictionStrategy.cs` (신규)
- `StrategyFactory.cs` (수정)
- `Config.cs` (수정)
- `config.json` (수정)

### 알고리즘
기존 4개 분석 전략(Scoring, WMA, Markov, LSTM)의 예측 결과를 **가중 투표(Weighted Voting)** 방식으로 결합합니다.

```
ensemble_score[번호] = Σ (weight_s × indicator(번호 ∈ strategy_s.Predict()))
```

- 각 서브 전략이 선택한 7개 번호에 해당 전략의 가중치만큼 점수를 부여
- LSTM에 더 높은 기본 가중치(1.5) 부여 (딥러닝 모델의 패턴 캡처 능력 반영)
- 다수의 전략이 동의하는 번호일수록 높은 앙상블 점수 획득

### 설정 (config.json)
```json
"Ensemble": {
    "ScoringWeight": 1.0,
    "WmaWeight": 1.0,
    "MarkovWeight": 1.0,
    "LstmWeight": 1.5,
    "Temperature": 0.5
}
```

### 사용법
```bash
dotnet run generate ensemble    # Ensemble 전략만 실행
dotnet run backtest ensemble    # Ensemble 백테스트
dotnet run generate all         # 전체 (Ensemble 포함)
```

---

## 개선 2: Combination (조합) 필터 구현

### 변경 파일
- `Strategies/CombinationFilter.cs` (신규)
- `Config.cs` (수정)
- `config.json` (수정)

### 구현 필터 (design.md 기반)

| 필터 | 조건 | 근거 |
|------|------|------|
| **총합(Sum)** | 7개 번호 합 100~160 | 기대값 133 주변 ±30 범위 |
| **고저(High/Low)** | 1~18(저):19~37(고) = 3:4 또는 4:3 | 역대 당첨 패턴 |
| **홀짝(Odd/Even)** | 홀:짝 = 3:4 또는 4:3 | 역대 당첨 패턴 |
| **연속수(Consecutive)** | 연속 번호 쌍 1~2개 | 적정 연속 패턴 |

### 필터 API
```csharp
CombinationFilter.PassesAll(nums, config.Filter)  // 전체 필터 통과 여부
CombinationFilter.CheckSumRange(nums, 100, 160)   // 개별 필터
CombinationFilter.CheckHighLowRatio(nums)
CombinationFilter.CheckOddEvenRatio(nums)
CombinationFilter.CheckConsecutive(nums, 1, 2)
```

### 설정 (config.json)
```json
"Filter": {
    "SumMin": 100,
    "SumMax": 160,
    "ConsecutiveMin": 1,
    "ConsecutiveMax": 2
}
```

---

## 개선 3: 확률적 샘플링 (Probabilistic Sampling)

### 변경 파일
- `Strategies/EnsemblePredictionStrategy.cs` (Ensemble 전략 내 통합)

### 알고리즘

기존 전략들의 결정론적 top-7 선택 대신, **Softmax 온도 기반 가중 샘플링**을 도입했습니다.

#### 동작 흐름
1. 앙상블 점수 → Softmax 확률 변환 (온도 파라미터 적용)
2. 확률 분포에서 비복원 가중 샘플링으로 7개 번호 추출
3. 조합 필터 통과 여부 확인
4. 실패 시 재시도 (최대 200회)
5. 200회 내 통과 못하면 가장 많은 필터를 통과한 후보 반환

#### Softmax 온도 효과
```
P(번호_i) = exp(score_i / T) / Σ exp(score_j / T)
```

| Temperature | 효과 |
|------------|------|
| T → 0 | 결정론적 (최고 점수만 선택) |
| T = 0.5 (기본) | 균형잡힌 탐색/활용 |
| T → ∞ | 균등 분포 (완전 랜덤) |

#### 비복원 가중 샘플링
```
1. 전체 확률 합 계산
2. [0, total) 균등 난수 생성
3. 누적 확률에서 해당 번호 선택
4. 선택된 번호의 확률을 0으로 설정
5. 7개 완성될 때까지 반복
```

---

## 개선 4: LSTM 학습 프로세스 개선

### 변경 파일
- `create_model.py` (수정)

### 개선 내용

#### 4-1. Train/Validation 분리
```python
VAL_RATIO = 0.2  # 80% 학습, 20% 검증
split = int(len(X_list) * (1 - VAL_RATIO))
X_train, X_val = X[:split], X[split:]
y_train, y_val = y[:split], y[split:]
```
- 시계열 데이터 특성에 맞게 앞부분 80%를 학습, 뒷부분 20%를 검증으로 분리
- 데이터 셔플링 없음 (시간 순서 보존)

#### 4-2. Learning Rate Scheduler
```python
scheduler = optim.lr_scheduler.ReduceLROnPlateau(
    optimizer, patience=15, factor=0.5
)
```
- 검증 손실이 15 에포크 동안 개선되지 않으면 학습률을 50% 감소
- 학습 후반부의 미세 조정 효과

#### 4-3. Early Stopping
```python
PATIENCE = 30  # 30 에포크 동안 개선 없으면 중단
```
- 과적합 방지: 검증 손실 기준 30 에포크 미개선 시 학습 중단
- 최대 에포크 200 → 500으로 증가 (early stopping이 적절히 중단)
- 최적 가중치(best_state)를 메모리에 보관하고 학습 종료 후 복원

#### 4-4. 학습 로그 개선
```
Epoch 50/500  train=0.1234  val=0.1456  lr=0.001000
Epoch 100/500  train=0.0987  val=0.1123  lr=0.000500
Early stopping at epoch 180 (best val_loss=0.1050)
Best model restored (val_loss=0.1050)
```
- train loss, val loss, 현재 lr 동시 출력
- early stopping 발동 시점과 최적 val_loss 출력

---

## 프로젝트 구조 (V7.0)

```
Loto7Gen/
├── Program.cs              # 메인 엔트리 (V7.0)
├── Config.cs               # 설정 (Ensemble, Filter 추가)
├── config.json             # 런타임 설정 파일
├── StrategyFactory.cs      # 전략 팩토리 (Ensemble 포함)
├── Strategies/
│   ├── IPredictionStrategy.cs          # 전략 인터페이스
│   ├── ScoringPredictionStrategy.cs    # 빈도 기반 스코어링
│   ├── WmaPredictionStrategy.cs        # 가중 이동 평균
│   ├── MarkovPredictionStrategy.cs     # 마르코프 체인
│   ├── LstmPredictionStrategy.cs       # ONNX LSTM
│   ├── EnsemblePredictionStrategy.cs   # ★ 앙상블 (신규)
│   ├── CombinationFilter.cs            # ★ 조합 필터 (신규)
│   ├── RandomPredictionStrategy.cs     # 랜덤
│   └── PredictionFeatureHelper.cs      # 피처 추출
├── create_model.py         # LSTM 학습 (★ 개선됨)
└── doc/
    └── improvement_v7.md   # 이 문서
```

---

## 백테스트 결과 비교

### Ensemble 단독 백테스트 (568회차)
| 지표 | 값 |
|------|-----|
| 총 투자 비용 | 170,400엔 |
| 총 당첨 금액 | 91,900엔 |
| 수익률 | 53.93% |
| 평균 일치 | 1.46개/게임 |
| 5개 일치 | 1번 |
| 4개 일치 | 12번 |
| 3개 일치 | 66번 |

---

## 설정 가이드

### Ensemble 가중치 조정
- 특정 전략의 성능이 좋으면 해당 가중치를 높임
- 예: LSTM 모델을 재학습한 후 성능이 향상되면 `LstmWeight`를 2.0으로 상향

### Temperature 조정
- **낮춤 (0.1~0.3)**: 앙상블 합의가 강한 번호에 집중 (보수적)
- **기본 (0.5)**: 균형잡힌 탐색과 활용
- **높임 (0.7~1.0)**: 다양한 조합 탐색 (공격적)
- **0으로 설정**: 필터 비활성, 결정론적 top-7 반환

### 필터 범위 조정
- 총합 범위를 넓히면 (예: 90~170) 더 다양한 조합 생성
- 연속수 범위를 0~3으로 변경하면 연속수 없는 조합도 허용
