# /// script
# requires-python = ">=3.11"
# dependencies = [
#     "beautifulsoup4",
#     "requests",
# ]
# ///
import requests
from bs4 import BeautifulSoup
import json
import re
import time
import logging
from typing import List, Optional
from datetime import datetime

# 로깅 설정
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s [%(levelname)s] %(message)s',
    handlers=[
        logging.FileHandler('fetch_data.log'),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

# 설정값
PRIMARY_URL = "http://sougaku.com/loto7/data/list1/"
BACKUP_URLS = [
    "http://sougaku.com/loto7/data/list2/",  # 예시 백업 소스
    # 실제 백업 소스가 있다면 추가
]
MAX_RETRIES = 3
RETRY_DELAY = 2  # seconds
REQUEST_TIMEOUT = 10  # seconds

def fetch_html(url: str, retries: int = MAX_RETRIES) -> Optional[str]:
    """
    URL에서 HTML을 가져옵니다. 재시도 로직 포함.
    
    Args:
        url: 대상 URL
        retries: 최대 재시도 횟수
    
    Returns:
        HTML 텍스트 또는 None (실패 시)
    """
    for attempt in range(1, retries + 1):
        try:
            logger.info(f"Fetching data from {url} (attempt {attempt}/{retries})")
            response = requests.get(url, timeout=REQUEST_TIMEOUT)
            response.raise_for_status()
            response.encoding = 'utf-8'
            logger.info(f"Successfully fetched data from {url}")
            return response.text
        
        except requests.exceptions.Timeout:
            logger.warning(f"Timeout error fetching {url} (attempt {attempt}/{retries})")
        except requests.exceptions.ConnectionError:
            logger.warning(f"Connection error fetching {url} (attempt {attempt}/{retries})")
        except requests.exceptions.HTTPError as e:
            logger.error(f"HTTP error {e.response.status_code} fetching {url}")
            break  # HTTP 에러는 재시도 안함
        except Exception as e:
            logger.error(f"Unexpected error fetching {url}: {str(e)}")
            break
        
        if attempt < retries:
            logger.info(f"Waiting {RETRY_DELAY} seconds before retry...")
            time.sleep(RETRY_DELAY)
    
    logger.error(f"Failed to fetch data from {url} after {retries} attempts")
    return None

def parse_history(html: str) -> List[List[int]]:
    """
    HTML을 파싱하여 로또 번호 히스토리를 추출합니다.
    
    Args:
        html: HTML 텍스트
    
    Returns:
        추첨 번호 리스트 (각 항목은 7개 숫자의 리스트)
    """
    history = []
    
    try:
        soup = BeautifulSoup(html, 'html.parser')
        tables = soup.find_all('table')
        
        if not tables:
            logger.warning("No tables found in HTML")
            return history
        
        logger.info(f"Found {len(tables)} tables, parsing...")
        
        for tr in soup.find_all('tr'):
            cells = tr.find_all(['td', 'th'])
            if len(cells) < 8:
                continue
            
            first_cell = cells[0].get_text(strip=True)
            
            # '第' 와 '回' 패턴 확인 (예: 第647回)
            if '第' not in first_cell or '回' not in first_cell:
                continue
            
            try:
                nums = []
                for i in range(1, 8):  # 7개 숫자
                    cell_text = cells[i].get_text(strip=True)
                    num = int(cell_text)
                    
                    # 범위 검증 (1~37)
                    if num < 1 or num > 37:
                        logger.warning(f"Number {num} out of range in row {first_cell}, skipping row")
                        break
                    
                    nums.append(num)
                
                # 정확히 7개 숫자가 파싱되었는지 확인
                if len(nums) == 7:
                    # 중복 체크
                    if len(set(nums)) != 7:
                        logger.warning(f"Duplicate numbers found in {first_cell}: {nums}, skipping row")
                        continue
                    
                    history.append(nums)
                    
            except ValueError as e:
                logger.warning(f"Failed to parse numbers in row {first_cell}: {str(e)}")
                continue
            except Exception as e:
                logger.error(f"Unexpected error parsing row {first_cell}: {str(e)}")
                continue
        
        logger.info(f"Successfully parsed {len(history)} valid draws")
        
    except Exception as e:
        logger.error(f"Error parsing HTML: {str(e)}")
    
    return history

def try_multiple_sources() -> Optional[List[List[int]]]:
    """
    여러 소스를 시도하여 데이터를 가져옵니다.
    
    Returns:
        추첨 번호 리스트 또는 None
    """
    # 주 소스 시도
    html = fetch_html(PRIMARY_URL)
    if html:
        history = parse_history(html)
        if history:
            return history
    
    # 백업 소스 시도
    logger.warning("Primary source failed, trying backup sources...")
    for backup_url in BACKUP_URLS:
        html = fetch_html(backup_url)
        if html:
            history = parse_history(html)
            if history:
                logger.info(f"Successfully fetched data from backup source: {backup_url}")
                return history
    
    return None

def validate_history_data(history: List[List[int]]) -> bool:
    """
    추출된 히스토리 데이터의 무결성을 검증합니다.
    
    Args:
        history: 검증할 히스토리 데이터
    
    Returns:
        검증 성공 여부
    """
    if not history:
        logger.error("Validation failed: history is empty")
        return False
    
    if len(history) < 10:
        logger.warning(f"History contains only {len(history)} draws (expected at least 10)")
    
    for idx, draw in enumerate(history):
        # 정확히 7개 숫자
        if len(draw) != 7:
            logger.error(f"Validation failed: draw {idx} has {len(draw)} numbers (expected 7)")
            return False
        
        # 모든 숫자가 1~37 범위
        if not all(1 <= num <= 37 for num in draw):
            logger.error(f"Validation failed: draw {idx} has out-of-range numbers: {draw}")
            return False
        
        # 중복 없음
        if len(set(draw)) != 7:
            logger.error(f"Validation failed: draw {idx} has duplicate numbers: {draw}")
            return False
    
    logger.info("Data validation passed")
    return True

def save_history(history: List[List[int]], filename: str = 'history.json') -> bool:
    """
    히스토리 데이터를 JSON 파일로 저장합니다.
    
    Args:
        history: 저장할 히스토리 데이터
        filename: 저장할 파일명
    
    Returns:
        저장 성공 여부
    """
    try:
        # 백업 생성 (기존 파일이 있다면)
        import os
        if os.path.exists(filename):
            backup_name = f"{filename}.backup.{datetime.now().strftime('%Y%m%d_%H%M%S')}"
            os.rename(filename, backup_name)
            logger.info(f"Created backup: {backup_name}")
        
        # 새 데이터 저장
        with open(filename, 'w', encoding='utf-8') as f:
            json.dump(history, f, ensure_ascii=False, indent=2)
        
        logger.info(f"Successfully saved {len(history)} draws to {filename}")
        return True
        
    except Exception as e:
        logger.error(f"Failed to save history to {filename}: {str(e)}")
        return False

def main():
    """메인 실행 함수"""
    logger.info("=" * 60)
    logger.info("Starting Loto7 data fetch process")
    logger.info("=" * 60)
    
    try:
        # 데이터 수집
        history = try_multiple_sources()
        
        if not history:
            logger.error("Failed to fetch data from all sources")
            exit(1)
        
        # 데이터 검증
        if not validate_history_data(history):
            logger.error("Data validation failed")
            exit(1)
        
        # 데이터 저장
        if not save_history(history):
            logger.error("Failed to save data")
            exit(1)
        
        logger.info("=" * 60)
        logger.info(f"Data fetch completed successfully: {len(history)} draws")
        logger.info("=" * 60)
        
    except KeyboardInterrupt:
        logger.warning("Process interrupted by user")
        exit(130)
    except Exception as e:
        logger.error(f"Unexpected error in main: {str(e)}", exc_info=True)
        exit(1)

if __name__ == "__main__":
    main()
