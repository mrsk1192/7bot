import json, urllib.request, time
base='http://127.0.0.1:18771'
def req(path, body=None):
    data=None
    headers={'Accept':'application/json'}
    if body is not None:
        data=json.dumps(body).encode('utf-8')
        headers['Content-Type']='application/json'
    with urllib.request.urlopen(urllib.request.Request(base+path, data=data, headers=headers, method='POST' if body is not None else 'GET'), timeout=10) as r:
        return json.loads(r.read().decode('utf-8'))
print('before')
print(json.dumps(req('/api/get_look_target'), ensure_ascii=False))
for _ in range(3):
    print(json.dumps(req('/api/command', {'Command':'look_down','Arguments':{}}), ensure_ascii=False))
    time.sleep(0.2)
print('after')
print(json.dumps(req('/api/get_player_rotation'), ensure_ascii=False))
print(json.dumps(req('/api/get_look_target'), ensure_ascii=False))
