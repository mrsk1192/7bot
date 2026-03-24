import json, time, urllib.request
base='http://127.0.0.1:18771'
for i in range(60):
    try:
        with urllib.request.urlopen(base + '/api/get_state', timeout=5) as r:
            data = json.loads(r.read().decode('utf-8'))
        d=data['Data']; p=d.get('Player') or {}; ui=d.get('Ui') or {}; a=d.get('Availability') or {}
        print(json.dumps({'i':i,'player_available':a.get('PlayerAvailable'),'alive':p.get('Alive'),'menu_open':ui.get('MenuOpen'),'ui_note':ui.get('Note'),'rotation':p.get('Rotation')}, ensure_ascii=False))
        if a.get('PlayerAvailable') and p.get('Alive') is True and ui.get('MenuOpen') is False:
            break
    except Exception as e:
        print(json.dumps({'i':i,'error':str(e)}, ensure_ascii=False))
    time.sleep(3)
