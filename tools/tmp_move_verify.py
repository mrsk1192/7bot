import json, math, time, urllib.request
base='http://127.0.0.1:18771'
def get(path):
    with urllib.request.urlopen(base+path, timeout=10) as r:
        return json.loads(r.read().decode('utf-8'))
def post(cmd, args=None):
    body=json.dumps({'Command':cmd,'Arguments':args or {}}).encode('utf-8')
    req=urllib.request.Request(base+'/api/command', data=body, headers={'Content-Type':'application/json','Accept':'application/json'}, method='POST')
    with urllib.request.urlopen(req, timeout=10) as r:
        return json.loads(r.read().decode('utf-8'))
state1=get('/api/get_state')['Data']
pos1=state1['Player']['Position']
post('move_forward_start')
time.sleep(4.5)
post('move_forward_stop')
state2=get('/api/get_state')['Data']
pos2=state2['Player']['Position']
d=math.dist((pos1['X'],pos1['Y'],pos1['Z']),(pos2['X'],pos2['Y'],pos2['Z']))
print(json.dumps({'start':pos1,'end':pos2,'distance':d,'alive':state2['Player']['Alive'],'menu_open':state2['Ui']['MenuOpen'],'movement_locked':state2['InputState']['MovementLocked']}, ensure_ascii=False))
