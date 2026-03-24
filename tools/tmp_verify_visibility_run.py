import json, math, time, urllib.request
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
look=req('GET','/api/get_look_target')
state1=req('GET','/api/get_state')['Data']
pos1=state1['Player']['Position']
req('POST','/api/command', {'Command':'move_forward_start','Arguments':{}})
time.sleep(4.5)
req('POST','/api/command', {'Command':'move_forward_stop','Arguments':{}})
state2=req('GET','/api/get_state')['Data']
pos2=state2['Player']['Position']
d=math.dist((pos1['X'],pos1['Y'],pos1['Z']),(pos2['X'],pos2['Y'],pos2['Z']))
print(json.dumps({'look_target':look['Data'],'start':pos1,'end':pos2,'distance':d,'alive':state2['Player']['Alive'],'menu_open':state2['Ui']['MenuOpen'],'rotation':state2['Player']['Rotation']}, ensure_ascii=False, indent=2))
