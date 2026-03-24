import json, urllib.request
with urllib.request.urlopen('http://127.0.0.1:18771/api/get_state', timeout=10) as r:
    data = json.loads(r.read().decode('utf-8'))
print(json.dumps(data, ensure_ascii=False, indent=2))
