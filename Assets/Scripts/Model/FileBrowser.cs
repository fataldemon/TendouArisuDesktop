using System;
using System.Runtime.InteropServices;
using UnityEngine;

public static class FileBrowser
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("Comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    public static string OpenFileDialog(string title, string filter)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel(title, "", filter.Replace("|", ",").Replace("*.", ""));
#elif UNITY_STANDALONE_WIN
        var ofn = new OpenFileName();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = filter.Replace("|", "\0") + "\0";
        ofn.lpstrFile = Marshal.StringToHGlobalAuto(new string('\0', 512));
        ofn.nMaxFile = 512;
        ofn.lpstrTitle = title;
        ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;

        if (GetOpenFileName(ref ofn))
        {
            string path = Marshal.PtrToStringAuto(ofn.lpstrFile);
            Marshal.FreeHGlobal(ofn.lpstrFile);
            return path;
        }
        Marshal.FreeHGlobal(ofn.lpstrFile);
        return null;
#else
        Debug.LogError("FileBrowser not supported on this platform");
        return null;
#endif
    }
}
