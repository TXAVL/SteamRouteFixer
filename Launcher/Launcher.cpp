#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <wininet.h>
#include <shlobj.h>
#include <string>
#include <vector>
#include <iostream>

#pragma comment(lib, "wininet.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "user32.lib")
#pragma comment(lib, "gdi32.lib")

// Global UI handles
HWND g_hwndSplash = NULL;
HWND g_hwndStatus = NULL;
HWND g_hwndProgress = NULL;

const wchar_t* MEDIAFIRE_PRIMARY_URL = L"https://www.mediafire.com/file/jj8pfqovpm30fw2/dotnet+10.exe/file";
const wchar_t* MICROSOFT_FALLBACK_URL = L"https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe";

bool CheckDotNet10Installed()
{
    // 1. Check SharedFx directory
    wchar_t programFiles[MAX_PATH];
    if (SHGetFolderPathW(NULL, CSIDL_PROGRAM_FILES, NULL, 0, programFiles) == S_OK)
    {
        std::wstring dotnetPath = std::wstring(programFiles) + L"\\dotnet\\shared\\Microsoft.WindowsDesktop.App";
        WIN32_FIND_DATAW ffd;
        HANDLE hFind = FindFirstFileW((dotnetPath + L"\\10.*").c_str(), &ffd);
        if (hFind != INVALID_HANDLE_VALUE)
        {
            FindClose(hFind);
            return true;
        }
    }

    // 2. Check Registry
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, L"SOFTWARE\\dotnet\\Setup\\InstalledVersions\\x64\\sharedfx\\Microsoft.WindowsDesktop.App", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        wchar_t subkeyName[256];
        DWORD index = 0;
        while (RegEnumKeyW(hKey, index++, subkeyName, 256) == ERROR_SUCCESS)
        {
            if (wcsncmp(subkeyName, L"10.", 3) == 0)
            {
                RegCloseKey(hKey);
                return true;
            }
        }
        RegCloseKey(hKey);
    }

    return false;
}

std::wstring GetSetupDirectory()
{
    wchar_t localApp[MAX_PATH];
    SHGetFolderPathW(NULL, CSIDL_LOCAL_APPDATA, NULL, 0, localApp);
    std::wstring dir = std::wstring(localApp) + L"\\SteamRouteFixer\\setup";
    CreateDirectoryW((std::wstring(localApp) + L"\\SteamRouteFixer").c_str(), NULL);
    CreateDirectoryW(dir.c_str(), NULL);
    return dir;
}

bool DownloadFile(const wchar_t* url, const wchar_t* destPath)
{
    HINTERNET hInternet = InternetOpenW(L"SteamLauncher-Bootstrapper/1.0", INTERNET_OPEN_TYPE_PRECONFIG, NULL, NULL, 0);
    if (!hInternet) return false;

    HINTERNET hUrl = InternetOpenUrlW(hInternet, url, NULL, 0, INTERNET_FLAG_RELOAD | INTERNET_FLAG_PRAGMA_NOCACHE | INTERNET_FLAG_NO_CACHE_WRITE, 0);
    if (!hUrl)
    {
        InternetCloseHandle(hInternet);
        return false;
    }

    HANDLE hFile = CreateFileW(destPath, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
    if (hFile == INVALID_HANDLE_VALUE)
    {
        InternetCloseHandle(hUrl);
        InternetCloseHandle(hInternet);
        return false;
    }

    char buffer[8192];
    DWORD bytesRead = 0;
    DWORD bytesWritten = 0;
    bool success = true;

    while (InternetReadFile(hUrl, buffer, sizeof(buffer), &bytesRead) && bytesRead > 0)
    {
        if (!WriteFile(hFile, buffer, bytesRead, &bytesWritten, NULL))
        {
            success = false;
            break;
        }
    }

    CloseHandle(hFile);
    InternetCloseHandle(hUrl);
    InternetCloseHandle(hInternet);

    return success;
}

void LaunchMainApp()
{
    wchar_t exePath[MAX_PATH];
    GetModuleFileNameW(NULL, exePath, MAX_PATH);
    std::wstring currentDir = exePath;
    size_t lastSlash = currentDir.find_last_of(L"\\/");
    if (lastSlash != std::wstring::npos)
    {
        currentDir = currentDir.substr(0, lastSlash);
    }

    std::wstring mainApp = currentDir + L"\\SteamRouteFixer.exe";
    ShellExecuteW(NULL, L"open", mainApp.c_str(), NULL, currentDir.c_str(), SW_SHOWNORMAL);
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hwnd, &ps);
        RECT rc;
        GetClientRect(hwnd, &rc);

        // Dark background
        HBRUSH hbg = CreateSolidBrush(RGB(23, 26, 33));
        FillRect(hdc, &rc, hbg);
        DeleteObject(hbg);

        // Title text
        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, RGB(102, 192, 244));
        HFONT hFont = CreateFontW(20, 0, 0, 0, FW_BOLD, FALSE, FALSE, FALSE, DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_DONTCARE, L"Segoe UI");
        HFONT oldFont = (HFONT)SelectObject(hdc, hFont);

        RECT rcTitle = { 20, 20, rc.right - 20, 60 };
        DrawTextW(hdc, L"🎮 STEAM ROUTE FIXER", -1, &rcTitle, DT_LEFT | DT_SINGLELINE);

        SelectObject(hdc, oldFont);
        DeleteObject(hFont);

        EndPaint(hwnd, &ps);
        return 0;
    }
    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }
    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPSTR lpCmdLine, int nCmdShow)
{
    // 1. Check if .NET 10 is already available
    if (CheckDotNet10Installed())
    {
        LaunchMainApp();
        return 0;
    }

    // 2. Register Splash Window
    WNDCLASSEXW wc = { sizeof(WNDCLASSEXW), CS_HREDRAW | CS_VREDRAW, WndProc, 0, 0, hInstance, NULL, LoadCursor(NULL, IDC_ARROW), NULL, NULL, L"SteamLauncherSplash", NULL };
    RegisterClassExW(&wc);

    int width = 450;
    int height = 180;
    int x = (GetSystemMetrics(SM_CXSCREEN) - width) / 2;
    int y = (GetSystemMetrics(SM_CYSCREEN) - height) / 2;

    g_hwndSplash = CreateWindowExW(WS_EX_TOPMOST, L"SteamLauncherSplash", L"Steam Route Fixer Launcher", WS_POPUP | WS_VISIBLE, x, y, width, height, NULL, NULL, hInstance, NULL);

    g_hwndStatus = CreateWindowExW(0, L"STATIC", L"Máy tính chưa cài .NET 10 Desktop Runtime.\nĐang tự động tải bộ cài đặt từ MediaFire...", WS_CHILD | WS_VISIBLE | SS_LEFT, 20, 65, 410, 45, g_hwndSplash, NULL, hInstance, NULL);

    UpdateWindow(g_hwndSplash);

    // 3. Download .NET 10 Installer in background
    std::wstring setupDir = GetSetupDirectory();
    std::wstring installerPath = setupDir + L"\\dotnet10_installer.exe";

    bool dlOk = DownloadFile(MEDIAFIRE_PRIMARY_URL, installerPath.c_str());
    if (!dlOk)
    {
        SetWindowTextW(g_hwndStatus, L"Đang thử tải từ máy chủ dự phòng Microsoft...");
        dlOk = DownloadFile(MICROSOFT_FALLBACK_URL, installerPath.c_str());
    }

    if (dlOk)
    {
        SetWindowTextW(g_hwndStatus, L"Đang tiến hành cài đặt .NET 10 Desktop Runtime...");
        SHELLEXECUTEINFOW sei = { sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS;
        sei.lpVerb = L"open";
        sei.lpFile = installerPath.c_str();
        sei.lpParameters = L"/install /quiet /norestart";
        sei.nShow = SW_SHOWNORMAL;

        if (ShellExecuteExW(&sei) && sei.hProcess)
        {
            WaitForSingleObject(sei.hProcess, INFINITE);
            CloseHandle(sei.hProcess);
        }
    }
    else
    {
        MessageBoxW(g_hwndSplash, L"Không thể tự động tải .NET 10. Vui lòng kiểm tra kết nối mạng và cài đặt thủ công.", L"Thông báo", MB_OK | MB_ICONERROR);
    }

    DestroyWindow(g_hwndSplash);
    LaunchMainApp();
    return 0;
}
