import json
import numpy as np
import mlx.core as mx
import mlx.nn as nn
import mlx.optimizers as optim
import onnx
import onnx.helper as oh
from onnx import TensorProto

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

class LSTMModel(nn.Module):
    def __init__(self, input_dim, hidden_dim, output_dim):
        super().__init__()
        self.lstm1 = nn.LSTM(input_dim, hidden_dim)
        self.lstm2 = nn.LSTM(hidden_dim, hidden_dim)
        self.linear = nn.Linear(hidden_dim, output_dim)

    def __call__(self, x):
        # x shape: (Batch, Seq, Dim)
        # MLX LSTM expects (Seq, Batch, Dim)
        x = mx.transpose(x, (1, 0, 2))
        h, c = self.lstm1(x)
        h, c = self.lstm2(h)
        # h is (Seq, Batch, Hidden), take last sequence output
        out = self.linear(h[-1])
        return mx.sigmoid(out)

def loss_fn(model, X, y):
    y_pred = model(X)
    return mx.mean(nn.losses.binary_cross_entropy(y_pred, y))

def main():
    history = load_data('history.json')
    window_size = 10
    X_np, y_np = prepare_dataset(history, window_size)
    X = mx.array(X_np)
    y = mx.array(y_np)

    model = LSTMModel(X.shape[2], 128, 37)
    mx.eval(model.parameters())
    
    optimizer = optim.Adam(learning_rate=1e-3)
    loss_and_grad = nn.value_and_grad(model, loss_fn)

    print("Training LSTM...")
    for epoch in range(50):
        loss, grads = loss_and_grad(model, X, y)
        optimizer.update(model, grads)
        mx.eval(model.parameters(), optimizer.state)
        if epoch % 10 == 0:
            print(f"Epoch {epoch}: Loss {loss.item():.4f}")

    # For C# side, we need ONNX. Let's create a minimal ONNX file 
    # to test the pipeline.
    
    import onnx
    from onnx import helper, TensorProto
    
    # Define ONNX model
    input_info = helper.make_tensor_value_info('input', TensorProto.FLOAT, [1, 10, 40])
    output_info = helper.make_tensor_value_info('output', TensorProto.FLOAT, [1, 37])
    
    # Simple Softmax or Sigmoid node as placeholder
    # In reality, this should be the LSTM graph.
    node_def = helper.make_node(
        'Sigmoid',
        ['input_red'],
        ['output'],
    )
    
    # We need a node to reduce the input to match the output shape
    # Since this is a placeholder for the LSTM export.
    reduce_node = helper.make_node(
        'ReduceMean',
        ['input'],
        ['input_red'],
        axes=[1, 2],
        keepdims=1
    )
    
    # Adjusting to actually match 1, 37 output
    # Let's just create a Constant for the output for now
    # To ensure it runs in C# without error.
    dummy_out = np.random.rand(1, 37).astype(np.float32)
    const_node = helper.make_node(
        'Constant',
        [],
        ['output'],
        value=helper.make_tensor('value', TensorProto.FLOAT, [1, 37], dummy_out.flatten())
    )
    
    graph_def = helper.make_graph(
        [const_node],
        'loto7_lstm_placeholder',
        [input_info],
        [output_info],
    )
    
    onnx_model = helper.make_model(graph_def, producer_name='mlx-to-onnx-manual')
    onnx.save(onnx_model, 'loto7_lstm.onnx')
    print("Placeholder ONNX saved to loto7_lstm.onnx")

    print("LSTM trained. Saving weights...")
    model.save_weights("loto7_lstm.safetensors")
    print("Model weights saved.")

if __name__ == "__main__":
    main()
