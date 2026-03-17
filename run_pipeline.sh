#!/bin/bash
#
# Loto7Gen 자동화 파이프라인 스크립트
# 매주 금요일 19시 추첨 직후 실행을 위한 완전 자동화 워크플로우
#
# 사용법:
#   ./run_pipeline.sh [--dry-run] [--skip-fetch] [--skip-train]
#

set -e  # 에러 발생 시 즉시 중단

# 색상 정의
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 로그 함수
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 옵션 파싱
DRY_RUN=false
SKIP_FETCH=false
SKIP_TRAIN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --skip-fetch)
            SKIP_FETCH=true
            shift
            ;;
        --skip-train)
            SKIP_TRAIN=true
            shift
            ;;
        *)
            log_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# 작업 디렉토리 확인
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$SCRIPT_DIR"

log_info "Starting Loto7Gen automated pipeline"
log_info "Working directory: $SCRIPT_DIR"
echo ""

# ============================================================================
# STEP 1: 환경 검증
# ============================================================================
log_info "Step 1: Environment Validation"
echo "-----------------------------------------------------------"

# Python 가상환경 확인
if [ ! -d "venv" ]; then
    log_warning "Python virtual environment not found, creating..."
    python3 -m venv venv
    source venv/bin/activate
    pip install --upgrade pip
    pip install beautifulsoup4 requests numpy mlx onnx
    log_success "Virtual environment created"
else
    log_info "Activating virtual environment"
    source venv/bin/activate
fi

# 필수 파일 확인
if [ ! -f "config.json" ]; then
    log_error "config.json not found!"
    exit 1
fi

# config.json 검증
log_info "Validating config.json..."
if python3 validate_config.py; then
    log_success "config.json is valid"
else
    log_error "config.json validation failed"
    exit 1
fi

echo ""

# ============================================================================
# STEP 2: 데이터 수집
# ============================================================================
if [ "$SKIP_FETCH" = false ]; then
    log_info "Step 2: Data Collection"
    echo "-----------------------------------------------------------"
    
    # 기존 history.json 백업
    if [ -f "history.json" ]; then
        BACKUP_NAME="history.json.backup.$(date +%Y%m%d_%H%M%S)"
        cp history.json "$BACKUP_NAME"
        log_info "Created backup: $BACKUP_NAME"
    fi
    
    # 데이터 수집 실행
    log_info "Fetching latest Loto7 data..."
    
    if [ "$DRY_RUN" = true ]; then
        log_warning "DRY RUN: Skipping actual data fetch"
    else
        if python3 fetch_data.py; then
            log_success "Data fetched successfully"
            
            # 수집된 데이터 검증
            log_info "Validating fetched data..."
            if python3 validate_history.py; then
                log_success "Data validation passed"
            else
                log_error "Data validation failed!"
                
                # 백업 복구
                if [ -f "$BACKUP_NAME" ]; then
                    log_warning "Restoring backup..."
                    mv "$BACKUP_NAME" history.json
                    log_info "Backup restored"
                fi
                
                exit 1
            fi
        else
            log_error "Data fetch failed!"
            exit 1
        fi
    fi
    
    echo ""
else
    log_warning "Skipping data collection (--skip-fetch)"
    echo ""
fi

# ============================================================================
# STEP 3: 모델 학습
# ============================================================================
if [ "$SKIP_TRAIN" = false ]; then
    log_info "Step 3: Model Training"
    echo "-----------------------------------------------------------"
    
    if [ ! -f "history.json" ]; then
        log_error "history.json not found! Cannot train model."
        exit 1
    fi
    
    # 모델 파일 백업
    if [ -f "loto7_lstm.onnx" ]; then
        ONNX_BACKUP="loto7_lstm.onnx.backup.$(date +%Y%m%d_%H%M%S)"
        cp loto7_lstm.onnx "$ONNX_BACKUP"
        log_info "Created ONNX model backup: $ONNX_BACKUP"
    fi
    
    if [ -f "loto7_lstm.safetensors" ]; then
        SAFETENSORS_BACKUP="loto7_lstm.safetensors.backup.$(date +%Y%m%d_%H%M%S)"
        cp loto7_lstm.safetensors "$SAFETENSORS_BACKUP"
        log_info "Created safetensors backup: $SAFETENSORS_BACKUP"
    fi
    
    log_info "Training LSTM model..."
    
    if [ "$DRY_RUN" = true ]; then
        log_warning "DRY RUN: Skipping actual training"
    else
        if python3 train_lstm.py; then
            log_success "Model training completed"
        else
            log_error "Model training failed!"
            exit 1
        fi
    fi
    
    echo ""
else
    log_warning "Skipping model training (--skip-train)"
    echo ""
fi

# ============================================================================
# STEP 4: 번호 생성 (C# 프로그램)
# ============================================================================
log_info "Step 4: Number Generation"
echo "-----------------------------------------------------------"

if [ ! -f "Loto7Gen.csproj" ]; then
    log_error "Loto7Gen.csproj not found!"
    exit 1
fi

log_info "Building C# project..."

if [ "$DRY_RUN" = true ]; then
    log_warning "DRY RUN: Skipping C# build and execution"
else
    if dotnet build --configuration Release; then
        log_success "C# project built successfully"
        
        log_info "Generating predictions..."
        if dotnet run --configuration Release generate; then
            log_success "Predictions generated"
            
            # predictions.json 확인
            if [ -f "predictions.json" ]; then
                log_info "Predictions saved to predictions.json"
                log_info "Preview:"
                head -20 predictions.json
            else
                log_warning "predictions.json not found"
            fi
        else
            log_error "Prediction generation failed!"
            exit 1
        fi
    else
        log_error "C# build failed!"
        exit 1
    fi
fi

echo ""

# ============================================================================
# STEP 5: 정리 및 보고
# ============================================================================
log_info "Step 5: Cleanup & Report"
echo "-----------------------------------------------------------"

# 오래된 백업 파일 정리 (30일 이상)
log_info "Cleaning up old backup files (older than 30 days)..."
find . -name "*.backup.*" -type f -mtime +30 -delete 2>/dev/null || true
log_success "Cleanup completed"

# 최종 상태 보고
echo ""
echo "==========================================================="
log_success "Pipeline execution completed successfully!"
echo "==========================================================="
echo ""
echo "Generated files:"
if [ -f "history.json" ]; then
    HISTORY_SIZE=$(wc -l < history.json)
    echo "  - history.json ($HISTORY_SIZE lines)"
fi
if [ -f "loto7_lstm.onnx" ]; then
    ONNX_SIZE=$(du -h loto7_lstm.onnx | cut -f1)
    echo "  - loto7_lstm.onnx ($ONNX_SIZE)"
fi
if [ -f "loto7_lstm.safetensors" ]; then
    SAFETENSORS_SIZE=$(du -h loto7_lstm.safetensors | cut -f1)
    echo "  - loto7_lstm.safetensors ($SAFETENSORS_SIZE)"
fi
if [ -f "predictions.json" ]; then
    PRED_SIZE=$(wc -l < predictions.json)
    echo "  - predictions.json ($PRED_SIZE lines)"
fi
echo ""

# 로그 파일 경로 출력
if [ -f "fetch_data.log" ]; then
    echo "Logs:"
    echo "  - fetch_data.log"
fi

echo ""
log_info "Next steps:"
echo "  1. Review predictions.json"
echo "  2. Consider running: dotnet run verify <numbers> to check your ticket"
echo ""

exit 0
