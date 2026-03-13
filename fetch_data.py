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

url = "http://sougaku.com/loto7/data/list1/"
response = requests.get(url)
response.encoding = 'utf-8'
soup = BeautifulSoup(response.text, 'html.parser')

history = []

for tr in soup.find_all('tr'):
    cells = tr.find_all(['td', 'th'])
    if len(cells) >= 8:
        first = cells[0].get_text(strip=True)
        # Check if first cell has '第' and '回'
        if '第' in first and '回' in first:
            try:
                nums = []
                for i in range(1, 8):
                    nums.append(int(cells[i].get_text(strip=True)))
                history.append(nums)
            except ValueError:
                pass

if not history:
    print("Could not parse history.")
    exit(1)

latest_data = history

with open('history.json', 'w') as f:
    json.dump(latest_data, f)

print(f"Successfully saved {len(latest_data)} real draws to history.json.")
