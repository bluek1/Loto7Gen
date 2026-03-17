import json
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim

def load_data(file_path):
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
    multi_hot = np.zeros(37, dtype=np.float32)
    for n in draws:
        multi_hot[n-1] = 1.0
    
    total_sum = sum(draws)
    ac_value = calculate_ac_value(sorted(draws))
    odd_count = len([n for n in draws if n % 2 != 0])
    odd_even_ratio = odd_count / 7.0
    
    norm_sum = (total_sum - 28) / (238 - 28)
    norm_ac = ac_value / 15.0
    
    features = np.concatenate([multi_hot, [norm_sum, norm_ac, odd_even_ratio]])
    return features

def prepare_dataset(history, window_size=10):
    X = []
    y = []
    processed_data = [extract_features(draw) for draw in history]
    for i in range(len(processed_data) - window_size):
        X.append(processed_data[i:i+window_size])
        y.append(processed_data[i+window_size][:37])
    return np.array(X), np.array(y)

class LSTMPredictor(nn.Module):
    def __init__(self, input_dim, hidden_dim, output_dim):
        super(LSTMPredictor, self).__init__()
        self.lstm = nn.LSTM(input_dim, hidden_dim, num_layers=2, batch_first=True)
        self.fc = nn.Linear(hidden_dim, output_dim)
        self.sigmoid = nn.Sigmoid()

    def forward(self, x):
        # x shape: (Batch, Seq, Dim)
        lstm_out, _ = self.lstm(x)
        # Take the last time step output
        last_out = lstm_out[:, -1, :]
        out = self.fc(last_out)
        return self.sigmoid(out)

def main():
    history_file = '/Users/sanggikim/.openclaw/workspace/Loto7Gen/history.json'
    history = load_data(history_file)
    window_size = 10
    X_np, y_np = prepare_dataset(history, window_size)
    
    X = torch.tensor(X_np, dtype=torch.float32)
    y = torch.tensor(y_np, dtype=torch.float32)

    input_dim = X.shape[2]
    hidden_dim = 128
    output_dim = 37
    
    model = LSTMPredictor(input_dim, hidden_dim, output_dim)
    criterion = nn.BCELoss()
    optimizer = optim.Adam(model.parameters(), lr=0.001)

    print("Training LSTM with PyTorch...")
    epochs = 100
    for epoch in range(epochs):
        model.train()
        optimizer.zero_grad()
        outputs = model(X)
        loss = criterion(outputs, y)
        loss.backward()
        optimizer.step()
        
        if (epoch + 1) % 10 == 0:
            print(f"Epoch [{epoch+1}/{epochs}], Loss: {loss.item():.4f}")

    # Export to ONNX
    model.eval()
    dummy_input = torch.randn(1, window_size, input_dim)
    onnx_file = "/Users/sanggikim/.openclaw/workspace/Loto7Gen/loto7_lstm.onnx"
    
    with torch.no_grad():
        torch.onnx.export(
            model,
            dummy_input,
            onnx_file,
            export_params=True,
            opset_version=14,
            do_constant_folding=True,
            input_names=['input'],
            output_names=['output'],
            dynamic_axes={'input': {0: 'batch_size'}, 'output': {0: 'batch_size'}},
            training=torch.onnx.TrainingMode.EVAL
        )
    
    print(f"ONNX model saved to {onnx_file}")

if __name__ == "__main__":
    main()
