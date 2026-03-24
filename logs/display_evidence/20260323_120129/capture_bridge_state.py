import json
import urllib.request

base = 'http://127.0.0.1:18771'
payload = {}
for path in [
    '/api/get_version',
    '/api/get_state',
    '/api/get_player_rotation',
    '/api/get_look_target',
    '/api/get_interaction_context'
]:
    try:
        with urllib.request.urlopen(base + path, timeout=10) as response:
            payload[path] = json.loads(response.read().decode('utf-8'))
    except Exception as exc:
        payload[path] = {'error': str(exc)}

print(json.dumps(payload, ensure_ascii=False, indent=2))
