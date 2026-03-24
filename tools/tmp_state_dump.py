import json, urllib.request
base = 'http://127.0.0.1:18771'
for path in ['/api/get_state', '/api/get_player_rotation', '/api/get_look_target']:
    with urllib.request.urlopen(base + path, timeout=10) as r:
        data = json.loads(r.read().decode('utf-8'))
    print(path)
    print(json.dumps(data, ensure_ascii=False))
