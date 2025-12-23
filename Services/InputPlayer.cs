using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using he_dieu_hanh.Models;

namespace he_dieu_hanh.Services
{
    public class InputPlayer
    {
        public async Task PlayEvent(RecordedEvent evt, bool useEmbeddedModifiers = true)
        {
            switch (evt.EventType)
            {
                case "MouseDown": await PlayMouse(evt, true); break;
                case "MouseUp": await PlayMouse(evt, false); break;
                case "MouseMove": await PlayMouseMove(evt); break;
                case "MouseWheel": await PlayMouseWheel(evt); break;
                case "KeyDown": await PlayKey(evt, false, useEmbeddedModifiers); break;
                case "KeyUp": await PlayKey(evt, true, useEmbeddedModifiers); break;
            }
        }

        private async Task PlayMouse(RecordedEvent evt, bool isDown)
        {
            int x = GetInt(evt.Data["X"]);
            int y = GetInt(evt.Data["Y"]);
            string button = evt.Data["Button"].ToString() ?? "Left";

            uint flags = 0;
            if (button == "Left") flags = isDown ? Win32Api.MOUSEEVENTF_LEFTDOWN : Win32Api.MOUSEEVENTF_LEFTUP;
            else if (button == "Right") flags = isDown ? Win32Api.MOUSEEVENTF_RIGHTDOWN : Win32Api.MOUSEEVENTF_RIGHTUP;
            else if (button == "Middle") flags = isDown ? Win32Api.MOUSEEVENTF_MIDDLEDOWN : Win32Api.MOUSEEVENTF_MIDDLEUP;

            await SendMouseInput(x, y, flags);
        }

        private async Task PlayMouseMove(RecordedEvent evt)
        {
            int x = GetInt(evt.Data["X"]);
            int y = GetInt(evt.Data["Y"]);
            await SendMouseInput(x, y, Win32Api.MOUSEEVENTF_MOVE | Win32Api.MOUSEEVENTF_ABSOLUTE);
        }

        private async Task PlayMouseWheel(RecordedEvent evt)
        {
            int x = GetInt(evt.Data["X"]);
            int y = GetInt(evt.Data["Y"]);
            int delta = GetInt(evt.Data["Delta"]);

            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_MOUSE,
                union = new Win32Api.INPUTUNION
                {
                    mouse = new Win32Api.MOUSEINPUT
                    {
                        dx = x * 65536 / Screen.PrimaryScreen.Bounds.Width,
                        dy = y * 65536 / Screen.PrimaryScreen.Bounds.Height,
                        mouseData = (uint)delta,
                        dwFlags = Win32Api.MOUSEEVENTF_WHEEL | Win32Api.MOUSEEVENTF_ABSOLUTE
                    }
                }
            };
            Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Win32Api.INPUT)));
            await Task.Delay(10);
        }

        private async Task PlayKey(RecordedEvent evt, bool isUp, bool useModifiers)
        {
            int keyValue = GetInt(evt.Data["KeyValue"]);
            
            if (useModifiers && !isUp) HandleModifiers(evt, false); // Press modifiers
            
            await SendKeyInput((ushort)keyValue, isUp);

            if (useModifiers && isUp) HandleModifiers(evt, true); // Release modifiers
        }

        private async void HandleModifiers(RecordedEvent evt, bool isUp)
        {
            if (evt.Data.ContainsKey("Modifiers"))
            {
                string mods = evt.Data["Modifiers"].ToString();
                if (mods.Contains("Ctrl")) await SendKeyInput(0x11, isUp);
                if (mods.Contains("Shift")) await SendKeyInput(0x10, isUp);
                if (mods.Contains("Alt")) await SendKeyInput(0x12, isUp);
            }
        }

        private async Task SendMouseInput(int x, int y, uint flags)
        {
            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_MOUSE,
                union = new Win32Api.INPUTUNION
                {
                    mouse = new Win32Api.MOUSEINPUT
                    {
                        dx = x * 65536 / Screen.PrimaryScreen.Bounds.Width,
                        dy = y * 65536 / Screen.PrimaryScreen.Bounds.Height,
                        dwFlags = flags | Win32Api.MOUSEEVENTF_ABSOLUTE
                    }
                }
            };
            Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Win32Api.INPUT)));
            await Task.Delay(10);
        }

        private async Task SendKeyInput(ushort vk, bool keyUp)
        {
            uint flags = keyUp ? Win32Api.KEYEVENTF_KEYUP : 0;
            if ((vk >= 33 && vk <= 46) || vk == 91 || vk == 92) flags |= Win32Api.KEYEVENTF_EXTENDEDKEY;

            var input = new Win32Api.INPUT
            {
                type = Win32Api.INPUT_KEYBOARD,
                union = new Win32Api.INPUTUNION
                {
                    keyboard = new Win32Api.KEYBDINPUT { wVk = vk, dwFlags = flags }
                }
            };
            Win32Api.SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Win32Api.INPUT)));
            await Task.Delay(10);
        }

        // Helper xử lý JSON an toàn
        private int GetInt(object data)
        {
            if (data is int i) return i;
            if (data is long l) return (int)l;
            if (data is JsonElement e && e.ValueKind == JsonValueKind.Number) return e.GetInt32();
            try { return Convert.ToInt32(data); } catch { return 0; }
        }
    }
}
