import json, urllib.request, time
base='http://127.0.0.1:18771'
def request(method, path, body=None):
    data=None
    headers={'Accept':'application/json'}
    if body is not None:
        data=json.dumps(body).encode('utf-8')
        headers['Content-Type']='application/json'
    req=urllib.request.Request(base+path, data=data, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=10) as r:
        return json.loads(r.read().decode('utf-8'))
state=request('GET','/api/get_player_rotation')
rot=state['Data']['rotation']
yaw=rot.get('Yaw', 0)
print('before', json.dumps(state, ensure_ascii=False))
resp=request('POST','/api/command', {'Command':'look_to','Arguments':{'yaw':yaw,'pitch':25.0}})
print('cmd', json.dumps(resp, ensure_ascii=False))
time.sleep(0.5)
after=request('GET','/api/get_player_rotation')
print('after', json.dumps(after, ensure_ascii=False))
