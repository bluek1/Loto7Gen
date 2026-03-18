"""
OnnxRuntime 1.23.2 (max IR version 11) 호환 LSTM 모델 생성 스크립트.
history.json 데이터로 학습한 뒤 opset 11로 내보냅니다.
"""
import json
import os
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim

HISTORY_FILE = "history.json"
OUTPUT_ONNX  = "loto7_lstm.onnx"
WINDOW_SIZE  = 10
INPUT_DIM    = 40
HIDDEN_DIM   = 64
OUTPUT_DIM   = 37
EPOCHS       = 500
OPSET        = 11   # IR version 6 → OnnxRuntime 1.23.2 (max 11) OK
VAL_RATIO    = 0.2  # 검증 데이터 비율
PATIENCE     = 30   # Early stopping patience
LR_PATIENCE  = 15   # Learning rate scheduler patience
LR_FACTOR    = 0.5  # Learning rate 감소 비율

# -------- 피처 추출 --------
def calc_ac(nums):
    s = sorted(nums)
    diffs = set()
    for i in range(len(s)):
        for j in range(i + 1, len(s)):
            diffs.add(s[j] - s[i])
    return len(diffs) - (len(s) - 1)

def extract(draw):
    f = np.zeros(40, dtype=np.float32)
    for n in draw:
        f[n - 1] = 1.0
    f[37] = (sum(draw) - 28) / (238.0 - 28.0)
    f[38] = calc_ac(draw) / 15.0
    f[39] = sum(1 for n in draw if n % 2 != 0) / 7.0
    return f

# -------- 데이터 준비 --------
with open(HISTORY_FILE, encoding="utf-8") as fp:
    history = json.load(fp)

feats = [extract(d) for d in history]
X_list, y_list = [], []
for i in range(len(feats) - WINDOW_SIZE):
    X_list.append(feats[i : i + WINDOW_SIZE])
    y_list.append(feats[i + WINDOW_SIZE][:37])

X = torch.tensor(np.array(X_list), dtype=torch.float32)
y = torch.tensor(np.array(y_list), dtype=torch.float32)

# -------- Train / Validation 분리 --------
split = int(len(X_list) * (1 - VAL_RATIO))
X_train, X_val = X[:split], X[split:]
y_train, y_val = y[:split], y[split:]
print(f"Dataset: train={X_train.shape}, val={X_val.shape}")

# -------- 모델 정의 --------
class LSTMPredictor(nn.Module):
    def __init__(self):
        super().__init__()
        self.lstm = nn.LSTM(INPUT_DIM, HIDDEN_DIM, num_layers=2,
                            batch_first=True, dropout=0.2)
        self.fc   = nn.Linear(HIDDEN_DIM, OUTPUT_DIM)

    def forward(self, x):
        out, _ = self.lstm(x)
        return torch.sigmoid(self.fc(out[:, -1, :]))

model = LSTMPredictor()
optimizer = optim.Adam(model.parameters(), lr=0.001)
criterion = nn.BCELoss()
scheduler = optim.lr_scheduler.ReduceLROnPlateau(
    optimizer, patience=LR_PATIENCE, factor=LR_FACTOR, verbose=False
)

# -------- 학습 (Early Stopping + LR Scheduler) --------
print("Training...")
best_val_loss = float('inf')
patience_counter = 0
best_state = None

for epoch in range(1, EPOCHS + 1):
    model.train()
    optimizer.zero_grad()
    loss = criterion(model(X_train), y_train)
    loss.backward()
    optimizer.step()

    model.eval()
    with torch.no_grad():
        val_loss = criterion(model(X_val), y_val).item()
    scheduler.step(val_loss)

    if val_loss < best_val_loss:
        best_val_loss = val_loss
        patience_counter = 0
        best_state = {k: v.clone() for k, v in model.state_dict().items()}
    else:
        patience_counter += 1
        if patience_counter >= PATIENCE:
            print(f"  Early stopping at epoch {epoch} (best val_loss={best_val_loss:.4f})")
            break

    if epoch % 50 == 0:
        lr = optimizer.param_groups[0]['lr']
        print(f"  Epoch {epoch}/{EPOCHS}  train={loss.item():.4f}  val={val_loss:.4f}  lr={lr:.6f}")

# 최적 가중치 복원
if best_state is not None:
    model.load_state_dict(best_state)
    print(f"Best model restored (val_loss={best_val_loss:.4f})")

# -------- ONNX 내보내기 (opset 11, legacy exporter) --------
model.eval()
dummy = torch.randn(1, WINDOW_SIZE, INPUT_DIM)

# dynamo=False → 레거시 exporter → opset 11 → IR version 6
with torch.no_grad():
    torch.onnx.export(
        model, dummy, OUTPUT_ONNX,
        dynamo=False,
        export_params=True,
        opset_version=OPSET,
        do_constant_folding=True,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch_size"}, "output": {0: "batch_size"}},
    )

size = os.path.getsize(OUTPUT_ONNX)
print(f"Saved: {OUTPUT_ONNX}  ({size:,} bytes)")

# -------- IR 버전 확인 --------
import onnx
m = onnx.load(OUTPUT_ONNX)
print(f"ONNX IR version: {m.ir_version}, opset: {m.opset_import[0].version}")
