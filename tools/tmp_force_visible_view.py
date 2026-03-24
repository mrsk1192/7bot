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
rot=request('GET','/api/get_player_rotation')['Data']['rotation']
request('POST','/api/command', {'Command':'look_to','Arguments':{'yaw':rot['Yaw'],'pitch':40.0}})
time.sleep(0.5)
state=request('GET','/api/get_look_target')
print(json.dumps(state, ensure_ascii=False))
