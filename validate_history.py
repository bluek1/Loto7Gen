#!/usr/bin/env python3
"""
history.json 데이터 검증 모듈
로또7 당첨 번호 데이터의 무결성을 체크합니다.
"""
import json
import logging
from typing import List, Dict, Any
from collections import Counter
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)

def validate_draw(draw: List[int], draw_index: int) -> bool:
    """
    개별 추첨 결과를 검증합니다.
    
    Args:
        draw: 7개 숫자의 리스트
        draw_index: 추첨 인덱스 (로깅용)
    
    Returns:
        검증 성공 여부
    """
    is_valid = True
    
    # 1. 정확히 7개 숫자
    if len(draw) != 7:
        logger.error(f"Draw {draw_index}: Expected 7 numbers, got {len(draw)}")
        return False
    
    # 2. 모든 요소가 정수형인지 확인
    if not all(isinstance(num, int) for num in draw):
        logger.error(f"Draw {draw_index}: Non-integer value found: {draw}")
        return False
    
    # 3. 범위 검증 (1~37)
    out_of_range = [num for num in draw if num < 1 or num > 37]
    if out_of_range:
        logger.error(f"Draw {draw_index}: Numbers out of range (1-37): {out_of_range}")
        is_valid = False
    
    # 4. 중복 검사
    if len(set(draw)) != 7:
        duplicates = [num for num, count in Counter(draw).items() if count > 1]
        logger.error(f"Draw {draw_index}: Duplicate numbers found: {duplicates}")
        is_valid = False
    
    # 5. 정렬 확인 (선택사항, 경고만 출력)
    if draw != sorted(draw):
        logger.warning(f"Draw {draw_index}: Numbers are not sorted: {draw}")
    
    return is_valid

def analyze_history(history: List[List[int]]) -> Dict[str, Any]:
    """
    히스토리 데이터를 분석하여 통계 정보를 반환합니다.
    
    Args:
        history: 전체 추첨 히스토리
    
    Returns:
        통계 정보 딕셔너리
    """
    total_draws = len(history)
    
    # 번호별 출현 횟수
    frequency = Counter()
    for draw in history:
        frequency.update(draw)
    
    # 가장 많이/적게 나온 번호
    most_common = frequency.most_common(5)
    least_common = frequency.most_common()[:-6:-1]
    
    # 평균 합계
    sums = [sum(draw) for draw in history]
    avg_sum = sum(sums) / total_draws if total_draws > 0 else 0
    
    # 홀짝 비율
    odd_counts = []
    for draw in history:
        odd_count = len([n for n in draw if n % 2 != 0])
        odd_counts.append(odd_count)
    avg_odd = sum(odd_counts) / total_draws if total_draws > 0 else 0
    
    # 고저 비율 (1-18: 저, 19-37: 고)
    low_counts = []
    for draw in history:
        low_count = len([n for n in draw if n <= 18])
        low_counts.append(low_count)
    avg_low = sum(low_counts) / total_draws if total_draws > 0 else 0
    
    return {
        "total_draws": total_draws,
        "most_common": most_common,
        "least_common": least_common,
        "avg_sum": avg_sum,
        "sum_range": (min(sums), max(sums)) if sums else (0, 0),
        "avg_odd_count": avg_odd,
        "avg_low_count": avg_low,
        "number_frequency": dict(frequency)
    }

def validate_history(history: List[List[int]]) -> bool:
    """
    전체 히스토리 데이터를 검증합니다.
    
    Args:
        history: 검증할 히스토리 리스트
    
    Returns:
        검증 성공 여부
    """
    logger.info(f"Validating {len(history)} draws...")
    
    if not history:
        logger.error("History is empty")
        return False
    
    if not isinstance(history, list):
        logger.error(f"History must be a list, got {type(history).__name__}")
        return False
    
    is_valid = True
    invalid_count = 0
    
    # 각 추첨 결과 검증
    for idx, draw in enumerate(history):
        if not isinstance(draw, list):
            logger.error(f"Draw {idx}: Expected list, got {type(draw).__name__}")
            invalid_count += 1
            is_valid = False
            continue
        
        if not validate_draw(draw, idx):
            invalid_count += 1
            is_valid = False
    
    if invalid_count > 0:
        logger.error(f"Validation failed: {invalid_count}/{len(history)} draws are invalid")
    else:
        logger.info("All draws passed validation")
    
    return is_valid

def load_and_validate_history(history_path: str = "history.json") -> bool:
    """
    history.json을 로드하고 검증합니다.
    
    Args:
        history_path: 히스토리 파일 경로
    
    Returns:
        검증 성공 여부
    """
    logger.info(f"Loading history from {history_path}")
    
    try:
        path = Path(history_path)
        
        if not path.exists():
            logger.error(f"History file not found: {history_path}")
            return False
        
        with open(path, 'r', encoding='utf-8') as f:
            history = json.load(f)
        
        logger.info(f"History loaded: {len(history)} draws")
        
        # 검증
        if not validate_history(history):
            return False
        
        # 통계 분석
        stats = analyze_history(history)
        logger.info("\n" + "=" * 60)
        logger.info("History Statistics:")
        logger.info("=" * 60)
        logger.info(f"Total draws: {stats['total_draws']}")
        logger.info(f"Average sum: {stats['avg_sum']:.2f}")
        logger.info(f"Sum range: {stats['sum_range'][0]} - {stats['sum_range'][1]}")
        logger.info(f"Average odd count: {stats['avg_odd_count']:.2f}")
        logger.info(f"Average low (1-18) count: {stats['avg_low_count']:.2f}")
        logger.info(f"\nMost common numbers: {stats['most_common']}")
        logger.info(f"Least common numbers: {stats['least_common']}")
        logger.info("=" * 60)
        
        return True
        
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON in {history_path}: {str(e)}")
        return False
    except Exception as e:
        logger.error(f"Error loading history: {str(e)}")
        return False

def main():
    """메인 실행 함수"""
    import sys
    
    logger.info("=" * 60)
    logger.info("History Data Validation Tool")
    logger.info("=" * 60)
    
    # 히스토리 파일 검증
    if load_and_validate_history():
        logger.info("\n✓ History validation passed!")
        sys.exit(0)
    else:
        logger.error("\n✗ History validation failed!")
        sys.exit(1)

if __name__ == "__main__":
    main()
