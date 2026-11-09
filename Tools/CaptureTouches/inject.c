#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <psapi.h>
#include <stdio.h>
#include <string.h>

static BOOL CALLBACK FindEditorWindow(HWND hwnd, LPARAM param)
{
    wchar_t title[512];
    DWORD pid = 0;
    (void)param;
    if (!IsWindowVisible(hwnd)) return TRUE;
    title[0] = 0;
    GetWindowTextW(hwnd, title, 512);
    if (wcsstr(title, L"ProjectBlackSun") == NULL || wcsstr(title, L"Unity") == NULL) return TRUE;
    GetWindowThreadProcessId(hwnd, &pid);
    *(DWORD*)param = pid;
    return FALSE;
}

static DWORD FindEditorPid(void)
{
    DWORD pid = 0;
    EnumWindows(FindEditorWindow, (LPARAM)&pid);
    return pid;
}

static int RemoteLoad(HANDLE process, const wchar_t* dllPath, HMODULE* remoteBase)
{
    SIZE_T bytes = (wcslen(dllPath) + 1) * sizeof(wchar_t);
    LPVOID remotePath = VirtualAllocEx(process, NULL, bytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    LPTHREAD_START_ROUTINE loadLibrary;
    HANDLE thread;
    DWORD loaded = 0;
    HMODULE modules[512];
    DWORD needed = 0;
    unsigned int i;

    if (!remotePath) return 0;
    if (!WriteProcessMemory(process, remotePath, dllPath, bytes, NULL)) return 0;

    loadLibrary = (LPTHREAD_START_ROUTINE)GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "LoadLibraryW");
    thread = CreateRemoteThread(process, NULL, 0, loadLibrary, remotePath, 0, NULL);
    if (!thread) return 0;
    WaitForSingleObject(thread, 15000);
    GetExitCodeThread(thread, &loaded);
    CloseHandle(thread);
    if (!loaded) return 0;

    if (!EnumProcessModules(process, modules, sizeof(modules), &needed)) return 0;
    *remoteBase = NULL;
    for (i = 0; i < needed / sizeof(HMODULE); i++)
    {
        wchar_t name[MAX_PATH];
        if (!GetModuleFileNameExW(process, modules[i], name, MAX_PATH)) continue;
        if (wcsstr(name, L"touchcap4.dll"))
        {
            *remoteBase = modules[i];
            break;
        }
    }

    return *remoteBase != NULL;
}

int wmain(void)
{
    wchar_t dllPath[MAX_PATH];
    DWORD     pid = FindEditorPid();
    HANDLE process;
    if (!pid)
    {
        wprintf(L"Unity editor window not found\n");
        return 1;
    }
    wprintf(L"Editor pid %lu\n", pid);
    HMODULE local;
    HMODULE remote;
    FARPROC localStart;
    LPTHREAD_START_ROUTINE remoteStart;
    HANDLE thread;
    DWORD exitCode = 0;

    GetFullPathNameW(L"touchcap4.dll", MAX_PATH, dllPath, NULL);
    process = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, FALSE, pid);
    if (!process)
    {
        wprintf(L"OpenProcess failed %lu\n", GetLastError());
        return 1;
    }

    if (!RemoteLoad(process, dllPath, &remote))
    {
        wprintf(L"LoadLibrary remote failed %lu\n", GetLastError());
        CloseHandle(process);
        return 1;
    }

    local = LoadLibraryW(dllPath);
    localStart = GetProcAddress(local, "Start");
    remoteStart = (LPTHREAD_START_ROUTINE)((BYTE*)remote + ((BYTE*)localStart - (BYTE*)local));
    thread = CreateRemoteThread(process, NULL, 0, remoteStart, NULL, 0, NULL);
    if (!thread)
    {
        wprintf(L"Start thread failed %lu\n", GetLastError());
        CloseHandle(process);
        return 1;
    }

    WaitForSingleObject(thread, 15000);
    GetExitCodeThread(thread, &exitCode);
    CloseHandle(thread);
    CloseHandle(process);
    wprintf(L"Start returned %lu\n", exitCode);
    return 0;
}
