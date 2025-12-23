using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using he_dieu_hanh.Models;

namespace he_dieu_hanh.Services
{
    public class InputRecorder : IDisposable
    {
        private IntPtr _hookIDKeyboard = IntPtr.Zero;
        private IntPtr _hookIDMouse = IntPtr.Zero;
        private Win32Api.LowLevelKeyboardProc _procKeyboard;
        private Win32Api.LowLevelMouseProc _procMouse;
        
        private bool _isRecording = false;
        private DateTime _startTime;
        private RecordedEvent? _lastEvent;

        // Sự kiện để báo cho UI biết có hành động mới
        public event Action<RecordedEvent> OnEventRecorded;

        public InputRecorder()
        {
            _procKeyboard = HookCallbackKeyboard;
            _procMouse = HookCallbackMouse;
        }

        public void Start()
        {
            if (_isRecording) return;
            
            _isRecording = true;
            _startTime = DateTime.Now;
            _lastEvent = null;

            _hookIDKeyboard = SetHook(_procKeyboard, Win32Api.WH_KEYBOARD_LL);
            _hookIDMouse = SetHook(_procMouse, Win32Api.WH_MOUSE_LL);
        }

        public void Stop()
        {
            if (!_isRecording) return;
            
            _isRecording = false;
            Win32Api.UnhookWindowsHookEx(_hookIDKeyboard);
            Win32Api.UnhookWindowsHookEx(_hookIDMouse);
            _hookIDKeyboard = IntPtr.Zero;
            _hookIDMouse = IntPtr.Zero;
        }

        private IntPtr SetHook(Delegate proc, int hookId)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                if (proc is Win32Api.LowLevelKeyboardProc kProc)
                    return Win32Api.SetWindowsHookEx(hookId, kProc, Win32Api.GetModuleHandle(curModule.ModuleName), 0);
                else if (proc is Win32Api.LowLevelMouseProc mProc)
                    return Win32Api.SetWindowsHookEx(hookId, mProc, Win32Api.GetModuleHandle(curModule.ModuleName), 0);
                return IntPtr.Zero;
            }
        }

        private void ProcessEvent(string type, Dictionary<string, object> data)
        {
            if (!_isRecording) return;

            var now = DateTime.Now;
            long timestamp = (long)(now - _startTime).TotalMilliseconds;
            int delayMs = 0;

            if (_lastEvent != null)
            {
                delayMs = (int)(timestamp - _lastEvent.Timestamp);
            }

            var evt = new RecordedEvent
            {
                EventType = type,
                Timestamp = timestamp,
                DelayMs = delayMs,
                Data = data
            };

            _lastEvent = evt;
            OnEventRecorded?.Invoke(evt);
        }

        private IntPtr HookCallbackKeyboard(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)Win32Api.WM_KEYDOWN || wParam == (IntPtr)Win32Api.WM_SYSKEYDOWN || wParam == (IntPtr)Win32Api.WM_KEYUP || wParam == (IntPtr)Win32Api.WM_SYSKEYUP))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;
                bool isDown = (wParam == (IntPtr)Win32Api.WM_KEYDOWN || wParam == (IntPtr)Win32Api.WM_SYSKEYDOWN);

                // Không còn lọc phím Windows nữa, mọi phím đều được ghi nhận
                var data = new Dictionary<string, object> { { "Key", key.ToString() }, { "KeyValue", (int)key } };
                var modifiers = new List<string>();
                if ((Control.ModifierKeys & Keys.Control) != 0) modifiers.Add("Ctrl");
                if ((Control.ModifierKeys & Keys.Shift) != 0) modifiers.Add("Shift");
                if ((Control.ModifierKeys & Keys.Alt) != 0) modifiers.Add("Alt");
                if (modifiers.Count > 0) data["Modifiers"] = string.Join("+", modifiers);

                ProcessEvent(isDown ? "KeyDown" : "KeyUp", data);
            }
            return Win32Api.CallNextHookEx(_hookIDKeyboard, nCode, wParam, lParam);
        }

        private IntPtr HookCallbackMouse(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<Win32Api.MSLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;

                if (msg == Win32Api.WM_MOUSEMOVE)
                {
                    ProcessEvent("MouseMove", new Dictionary<string, object> { { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
                else if (msg == Win32Api.WM_LBUTTONDOWN || msg == Win32Api.WM_RBUTTONDOWN || msg == Win32Api.WM_MBUTTONDOWN)
                {
                    string btn = msg == Win32Api.WM_LBUTTONDOWN ? "Left" : (msg == Win32Api.WM_RBUTTONDOWN ? "Right" : "Middle");
                    ProcessEvent("MouseDown", new Dictionary<string, object> { { "Button", btn }, { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
                else if (msg == Win32Api.WM_LBUTTONUP || msg == Win32Api.WM_RBUTTONUP || msg == Win32Api.WM_MBUTTONUP)
                {
                    string btn = msg == Win32Api.WM_LBUTTONUP ? "Left" : (msg == Win32Api.WM_RBUTTONUP ? "Right" : "Middle");
                    ProcessEvent("MouseUp", new Dictionary<string, object> { { "Button", btn }, { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
                else if (msg == Win32Api.WM_MOUSEWHEEL)
                {
                    short delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                    ProcessEvent("MouseWheel", new Dictionary<string, object> { { "Delta", delta }, { "X", hookStruct.pt.x }, { "Y", hookStruct.pt.y } });
                }
            }
            return Win32Api.CallNextHookEx(_hookIDMouse, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
