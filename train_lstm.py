import json
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
import onnx
import os

# 파일 경로 설정 (절대 경로 사용 권장)
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
HISTORY_FILE = os.path.join(BASE_DIR, 'history.json')
ONNX_FILE = os.path.join(BASE_DIR, 'loto7_lstm.onnx')

def load_data(file_path):
    if not os.path.exists(file_path):
        print(f"Error: {file_path} not found.")
        return []
    with open(file_path, 'r') as f:
        history = json.load(f)
    return history

def calculate_ac_value(numbers):
    diffs = set()
    n = len(numbers)
    for i in range(n):
        for j in range(i + 1, n):
            diffs.add(abs(numbers[i] - numbers[j]))
    return len(diffs) - (n - 1)

def extract_features(draws):
    # 37-bit multi-hot
    multi_hot = np.zeros(37, dtype=np.float32)
    for n in draws:
        if 1 <= n <= 37:
            multi_hot[n-1] = 1.0
    
    # 추가 특성: 합계, AC값, 홀짝 비율
    total_sum = sum(draws)
    ac_value = calculate_ac_value(sorted(draws))
    odd_count = len([n for n in draws if n % 2 != 0])
    odd_even_ratio = odd_count / 7.0
    
    # 정규화
    norm_sum = (total_sum - 28) / (238 - 28)
    norm_ac = ac_value / 15.0
    
    features = np.concatenate([multi_hot, [norm_sum, norm_ac, odd_even_ratio]])
    return features

def prepare_dataset(history, window_size=10):
    X = []
    y = []
    # 데이터가 부족한 경우 처리
    if len(history) <= window_size:
        return np.array([]), np.array([])
        
    processed_data = [extract_features(draw) for draw in history]
    for i in range(len(processed_data) - window_size):
        X.append(processed_data[i:i+window_size])
        y.append(processed_data[i+window_size][:37]) # 다음 추첨의 multi-hot (37)
    return np.array(X), np.array(y)

class LSTMPredictor(nn.Module):
    def __init__(self, input_dim, hidden_dim, output_dim):
        super(LSTMPredictor, self).__init__()
        # PyTorch LSTM: (Batch, Seq, Dim) with batch_first=True
        self.lstm = nn.LSTM(input_dim, hidden_dim, num_layers=2, batch_first=True, dropout=0.2)
        self.fc = nn.Linear(hidden_dim, output_dim)
        self.sigmoid = nn.Sigmoid()

    def forward(self, x):
        # x shape: (Batch, Seq, Dim)
        lstm_out, _ = self.lstm(x)
        # 마지막 타임스텝의 출력 사용
        last_out = lstm_out[:, -1, :]
        out = self.fc(last_out)
        return self.sigmoid(out)

def main():
    history = load_data(HISTORY_FILE)
    if not history:
        print("No history data found.")
        return

    window_size = 10
    X_np, y_np = prepare_dataset(history, window_size)
    
    if X_np.size == 0:
        print("Not enough data to train.")
        return

    X = torch.tensor(X_np, dtype=torch.float32)
    y = torch.tensor(y_np, dtype=torch.float32)

    input_dim = X.shape[2] # 40
    hidden_dim = 128
    output_dim = 37
    
    model = LSTMPredictor(input_dim, hidden_dim, output_dim)
    
    # Apple Silicon (MPS) 사용 가능 시 활용, 아니면 CPU
    device = torch.device("mps" if torch.backends.mps.is_available() else "cpu")
    print(f"Using device: {device}")
    model.to(device)
    X, y = X.to(device), y.to(device)

    criterion = nn.BCELoss()
    optimizer = optim.Adam(model.parameters(), lr=0.001)

    print(f"Training LSTM for {len(X)} samples...")
    epochs = 200 # 조금 더 충분히 학습
    for epoch in range(epochs):
        model.train()
        optimizer.zero_grad()
        outputs = model(X)
        loss = criterion(outputs, y)
        loss.backward()
        optimizer.step()
        
        if (epoch + 1) % 50 == 0:
            print(f"Epoch [{epoch+1}/{epochs}], Loss: {loss.item():.4f}")

    # 가중치 저장 (백업용)
    torch.save(model.state_dict(), os.path.join(BASE_DIR, 'loto7_lstm.pth'))

    # ONNX로 내보내기
    model.eval()
    model.to("cpu") # Export는 CPU에서 수행하는 것이 안정적
    dummy_input = torch.randn(1, window_size, input_dim)
    
    print(f"Exporting model to {ONNX_FILE}...")
    
    # PyTorch 2.x에서는 legacy exporter를 사용하기 위해 dynamo=False 명시 (또는 기본값 사용)
    # 900KB 정도로 작으므로 가중치를 포함하도록 설정
    try:
        # 1단계: 임시 export (가끔 외부 데이터로 나뉠 수 있음)
        tmp_onnx = ONNX_FILE + ".tmp"
        torch.onnx.export(
            model,
            dummy_input,
            tmp_onnx,
            export_params=True,
            opset_version=14, # C# ML.NET 호환성을 위해 14 선호
            do_constant_folding=True,
            input_names=['input'],
            output_names=['output']
            # dynamic_axes 제거 (C#에서는 고정 크기가 더 안정적일 수 있음)
        )
        
        # 2단계: onnx 라이브러리를 사용하여 가중치를 내부에 강제 포함 (Embed)
        # 외부 데이터(.data)가 생기는 것을 방지
        onnx_model = onnx.load(tmp_onnx)
        # onnx.save 시 save_as_external_data=False (기본값)로 저장하면 내부에 포함됨
        onnx.save(onnx_model, ONNX_FILE)
        
        if os.path.exists(tmp_onnx):
            os.remove(tmp_onnx)
        # .data 파일이 생성되었다면 삭제 (이미 embedded에 포함됨)
        if os.path.exists(tmp_onnx + ".data"):
            os.remove(tmp_onnx + ".data")
        if os.path.exists(ONNX_FILE + ".data"):
            os.remove(ONNX_FILE + ".data")
            
        print(f"Successfully saved embedded ONNX model to {ONNX_FILE}")
        print(f"Model size: {os.path.getsize(ONNX_FILE) / 1024:.2f} KB")
        
    except Exception as e:
        print(f"Export failed: {e}")

if __name__ == "__main__":
    main()
