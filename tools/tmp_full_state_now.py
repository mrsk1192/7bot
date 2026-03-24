import json, urllib.request
base='http://127.0.0.1:18771'
with urllib.request.urlopen(base+'/api/get_state', timeout=10) as r:
    data=json.loads(r.read().decode('utf-8'))
print(json.dumps(data, ensure_ascii=False, indent=2))
