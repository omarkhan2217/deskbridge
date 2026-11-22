using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Deskbridge.Engines.Os;

/// <summary>
/// Who signed an exe: the common name of its Authenticode signer, only when Windows says the signature is
/// valid. Revocation is not checked online, so this makes no network calls.
/// </summary>
public static unsafe class Publisher
{
    public static string? Of(string exePath)
    {
        if (!File.Exists(exePath) || !IsTrusted(exePath))
        {
            return null;
        }

        try
        {
#pragma warning disable SYSLIB0057 // the loader APIs cannot read a certificate out of a signed PE file
            using var signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(exePath));
#pragma warning restore SYSLIB0057
            return signer.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        }
        catch (CryptographicException)
        {
            return null; // signed through a catalog, not inside the file
        }
    }

    static bool IsTrusted(string path)
    {
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"); // WINTRUST_ACTION_GENERIC_VERIFY_V2
        fixed (char* file = path)
        {
            var fileInfo = new WinTrustFileInfo
            {
                cbStruct = (uint)sizeof(WinTrustFileInfo),
                pcwszFilePath = file,
            };
            var data = new WinTrustData
            {
                cbStruct = (uint)sizeof(WinTrustData),
                dwUIChoice = 2,            // WTD_UI_NONE
                fdwRevocationChecks = 0,   // WTD_REVOKE_NONE
                dwUnionChoice = 1,         // WTD_CHOICE_FILE
                pFile = &fileInfo,
                dwStateAction = 1,         // WTD_STATEACTION_VERIFY
                dwProvFlags = 0x1000,      // WTD_CACHE_ONLY_URL_RETRIEVAL: never go to the network
            };
            var result = WinVerifyTrust(-1, &action, &data);
            data.dwStateAction = 2; // WTD_STATEACTION_CLOSE
            WinVerifyTrust(-1, &action, &data);
            return result == 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WinTrustFileInfo
    {
        public uint cbStruct;
        public char* pcwszFilePath;
        public nint hFile;
        public nint pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WinTrustData
    {
        public uint cbStruct;
        public nint pPolicyCallbackData;
        public nint pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public WinTrustFileInfo* pFile;
        public uint dwStateAction;
        public nint hWVTStateData;
        public nint pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public nint pSignatureSettings;
    }

    [DllImport("wintrust.dll")]
    static extern int WinVerifyTrust(nint hwnd, Guid* action, WinTrustData* data);
}
