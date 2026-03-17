#!/usr/bin/env python3
"""
config.json 검증 및 안전성 체크 모듈
"""
import json
import logging
from typing import Dict, Any, Optional
from pathlib import Path

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s'
)
logger = logging.getLogger(__name__)

# 설정 스키마 정의
CONFIG_SCHEMA = {
    "Scoring": {
        "FrequencyWeight": {"type": float, "min": 0.0, "max": 10.0},
        "ColdBonusThreshold": {"type": int, "min": 1, "max": 50},
        "ColdBonusValue": {"type": float, "min": 0.0, "max": 100.0},
        "HotPenaltyThreshold": {"type": int, "min": 1, "max": 50},
        "HotPenaltyValue": {"type": float, "min": 0.0, "max": 100.0}
    },
    "WMA": {
        "WindowSize": {"type": int, "min": 10, "max": 500}
    },
    "Markov": {
        "WindowSize": {"type": int, "min": 10, "max": 500}
    }
}

def validate_value(value: Any, schema: Dict[str, Any], path: str) -> bool:
    """
    단일 값의 타입과 범위를 검증합니다.
    
    Args:
        value: 검증할 값
        schema: 스키마 정의
        path: 설정 경로 (로깅용)
    
    Returns:
        검증 성공 여부
    """
    expected_type = schema["type"]
    
    # 타입 검증
    if not isinstance(value, expected_type):
        logger.error(f"{path}: Expected {expected_type.__name__}, got {type(value).__name__}")
        return False
    
    # 범위 검증 (숫자 타입의 경우)
    if expected_type in (int, float):
        if "min" in schema and value < schema["min"]:
            logger.error(f"{path}: Value {value} is below minimum {schema['min']}")
            return False
        if "max" in schema and value > schema["max"]:
            logger.error(f"{path}: Value {value} exceeds maximum {schema['max']}")
            return False
    
    return True

def validate_config(config: Dict[str, Any]) -> bool:
    """
    config.json 전체 구조와 값을 검증합니다.
    
    Args:
        config: 검증할 설정 딕셔너리
    
    Returns:
        검증 성공 여부
    """
    is_valid = True
    
    # 최상위 섹션 존재 확인
    for section_name, section_schema in CONFIG_SCHEMA.items():
        if section_name not in config:
            logger.error(f"Missing required section: {section_name}")
            is_valid = False
            continue
        
        section = config[section_name]
        
        if not isinstance(section, dict):
            logger.error(f"{section_name}: Expected dict, got {type(section).__name__}")
            is_valid = False
            continue
        
        # 각 필드 검증
        for field_name, field_schema in section_schema.items():
            path = f"{section_name}.{field_name}"
            
            if field_name not in section:
                logger.error(f"Missing required field: {path}")
                is_valid = False
                continue
            
            value = section[field_name]
            if not validate_value(value, field_schema, path):
                is_valid = False
    
    return is_valid

def load_and_validate_config(config_path: str = "config.json") -> Optional[Dict[str, Any]]:
    """
    config.json을 로드하고 검증합니다.
    
    Args:
        config_path: 설정 파일 경로
    
    Returns:
        검증된 설정 딕셔너리 또는 None (실패 시)
    """
    logger.info(f"Loading configuration from {config_path}")
    
    try:
        path = Path(config_path)
        
        if not path.exists():
            logger.error(f"Configuration file not found: {config_path}")
            return None
        
        with open(path, 'r', encoding='utf-8') as f:
            config = json.load(f)
        
        logger.info("Configuration loaded successfully")
        
        # 검증
        if not validate_config(config):
            logger.error("Configuration validation failed")
            return None
        
        logger.info("Configuration validation passed")
        return config
        
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON in {config_path}: {str(e)}")
        return None
    except Exception as e:
        logger.error(f"Error loading configuration: {str(e)}")
        return None

def create_default_config(output_path: str = "config.json.default") -> bool:
    """
    기본 설정 파일을 생성합니다.
    
    Args:
        output_path: 출력 파일 경로
    
    Returns:
        생성 성공 여부
    """
    default_config = {
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
    
    try:
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(default_config, f, indent=2, ensure_ascii=False)
        logger.info(f"Default configuration saved to {output_path}")
        return True
    except Exception as e:
        logger.error(f"Failed to create default config: {str(e)}")
        return False

def main():
    """메인 실행 함수"""
    import sys
    
    logger.info("=" * 60)
    logger.info("Configuration Validation Tool")
    logger.info("=" * 60)
    
    # 설정 파일 검증
    config = load_and_validate_config()
    
    if config is None:
        logger.error("Configuration validation failed")
        
        # 기본 설정 생성 제안
        response = input("\nCreate default configuration? (y/n): ").strip().lower()
        if response == 'y':
            create_default_config()
        
        sys.exit(1)
    
    logger.info("=" * 60)
    logger.info("Configuration is valid!")
    logger.info("=" * 60)
    
    # 설정값 출력
    print("\nCurrent Configuration:")
    print(json.dumps(config, indent=2))

if __name__ == "__main__":
    main()
