#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdio.h>
#include <stdint.h>
#include <string.h>

typedef void* MonoDomain;
typedef void* MonoAssembly;
typedef void* MonoImage;
typedef void* MonoClass;
typedef void* MonoObject;
typedef void* MonoString;
typedef void* MonoMethod;
typedef void* MonoClassField;
typedef void* MonoArray;
typedef void* MonoLivenessState;

typedef void (*register_object_callback)(void** arr, int size, void* userdata);
typedef void (*WorldStateChanged)(void);

typedef MonoDomain (*fn_get_root_domain)(void);
typedef MonoDomain (*fn_thread_attach)(MonoDomain);
typedef void (*fn_assembly_foreach)(void (*func)(MonoAssembly, void*), void*);
typedef MonoImage (*fn_assembly_get_image)(MonoAssembly);
typedef const char* (*fn_image_get_name)(MonoImage);
typedef MonoClass (*fn_class_from_name)(MonoImage, const char*, const char*);
typedef const char* (*fn_class_get_name)(MonoClass);
typedef MonoClass (*fn_class_get_parent)(MonoClass);
typedef MonoClassField (*fn_class_get_field_from_name)(MonoClass, const char*);
typedef MonoMethod (*fn_class_get_method_from_name)(MonoClass, const char*, int);
typedef MonoClass (*fn_object_get_class)(MonoObject);
typedef void (*fn_field_get_value)(MonoObject, MonoClassField, void*);
typedef MonoObject (*fn_runtime_invoke)(MonoMethod, void*, void**, MonoObject*);
typedef char* (*fn_string_to_utf8)(MonoString);
typedef void (*fn_mono_free)(void*);
typedef uintptr_t (*fn_array_length)(MonoArray);
typedef void* (*fn_array_addr)(MonoArray, int, uintptr_t);
typedef MonoLivenessState (*fn_liveness_alloc)(MonoClass, unsigned int, register_object_callback, void*, WorldStateChanged, WorldStateChanged);
typedef void (*fn_liveness_statics)(MonoLivenessState);
typedef void (*fn_liveness_finalize)(MonoLivenessState);
typedef void (*fn_liveness_free)(MonoLivenessState);
typedef void* MonoType;
typedef MonoType (*fn_class_get_type)(MonoClass);
typedef MonoObject (*fn_type_get_object)(MonoDomain, MonoType);

static fn_get_root_domain p_get_root_domain;
static fn_thread_attach p_thread_attach;
static fn_assembly_foreach p_assembly_foreach;
static fn_assembly_get_image p_assembly_get_image;
static fn_image_get_name p_image_get_name;
static fn_class_from_name p_class_from_name;
static fn_class_get_name p_class_get_name;
static fn_class_get_parent p_class_get_parent;
static fn_class_get_field_from_name p_class_get_field_from_name;
static fn_class_get_method_from_name p_class_get_method_from_name;
static fn_object_get_class p_object_get_class;
static fn_field_get_value p_field_get_value;
static fn_runtime_invoke p_runtime_invoke;
static fn_string_to_utf8 p_string_to_utf8;
static fn_mono_free p_mono_free;
static fn_array_length p_array_length;
static fn_array_addr p_array_addr;
static fn_class_get_type p_class_get_type;
static fn_type_get_object p_type_get_object;

static FILE* logFile;
static MonoImage unityImage;
static MonoClass zoneClass;
static MonoClassField linksField;
static MonoMethod nameMethod;
static void* foundObjects[4096];
static int foundCount;
static int dumpDone;

static const wchar_t* kOutPath = L"E:\\.Proyects\\Unity\\ProjectBlackSun\\ZoneTouches.txt";
static const wchar_t* kLogPath = L"E:\\.Proyects\\Unity\\ProjectBlackSun\\ZoneTouches-log.txt";

static void Log(const char* message)
{
    if (!logFile) logFile = _wfopen(kLogPath, L"w");
    if (!logFile) return;
    fputs(message, logFile);
    fputc('\n', logFile);
    fflush(logFile);
}

static int LoadMono(void)
{
    HMODULE mono = GetModuleHandleA("mono-2.0-bdwgc.dll");
    if (!mono)
    {
        Log("mono-2.0-bdwgc.dll is not loaded");
        return 0;
    }

#define LOAD(name, var) var = (name)GetProcAddress(mono, #var); if (!var) { Log("missing " #var); return 0; }
    p_get_root_domain = (fn_get_root_domain)GetProcAddress(mono, "mono_get_root_domain");
    p_thread_attach = (fn_thread_attach)GetProcAddress(mono, "mono_thread_attach");
    p_assembly_foreach = (fn_assembly_foreach)GetProcAddress(mono, "mono_assembly_foreach");
    p_assembly_get_image = (fn_assembly_get_image)GetProcAddress(mono, "mono_assembly_get_image");
    p_image_get_name = (fn_image_get_name)GetProcAddress(mono, "mono_image_get_name");
    p_class_from_name = (fn_class_from_name)GetProcAddress(mono, "mono_class_from_name");
    p_class_get_name = (fn_class_get_name)GetProcAddress(mono, "mono_class_get_name");
    p_class_get_parent = (fn_class_get_parent)GetProcAddress(mono, "mono_class_get_parent");
    p_class_get_field_from_name = (fn_class_get_field_from_name)GetProcAddress(mono, "mono_class_get_field_from_name");
    p_class_get_method_from_name = (fn_class_get_method_from_name)GetProcAddress(mono, "mono_class_get_method_from_name");
    p_object_get_class = (fn_object_get_class)GetProcAddress(mono, "mono_object_get_class");
    p_field_get_value = (fn_field_get_value)GetProcAddress(mono, "mono_field_get_value");
    p_runtime_invoke = (fn_runtime_invoke)GetProcAddress(mono, "mono_runtime_invoke");
    p_string_to_utf8 = (fn_string_to_utf8)GetProcAddress(mono, "mono_string_to_utf8");
    p_mono_free = (fn_mono_free)GetProcAddress(mono, "mono_free");
    p_array_length = (fn_array_length)GetProcAddress(mono, "mono_array_length");
    p_array_addr = (fn_array_addr)GetProcAddress(mono, "mono_array_addr_with_size");
    p_class_get_type = (fn_class_get_type)GetProcAddress(mono, "mono_class_get_type");
    p_type_get_object = (fn_type_get_object)GetProcAddress(mono, "mono_type_get_object");
#undef LOAD

    if (!p_get_root_domain || !p_thread_attach || !p_assembly_foreach || !p_class_from_name || !p_type_get_object)
    {
        Log("mono exports missing");
        return 0;
    }

    Log("mono exports loaded");
    return 1;
}

static void OnAssembly(MonoAssembly assembly, void* unused)
{
    const char* imageName;
    MonoImage image;
    MonoClass klass;
    (void)unused;
    image = p_assembly_get_image(assembly);
    if (!image) return;
    imageName = p_image_get_name(image);
    if (!imageName) return;
    if (strstr(imageName, "UnityEngine.CoreModule")) unityImage = image;
    if (!strstr(imageName, "Assembly-CSharp") || strstr(imageName, "Editor")) return;
    klass = p_class_from_name(image, "", "DistrictZone");
    if (klass) zoneClass = klass;
}

static void OnLiveness(void** arr, int size, void* unused)
{
    int i;
    char note[64];
    (void)unused;
    sprintf(note, "callback size %d", size);
    Log(note);
    if (!arr || size <= 0 || size > 100000) return;
    for (i = 0; i < size && foundCount < 4096; i++)
    {
        if (arr[i]) foundObjects[foundCount++] = arr[i];
    }
}

static void WorldNop(void)
{
}

static int CopyName(MonoObject obj, char* buffer, int bufferSize)
{
    MonoObject exc = NULL;
    MonoString text;
    char* utf8;
    MonoClass klass;
    MonoMethod method = nameMethod;

    if (!method)
    {
        klass = p_object_get_class(obj);
        while (klass && !method)
        {
            method = p_class_get_method_from_name(klass, "get_name", 0);
            klass = p_class_get_parent(klass);
        }
        nameMethod = method;
    }

    if (!method)
    {
        strncpy(buffer, "?", bufferSize - 1);
        buffer[bufferSize - 1] = 0;
        return 0;
    }

    text = (MonoString)p_runtime_invoke(method, obj, NULL, &exc);
    if (exc || !text)
    {
        strncpy(buffer, "?", bufferSize - 1);
        buffer[bufferSize - 1] = 0;
        return 0;
    }

    utf8 = p_string_to_utf8(text);
    if (!utf8)
    {
        strncpy(buffer, "?", bufferSize - 1);
        buffer[bufferSize - 1] = 0;
        return 0;
    }

    strncpy(buffer, utf8, bufferSize - 1);
    buffer[bufferSize - 1] = 0;
    p_mono_free(utf8);
    return 1;
}

static int InvokeFind(MonoDomain domain, MonoMethod find, int paramCount)
{
    MonoObject typeObj;
    MonoObject exc = NULL;
    MonoArray result;
    void* args[3];
    int inactive = 0;
    int sort = 0;
    uintptr_t count;
    uintptr_t index;

    typeObj = p_type_get_object(domain, p_class_get_type(zoneClass));
    args[0] = &typeObj;
    args[1] = &inactive;
    args[2] = &sort;
    result = (MonoArray)p_runtime_invoke(find, NULL, args, &exc);
    (void)paramCount;
    if (exc || !result) return 0;

    count = p_array_length(result);
    foundCount = 0;
    for (index = 0; index < count && foundCount < 4096; index++)
    {
        MonoObject* slot = (MonoObject*)p_array_addr(result, (int)sizeof(void*), index);
        if (slot && *slot) foundObjects[foundCount++] = *slot;
    }

    return foundCount > 0;
}

static int FindZones(MonoDomain domain)
{
    MonoClass objectClass;
    MonoMethod find;

    Log("enter find");
    if (!unityImage || !p_class_get_type || !p_type_get_object)
    {
        Log("unity image missing");
        return 0;
    }

    objectClass = p_class_from_name(unityImage, "UnityEngine", "Object");
    if (!objectClass)
    {
        Log("UnityEngine.Object not found");
        return 0;
    }
    Log("object class");

    find = p_class_get_method_from_name(objectClass, "FindObjectsOfType", 1);
    if (find)
    {
        Log("invoke FindObjectsOfType");
        __try
        {
            if (InvokeFind(domain, find, 1)) return 1;
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            Log("FindObjectsOfType threw");
        }
    }

    find = p_class_get_method_from_name(objectClass, "FindObjectsByType", 3);
    if (!find)
    {
        Log("find method not found");
        return 0;
    }

    Log("invoke FindObjectsByType");
    __try
    {
        return InvokeFind(domain, find, 3);
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        Log("FindObjectsByType threw");
        return 0;
    }
}

static void Dump(void)
{
    MonoDomain domain;
    FILE* out;
    int i;
    int linkCount = 0;

    Log("dump start");
    if (!LoadMono()) return;

    domain = p_get_root_domain();
    if (!domain)
    {
        Log("no root domain");
        return;
    }

    p_thread_attach(domain);
    Log("thread attached");
    p_assembly_foreach(OnAssembly, NULL);
    if (!zoneClass)
    {
        Log("DistrictZone class not found");
        return;
    }

    linksField = p_class_get_field_from_name(zoneClass, "travelLinks");
    if (!linksField)
    {
        Log("travelLinks field not found");
        return;
    }

    Log("finding zones");
    foundCount = 0;
    __try
    {
        if (!FindZones(domain)) return;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        Log("find zones threw");
        return;
    }
    Log("zones found");

    out = _wfopen(kOutPath, L"w");
    if (!out)
    {
        Log("could not open output");
        return;
    }

    fprintf(out, "# zone touches captured from the running editor\n");
    fprintf(out, "# from|to|gap|kind\n");

    for (i = 0; i < foundCount; i++)
    {
        MonoObject zone = (MonoObject)foundObjects[i];
        MonoObject listObj = NULL;
        MonoClass listClass;
        MonoClassField sizeField;
        MonoClassField itemsField;
        MonoArray items = NULL;
        int size = 0;
        int n;
        char fromName[256];

        if (!zone) continue;
        CopyName(zone, fromName, sizeof(fromName));
        p_field_get_value(zone, linksField, &listObj);
        if (!listObj) continue;

        listClass = p_object_get_class(listObj);
        sizeField = p_class_get_field_from_name(listClass, "_size");
        itemsField = p_class_get_field_from_name(listClass, "_items");
        if (!sizeField || !itemsField) continue;

        p_field_get_value(listObj, sizeField, &size);
        p_field_get_value(listObj, itemsField, &items);
        if (!items || size <= 0) continue;

        for (n = 0; n < size; n++)
        {
            MonoObject* linkSlot = (MonoObject*)p_array_addr(items, (int)sizeof(void*), (uintptr_t)n);
            MonoObject link;
            MonoClass linkClass;
            MonoClassField otherField;
            MonoClassField gapField;
            MonoClassField kindField;
            MonoObject other = NULL;
            float gap = 0.f;
            int kind = 0;
            char toName[256];

            if (!linkSlot || !*linkSlot) continue;
            link = *linkSlot;
            linkClass = p_object_get_class(link);
            otherField = p_class_get_field_from_name(linkClass, "other");
            gapField = p_class_get_field_from_name(linkClass, "gap");
            kindField = p_class_get_field_from_name(linkClass, "kind");
            if (!otherField) continue;

            p_field_get_value(link, otherField, &other);
            if (gapField) p_field_get_value(link, gapField, &gap);
            if (kindField) p_field_get_value(link, kindField, &kind);
            if (!other) continue;

            CopyName(other, toName, sizeof(toName));
            fprintf(out, "%s|%s|%.4f|%d\n", fromName, toName, gap, kind);
            linkCount++;
        }
    }

    fprintf(out, "# zones %d links %d\n", foundCount, linkCount);
    fclose(out);

    if (logFile)
    {
        fprintf(logFile, "wrote %d zones and %d links\n", foundCount, linkCount);
        fflush(logFile);
    }
}

static DWORD mainThreadId;
static HWND mainWindow;
static HHOOK hookHandle;
static HMODULE selfModule;

static LRESULT CALLBACK CallWndHook(int code, WPARAM wParam, LPARAM lParam)
{
    if (code == HC_ACTION && !dumpDone)
    {
        dumpDone = 1;
        __try
        {
            Dump();
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            Log("dump threw an exception");
        }
        UnhookWindowsHookEx(hookHandle);
    }

    return CallNextHookEx(hookHandle, code, wParam, lParam);
}

static BOOL CALLBACK FindUnityWindow(HWND hwnd, LPARAM param)
{
    DWORD pid = 0;
    DWORD tid;
    wchar_t title[512];
    (void)param;

    tid = GetWindowThreadProcessId(hwnd, &pid);
    if (pid != GetCurrentProcessId() || !IsWindowVisible(hwnd)) return TRUE;
    title[0] = 0;
    GetWindowTextW(hwnd, title, 512);
    if (wcsstr(title, L"ProjectBlackSun") == NULL) return TRUE;
    mainThreadId = tid;
    mainWindow = hwnd;
    return FALSE;
}

__declspec(dllexport) DWORD WINAPI Start(void* unused)
{
    char note[128];
    (void)unused;
    Log("start");
    mainThreadId = 0;
    mainWindow = NULL;
    EnumWindows(FindUnityWindow, 0);
    if (!mainThreadId || !mainWindow)
    {
        Log("unity window not found");
        return 1;
    }

    sprintf(note, "window thread %lu", mainThreadId);
    Log(note);
    hookHandle = SetWindowsHookExW(WH_CALLWNDPROC, CallWndHook, selfModule, mainThreadId);
    if (!hookHandle)
    {
        Log("hook failed");
        return 1;
    }

    SendMessageW(mainWindow, WM_NULL, 0, 0);
    Log(dumpDone ? "hook ran" : "hook did not run");
    return dumpDone ? 0 : 2;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;
    if (reason == DLL_PROCESS_ATTACH)
    {
        selfModule = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}
