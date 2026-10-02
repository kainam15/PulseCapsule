"""Actual packaged host interactions; demo controls never touch physical fans."""
import argparse
import ctypes as c
import ctypes.wintypes as w
import json
from pathlib import Path
import re
import subprocess
import time

from pywinauto import Application, Desktop, mouse
from pywinauto.timings import wait_until
from outside_click_target import collapse_panel, require_input_desktop

parser = argparse.ArgumentParser()
parser.add_argument('--exe', default='dist/PulseCapsule.exe')
parser.add_argument('--live', action='store_true')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
artifact = root / '.artifacts' / ('system-' + ('live-' if args.live else 'demo-') + time.strftime('%Y%m%d-%H%M%S'))
artifact.mkdir(parents=True)
settings_path = artifact / 'settings.json'
settings_path.write_text(json.dumps({'Version': 1, 'StartExpanded': False, 'LeftPixels': 350, 'TopPixels': 500,
    'Notifications': False, 'AutoHideFullscreen': False, 'LastCapsuleId': 'system' if args.live else 'clock',
    'Providers': [{'Id': 'codex-fixture', 'Name': 'Codex', 'Type': 'Codex', 'Enabled': not args.live}]}), encoding='utf-8')
u = c.WinDLL('user32', use_last_error=True)
u.PostMessageW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
u.GetCursorPos.argtypes = [c.POINTER(w.POINT)]
u.SetCursorPos.argtypes = [c.c_int, c.c_int]
u.GetForegroundWindow.restype = w.HWND
u.SetForegroundWindow.argtypes = [w.HWND]
u.IsWindow.argtypes = [w.HWND]
cursor = w.POINT(); u.GetCursorPos(c.byref(cursor)); foreground = u.GetForegroundWindow()
checks = []
proc = None

def check(name, condition):
    checks.append({'name': name, 'passed': bool(condition)})
    print('PASS' if condition else 'FAIL', name, flush=True)
    if not condition: raise AssertionError(name)

def start():
    global proc
    command = [str(Path(args.exe).resolve()), '--data-dir', str(artifact)]
    if not args.live: command.append('--demo')
    proc = subprocess.Popen(command, creationflags=subprocess.CREATE_NO_WINDOW)
    app = Application(backend='uia').connect(process=proc.pid, timeout=20)
    win = app.window(title='PulseCapsule')
    win.wait('visible', timeout=20)
    win.child_window(auto_id='CapsuleText').wait('visible', timeout=10)
    return win

def stop(win):
    u.PostMessageW(win.handle, 0x10, 0, 0)
    proc.wait(timeout=15)
    check('graceful process exit', proc.returncode == 0)

def capsule_text(win): return win.child_window(auto_id='CapsuleText').window_text()

def wheel(win, direction, prefix):
    rect = win.child_window(auto_id='ExpandButton').rectangle()
    mouse.scroll(coords=(rect.left + 12, (rect.top + rect.bottom)//2), wheel_dist=direction)
    wait_until(5, .1, lambda: capsule_text(win).startswith(prefix))

try:
    require_input_desktop()
    win = start()
    width = win.rectangle().width()
    if args.live:
        wait_until(15, .2, lambda: bool(re.match(r'CPU \d+°C', capsule_text(win))))
        check('live CPU temperature rendered from packaged EXE', True)
        check('unverified physical fan control stays disabled', not win.child_window(auto_id='CapsuleActionButton').is_enabled())
    else:
        check('clock preserves date minute format', bool(re.fullmatch(r'\d{4}-\d{2}-\d{2}\u2003\d{2}:\d{2}', capsule_text(win))))
        wheel(win, -1, 'Codex'); check('wheel selects quota through host', '%' in capsule_text(win))
        wheel(win, -1, 'CPU'); check('wheel selects system through host', True)
        check('system retains compact width', win.rectangle().width() == width)
        action = win.child_window(auto_id='CapsuleActionButton')
        wait_until(5, .1, action.is_enabled)
        mouse.click(coords=tuple(action.rectangle().mid_point()))
        wait_until(5, .1, lambda: 'COOL' in action.window_text())
        check('demo COOL action does not expand capsule', win.rectangle().width() == width)
        check('demo recovery record exists while active', (artifact/'demo-cooling.json').exists())
    win.child_window(auto_id='ExpandButton').invoke()
    win.child_window(auto_id='TemperatureText').wait('visible', timeout=10)
    wait_until(10, .2, lambda: 'CPU Fan' in win.child_window(auto_id='FansText').window_text())
    text = '\n'.join(x.window_text() for x in win.descendants(control_type='Text'))
    win.capture_as_image().save(artifact/'expanded.png')
    if 'CPU Fan' not in text or 'RPM' not in text: print('Observed detail text:', repr(text), flush=True)
    check('expanded details show CPU load and fan RPM', 'Load' in text and 'CPU Fan' in text and 'RPM' in text)
    check('unsupported third fan is hidden', 'System Fan' not in win.child_window(auto_id='FansText').window_text())
    win.capture_as_image().save(artifact/'expanded.png')
    if args.live:
        check('live read-only reason is visible', '只读' in win.child_window(auto_id='ControlMessage').window_text())
    else:
        check('cooling statistics are visible', 'Before' in text and 'Current' in text and 'Minimum' in text)
        win.child_window(auto_id='CoolingButton').invoke()
        wait_until(5, .1, lambda: not (artifact/'demo-cooling.json').exists())
        check('restore action clears confirmed demo state', win.child_window(auto_id='CoolingText').window_text() == 'OFF')
        collapse_panel(win)
        wheel(win, -1, time.strftime('%Y-%m-%d')); check('system wraps to clock with same algorithm', True)
        wheel(win, 1, 'CPU')
        stop(win)
        win = start()
        wait_until(5, .1, lambda: capsule_text(win).startswith('CPU'))
        check('last capsule survives restart', json.loads(settings_path.read_text(encoding='utf-8-sig'))['LastCapsuleId'] == 'system')
        win.child_window(auto_id='ExpandButton').invoke()
        win.child_window(auto_id='SettingsButton').invoke()
        settings = win.child_window(title='PulseCapsule 设置', control_type='Window')
        settings.child_window(auto_id='CapsuleSettingsButton').wait('visible', timeout=10)
        settings.child_window(auto_id='CapsuleSettingsButton').invoke()
        capsule_settings = settings.child_window(title='PulseCapsule Capsules', control_type='Window')
        capsule_settings.wait('visible', timeout=10)
        capsule_settings = Desktop(backend='uia').window(handle=capsule_settings.handle)
        capsule_settings.child_window(title='system', control_type='CheckBox').toggle()
        capsule_settings.child_window(auto_id='SaveCapsulesButton').invoke()
        wait_until(5, .1, lambda: not next(x['Enabled'] for x in json.loads(settings_path.read_text(encoding='utf-8-sig'))['Capsules'] if x['Id']=='system'))
        check('Capsule settings disable System persistently', True)
        capsule_settings.capture_as_image().save(artifact/'settings.png')
        capsule_settings.close(); settings.close()
        collapse_panel(win)
        check('disabled current capsule falls back safely', not capsule_text(win).startswith('CPU'))
    stop(win)
finally:
    if proc and proc.poll() is None:
        proc.kill(); proc.wait(timeout=5)
    (artifact/'result.json').write_text(json.dumps({'exe':str(Path(args.exe).resolve()), 'live':args.live, 'checks':checks},indent=2),encoding='utf-8')
    u.SetCursorPos(cursor.x, cursor.y)
    if foreground and u.IsWindow(foreground): u.SetForegroundWindow(foreground)
    print('Artifacts:', artifact, flush=True)
