using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ZulfarakRPG
{
    // Registers IBM Plex Sans into the PROCESS (private font, from the bundled TTF bytes)
    // so the native Win32/GDI popups (NPC menu, world map, friends invite) can render with
    // it via CreateFontIndirectW. Falls back to "Segoe UI" if it can't be loaded, so the
    // popups always have a valid face name.
    public static class NativeFont
    {
        public const string Fallback = "Segoe UI";
        public const string Family   = "IBM Plex Sans";

        static bool     _tried;
        static bool     _ok;
        static GCHandle _pin;   // keeps the TTF bytes alive for the process lifetime

        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, out uint pcFonts);

        // Face name for LOGFONT.lfFaceName — IBM Plex Sans once loaded, otherwise Segoe UI.
        public static string Face
        {
            get { EnsureLoaded(); return _ok ? Family : Fallback; }
        }

        static void EnsureLoaded()
        {
            if (_tried) return;
            _tried = true;
#if !UNITY_EDITOR
            try
            {
                var ta = Resources.Load<TextAsset>("Fonts/IBMPlexSansRegular");
                if (ta == null || ta.bytes == null || ta.bytes.Length == 0) return;
                _pin = GCHandle.Alloc(ta.bytes, GCHandleType.Pinned);
                var handle = AddFontMemResourceEx(_pin.AddrOfPinnedObject(),
                    (uint)ta.bytes.Length, IntPtr.Zero, out uint count);
                _ok = handle != IntPtr.Zero && count > 0;
            }
            catch { _ok = false; }
#endif
        }
    }
}
