"""Packaged taskbar System and Capsule preferences via UIA; no physical mouse input."""
import argparse
import ctypes as c
import ctypes.wintypes as w
import json
from pathlib import Path
import subprocess
import time

# UIA rectangles are physical pixels; keep screenshot and Win32 coordinates consistent.
c.windll.user32.SetProcessDpiAwarenessContext(c.c_void_p(-4))
from PIL import ImageGrab
from pywinauto import Desktop
from pywinauto.timings import wait_until, wait_until_passes

parser = argparse.ArgumentParser()
parser.add_argument('--exe', default='dist/PulseCapsule.exe')
parser.add_argument('--live', action='store_true', help='Read actual hardware; never enable physical fan writes.')
args = parser.parse_args()
artifact = Path(__file__).resolve().parents[1] / '.artifacts' / ('capsule-settings-' + ('live-' if args.live else 'demo-') + time.strftime('%Y%m%d-%H%M%S'))
artifact.mkdir(parents=True)
settings_path = artifact / 'settings.json'
settings_path.write_text(json.dumps({'Version': 1, 'TaskbarDocked': True, 'StartExpanded': False,
    'Notifications': False, 'AutoHideFullscreen': False, 'LastCapsuleId': 'system',
    'Providers': [{'Id': 'settings-fixture', 'Name': 'Quota fixture', 'Type': 'Manual', 'ManualRemaining': 10}]}), encoding='utf-8')
u = c.WinDLL('user32', use_last_error=True)
u.FindWindowW.argtypes = [w.LPCWSTR, w.LPCWSTR]; u.FindWindowW.restype = w.HWND
u.FindWindowExW.argtypes = [w.HWND, w.HWND, w.LPCWSTR, w.LPCWSTR]; u.FindWindowExW.restype = w.HWND
u.GetWindowThreadProcessId.argtypes = [w.HWND, c.POINTER(w.DWORD)]
u.PostMessageW.argtypes = [w.HWND, w.UINT, w.WPARAM, w.LPARAM]
u.IsWindowVisible.argtypes = [w.HWND]
checks = []; proc = None; main = None


def check(name, condition):
    checks.append({'name': name, 'passed': bool(condition)})
    print('PASS' if condition else 'FAIL', name, flush=True)
    if not condition: raise AssertionError(name)


def settings():
    # Atomic replacement can briefly deny a simultaneous Windows reader.
    return wait_until_passes(2, .03, lambda: json.loads(settings_path.read_text(encoding='utf-8-sig')), (PermissionError, FileNotFoundError))


def capture(window, name):
    r = window.rectangle()
    ImageGrab.grab(bbox=(r.left, r.top, r.right, r.bottom), include_layered_windows=True).save(artifact / name)


def capsule_handle():
    shell = u.FindWindowW('Shell_TrayWnd', None); child = None
    while True:
        child = u.FindWindowExW(shell, child, None, 'PulseCapsule Taskbar Capsule')
        if not child: return None
        pid = w.DWORD(); u.GetWindowThreadProcessId(child, c.byref(pid))
        if pid.value == proc.pid: return child


def main_handle():
    return next((x.handle for x in Desktop(backend='win32').windows(process=proc.pid, visible_only=False)
                 if x.window_text() == 'PulseCapsule'), None)


try:
    command = [str(Path(args.exe).resolve()), '--data-dir', str(artifact)]
    if not args.live: command.append('--demo')
    proc = subprocess.Popen(command, creationflags=subprocess.CREATE_NO_WINDOW)
    wait_until(20, .1, lambda: bool(capsule_handle()) and bool(main_handle()))
    main = Desktop(backend='uia').window(handle=main_handle())
    surface = Desktop(backend='uia').window(handle=capsule_handle())
    wait_until(10, .1, lambda: '°C' in surface.child_window(auto_id='ProviderText').window_text())
    width = surface.rectangle().width()
    action = surface.child_window(auto_id='TaskbarActionButton')
    check('taskbar System renders CPU and AUTO', surface.child_window(auto_id='ProviderText').window_text().startswith('CPU') and 'AUTO' in action.window_text())
    if args.live:
        check('physical fan control remains disabled', not action.is_enabled())
    else:
        action.invoke()
        wait_until(5, .1, lambda: 'COOL' in action.window_text())
        check('taskbar COOL action does not expand or resize', not u.IsWindowVisible(main.handle) and surface.rectangle().width() == width)
        check('demo COOL keeps a durable recovery record', (artifact / 'demo-cooling.json').exists())
        action.invoke()
        wait_until(5, .1, lambda: 'AUTO' in action.window_text())
        check('taskbar AUTO confirms demo recovery', not (artifact / 'demo-cooling.json').exists())
    capture(surface, 'system-taskbar.png')
    surface.child_window(auto_id='TaskbarExpandButton').invoke()
    main.child_window(auto_id='TemperatureText').wait('visible', timeout=10)
    check('taskbar System expands to actual telemetry view', 'RPM' in main.child_window(auto_id='FansText').window_text())
    capture(main, 'system-expanded.png')
    if not args.live:
        main.child_window(auto_id='SettingsButton').invoke()
        preferences = main.child_window(title='PulseCapsule 设置', control_type='Window')
        preferences.child_window(auto_id='CapsuleSettingsButton').wait('visible', timeout=10)
        preferences.child_window(auto_id='CapsuleSettingsButton').invoke()
        dialog = preferences.child_window(title='PulseCapsule Capsules', control_type='Window')
        dialog.wait('visible', timeout=10)
        dialog = Desktop(backend='uia').window(handle=dialog.handle)
        dialog.child_window(auto_id='CapsuleList').get_item(2).select()
        dialog.child_window(title='↑ 上移', control_type='Button').invoke()
        dialog.child_window(title='↑ 上移', control_type='Button').invoke()
        dialog.child_window(auto_id='SaveCapsulesButton').invoke()
        wait_until(5, .1, lambda: settings()['Capsules'][0]['Id'] == 'system')
        check('reordering persists and preserves current Capsule', settings()['LastCapsuleId'] == 'system')
        dialog.child_window(auto_id='RotateSeconds').set_edit_text('5')
        dialog.child_window(auto_id='RotateCheck').toggle()
        dialog.child_window(auto_id='SaveCapsulesButton').invoke()
        for expected, prefix in [('clock', time.strftime('%Y-%m-%d')), ('quota:settings-fixture', 'Quota fixture'), ('system', 'CPU')]:
            wait_until(8, .1, lambda: surface.child_window(auto_id='ProviderText').window_text().startswith(prefix))
            check('carousel follows configured order: ' + expected, settings()['LastCapsuleId'] == expected)
        dialog.child_window(auto_id='RotateCheck').toggle()
        dialog.child_window(auto_id='SaveCapsulesButton').invoke()
        wait_until(5, .1, lambda: not settings()['AutoRotate'])
        dialog.close(); preferences.close()
        check('carousel can be disabled without changing selection', settings()['LastCapsuleId'] == 'system')
    u.PostMessageW(main.handle, 0x10, 0, 0)
    proc.wait(timeout=10)
    check('clean shutdown removes taskbar surface', proc.returncode == 0 and not capsule_handle())
finally:
    if proc and proc.poll() is None:
        if main: u.PostMessageW(main.handle, 0x10, 0, 0)
        try: proc.wait(timeout=8)
        except subprocess.TimeoutExpired: proc.kill(); proc.wait()
    (artifact / 'result.json').write_text(json.dumps({'exe': str(Path(args.exe).resolve()), 'live': args.live,
        'physical': 'not-run', 'checks': checks}, indent=2), encoding='utf-8')
    print('ARTIFACTS', artifact, flush=True)
