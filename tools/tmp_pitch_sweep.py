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
for pitch in (25.0, 40.0, 55.0, -25.0, -40.0):
    request('POST','/api/command', {'Command':'look_to','Arguments':{'yaw':rot['Yaw'],'pitch':pitch}})
    time.sleep(0.3)
    lt=request('GET','/api/get_look_target')
    print('pitch', pitch, json.dumps(lt, ensure_ascii=False))
