import json, urllib.request, time, math
base='http://127.0.0.1:18771'
def req(method, path, body=None):
    data=None
    headers={'Accept':'application/json'}
    if body is not None:
        data=json.dumps(body).encode('utf-8')
        headers['Content-Type']='application/json'
    rq=urllib.request.Request(base+path, data=data, headers=headers, method=method)
    with urllib.request.urlopen(rq, timeout=10) as r:
        return json.loads(r.read().decode('utf-8'))
print('before', json.dumps(req('GET','/api/get_state'), ensure_ascii=False))
print('toggle', json.dumps(req('POST','/api/command', {'Command':'console_toggle','Arguments':{}}), ensure_ascii=False))
time.sleep(0.8)
state1=req('GET','/api/get_state')['Data']
print('after_toggle', json.dumps(state1, ensure_ascii=False))
pos1=state1['Player']['Position']
req('POST','/api/command', {'Command':'move_forward_start','Arguments':{}})
time.sleep(4.0)
req('POST','/api/command', {'Command':'move_forward_stop','Arguments':{}})
state2=req('GET','/api/get_state')['Data']
pos2=state2['Player']['Position']
d=math.dist((pos1['X'],pos1['Y'],pos1['Z']),(pos2['X'],pos2['Y'],pos2['Z']))
print(json.dumps({'distance':d,'menu_open':state2['Ui']['MenuOpen'],'console_open':state2['Ui']['ConsoleOpen'],'movement_locked':state2['InputState']['MovementLocked'],'alive':state2['Player']['Alive']}, ensure_ascii=False))
