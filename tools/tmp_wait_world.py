import json, time, urllib.request
base='http://127.0.0.1:18771'
for i in range(40):
    try:
        with urllib.request.urlopen(base + '/api/get_state', timeout=5) as r:
            data = json.loads(r.read().decode('utf-8'))
        p = data['Data'].get('Player') or {}
        ui = data['Data'].get('Ui') or {}
        avail = data['Data'].get('Availability') or {}
        print(json.dumps({'i':i,'player_available':avail.get('PlayerAvailable'),'alive':p.get('Alive'),'menu_open':ui.get('MenuOpen'),'ui_note':ui.get('Note'),'pos':(p.get('Position') or {})}, ensure_ascii=False))
        if avail.get('PlayerAvailable') and p.get('Alive') is True and ui.get('MenuOpen') is False:
            break
    except Exception as e:
        print(json.dumps({'i':i,'error':str(e)}, ensure_ascii=False))
    time.sleep(3)
