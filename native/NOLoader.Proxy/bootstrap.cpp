#include "bootstrap.h"

#include "hook.h"



#include <fstream>

#include <mutex>

#include <string>

#include <vector>

#include <windows.h>



namespace

{

    HMODULE g_realWinHttp = nullptr;

    std::wstring g_gameRoot;

    std::wstring g_targetAssembly;

    std::once_flag g_hookOnce;

    bool g_monoFuncsLoaded = false;

    bool g_iatHookInstalled = false;



    using mono_jit_init_version_t = void* (__cdecl*)(const char*, const char*);

    using mono_domain_assembly_open_t = void* (__cdecl*)(void*, const char*);

    using mono_assembly_get_image_t = void* (__cdecl*)(void*);

    using mono_class_from_name_t = void* (__cdecl*)(void*, const char*, const char*);

    using mono_class_get_method_from_name_t = void* (__cdecl*)(void*, const char*, int);

    using mono_runtime_invoke_t = void* (__cdecl*)(void*, void*, void**, void**);

    using mono_string_new_t = void* (__cdecl*)(void*, const char*);

    using mono_thread_current_t = void* (__cdecl*)();

    using mono_thread_set_main_t = void (__cdecl*)(void*);

    using mono_set_assemblies_path_t = void (__cdecl*)(const char*);

    using mono_config_parse_t = void (__cdecl*)(const char*);

    using mono_domain_set_config_t = void (__cdecl*)(void*, const char*, const char*);



    mono_jit_init_version_t g_mono_jit_init_version = nullptr;

    mono_domain_assembly_open_t g_mono_domain_assembly_open = nullptr;

    mono_assembly_get_image_t g_mono_assembly_get_image = nullptr;

    mono_class_from_name_t g_mono_class_from_name = nullptr;

    mono_class_get_method_from_name_t g_mono_class_get_method_from_name = nullptr;

    mono_runtime_invoke_t g_mono_runtime_invoke = nullptr;

    mono_string_new_t g_mono_string_new = nullptr;

    mono_thread_current_t g_mono_thread_current = nullptr;

    mono_thread_set_main_t g_mono_thread_set_main = nullptr;

    mono_set_assemblies_path_t g_mono_set_assemblies_path = nullptr;

    mono_config_parse_t g_mono_config_parse = nullptr;

    mono_domain_set_config_t g_mono_domain_set_config = nullptr;



    using GetProcAddress_t = FARPROC(WINAPI*)(HMODULE, LPCSTR);

    GetProcAddress_t g_realGetProcAddress = nullptr;



    std::wstring GetModuleDirectory(HMODULE module)

    {

        wchar_t path[MAX_PATH]{};

        GetModuleFileNameW(module, path, MAX_PATH);

        std::wstring full(path);

        const auto pos = full.find_last_of(L"\\/");

        return pos == std::wstring::npos ? L"." : full.substr(0, pos);

    }



    std::string WideToUtf8(const std::wstring& value)

    {

        if (value.empty())

            return {};

        const int size = WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, nullptr, 0, nullptr, nullptr);

        if (size <= 0)

            return {};

        std::string result(static_cast<size_t>(size - 1), '\0');

        WideCharToMultiByte(CP_UTF8, 0, value.c_str(), -1, result.data(), size, nullptr, nullptr);

        return result;

    }



    void LogLine(const char* message)

    {

        if (g_gameRoot.empty())

            return;

        std::wstring logPath = g_gameRoot + L"\\NOLoader\\logs\\proxy.log";

        CreateDirectoryW((g_gameRoot + L"\\NOLoader\\logs").c_str(), nullptr);

        std::ofstream out(WideToUtf8(logPath), std::ios::app);

        if (out.is_open())

            out << message << '\n';

    }



    std::wstring ReadConfigValue(const std::wstring& configPath, const std::wstring& key)

    {

        std::wifstream in(configPath);

        if (!in.is_open())

            return L"";



        std::wstring line;

        while (std::getline(in, line))

        {

            if (line.empty() || line[0] == L'[')

                continue;

            if (line.rfind(key + L"=", 0) == 0)

                return line.substr(key.size() + 1);

        }

        return L"";

    }



    void LoadConfig()

    {

        const std::wstring configPath = g_gameRoot + L"\\noloader_config.ini";

        g_targetAssembly = ReadConfigValue(configPath, L"target_assembly");

        if (g_targetAssembly.empty())

            g_targetAssembly = L"NOLoader\\core\\NOLoader.Core.dll";

    }



    void LoadMonoFunctions(HMODULE monoModule)

    {

        if (!g_realGetProcAddress || !monoModule)

            return;



#define RESOLVE(name) g_##name = reinterpret_cast<decltype(g_##name)>(g_realGetProcAddress(monoModule, #name))



        RESOLVE(mono_jit_init_version);

        RESOLVE(mono_domain_assembly_open);

        RESOLVE(mono_assembly_get_image);

        RESOLVE(mono_class_from_name);

        RESOLVE(mono_class_get_method_from_name);

        RESOLVE(mono_runtime_invoke);

        RESOLVE(mono_string_new);

        RESOLVE(mono_thread_current);

        RESOLVE(mono_thread_set_main);

        RESOLVE(mono_set_assemblies_path);

        RESOLVE(mono_config_parse);

        RESOLVE(mono_domain_set_config);



#undef RESOLVE



        g_monoFuncsLoaded = g_mono_jit_init_version != nullptr

            && g_mono_domain_assembly_open != nullptr

            && g_mono_assembly_get_image != nullptr

            && g_mono_class_from_name != nullptr

            && g_mono_class_get_method_from_name != nullptr

            && g_mono_runtime_invoke != nullptr

            && g_mono_string_new != nullptr;



        LogLine(g_monoFuncsLoaded ? "Mono functions loaded" : "Failed to load mono functions");

    }



    void SetupMonoPaths()

    {

        if (!g_mono_set_assemblies_path)

            return;



        const std::wstring managed = g_gameRoot + L"\\NuclearOption_Data\\Managed";

        const std::wstring core = g_gameRoot + L"\\NOLoader\\core";

        const std::wstring search = managed + L";" + core;

        g_mono_set_assemblies_path(WideToUtf8(search).c_str());

        LogLine("Mono assembly search path configured");

    }



    void SetupDomainConfig(void* domain)

    {

        if (!g_mono_domain_set_config)

            return;



        const std::wstring configPath = g_gameRoot + L"\\NuclearOption.exe.config";

        g_mono_domain_set_config(domain, WideToUtf8(g_gameRoot).c_str(), WideToUtf8(configPath).c_str());

        LogLine("Mono domain config set");

    }



    void InvokeManagedBootstrap(void* domain)

    {

        if (GetEnvironmentVariableW(L"NOLOADER_INITIALIZED", nullptr, 0) > 0)

        {

            LogLine("Bootstrap already initialized, skipping");

            return;

        }

        SetEnvironmentVariableW(L"NOLOADER_INITIALIZED", L"TRUE");



        if (g_mono_thread_current && g_mono_thread_set_main)

            g_mono_thread_set_main(g_mono_thread_current());



        SetupDomainConfig(domain);



        if (g_mono_config_parse)

            g_mono_config_parse(nullptr);



        const std::wstring asmPath = g_gameRoot + L"\\" + g_targetAssembly;

        const std::string asmPathUtf8 = WideToUtf8(asmPath);

        const std::string gameRootUtf8 = WideToUtf8(g_gameRoot);



        LogLine(("Opening assembly: " + asmPathUtf8).c_str());

        void* assembly = g_mono_domain_assembly_open(domain, asmPathUtf8.c_str());

        if (!assembly)

        {

            LogLine("mono_domain_assembly_open failed");

            return;

        }



        void* image = g_mono_assembly_get_image(assembly);

        void* klass = g_mono_class_from_name(image, "NOLoader.Core", "Bootstrap");

        void* method = g_mono_class_get_method_from_name(klass, "Initialize", 1);

        if (!method)

        {

            LogLine("Bootstrap.Initialize not found");

            return;

        }



        void* arg = g_mono_string_new(domain, gameRootUtf8.c_str());

        void* args[1] = { arg };

        void* exc = nullptr;

        g_mono_runtime_invoke(method, nullptr, args, &exc);

        LogLine(exc ? "Bootstrap.Initialize threw" : "Bootstrap.Initialize completed");

    }



    void* __cdecl InitMonoHook(const char* rootDomainName, const char* runtimeVersion)

    {

        if (!g_mono_jit_init_version)

        {

            LogLine("InitMonoHook without mono_jit_init_version");

            return nullptr;

        }



        LogLine("mono_jit_init_version hook invoked");

        SetupMonoPaths();



        void* domain = g_mono_jit_init_version(rootDomainName, runtimeVersion);

        if (!domain)

        {

            LogLine("Original mono_jit_init_version returned null");

            return nullptr;

        }



        InvokeManagedBootstrap(domain);

        return domain;

    }



    FARPROC WINAPI GetProcAddressDetour(HMODULE module, LPCSTR name)

    {

        if (name && name[0] != '\0' && HIWORD(name) != 0)

        {

            if (strcmp(name, "mono_jit_init_version") == 0 || strcmp(name, "mono_jit_init") == 0)

            {

                LoadMonoFunctions(module);

                if (g_mono_jit_init_version)

                    return reinterpret_cast<FARPROC>(&InitMonoHook);

            }

        }



        return g_realGetProcAddress ? g_realGetProcAddress(module, name) : nullptr;

    }



    bool TryInstallGetProcAddressHook()

    {

        if (g_iatHookInstalled)

            return true;



        HMODULE unityPlayer = GetModuleHandleW(L"UnityPlayer.dll");

        HMODULE appModule = GetModuleHandleW(nullptr);



        bool ok = false;

        if (unityPlayer)

            ok |= IatHookGetProcAddress(unityPlayer, reinterpret_cast<void*>(GetProcAddressDetour));

        if (appModule && appModule != unityPlayer)

            ok |= IatHookGetProcAddress(appModule, reinterpret_cast<void*>(GetProcAddressDetour));



        if (ok)

        {

            g_iatHookInstalled = true;

            LogLine("GetProcAddress IAT hook installed");

        }



        return ok;

    }



    DWORD WINAPI HookRetryThread(LPVOID)

    {

        for (int i = 0; i < 100 && !g_iatHookInstalled; ++i)

        {

            if (TryInstallGetProcAddressHook())

                break;

            Sleep(50);

        }



        if (!g_iatHookInstalled)

            LogLine("GetProcAddress IAT hook failed after retries");



        return 0;

    }



    void InstallGetProcAddressHook()

    {

        if (TryInstallGetProcAddressHook())

            return;



        LogLine("GetProcAddress IAT hook deferred to retry thread");

        HANDLE thread = CreateThread(nullptr, 0, HookRetryThread, nullptr, 0, nullptr);

        if (thread)

            CloseHandle(thread);

    }

}



namespace noloader

{

    HMODULE RealWinHttp()

    {

        if (!g_realWinHttp)

        {

            wchar_t sysDir[MAX_PATH]{};

            GetSystemDirectoryW(sysDir, MAX_PATH);

            g_realWinHttp = LoadLibraryW((std::wstring(sysDir) + L"\\winhttp.dll").c_str());

        }

        return g_realWinHttp;

    }



    void InstallHooks(HMODULE selfModule)

    {

        std::call_once(g_hookOnce, [selfModule]()

        {

            HMODULE kernel32 = GetModuleHandleW(L"kernel32.dll");

            g_realGetProcAddress = reinterpret_cast<GetProcAddress_t>(

                GetProcAddress(kernel32, "GetProcAddress"));



            g_gameRoot = GetModuleDirectory(selfModule);



            wchar_t cwd[MAX_PATH]{};

            GetCurrentDirectoryW(MAX_PATH, cwd);

            if (_wcsicmp(cwd, g_gameRoot.c_str()) != 0)

                SetCurrentDirectoryW(g_gameRoot.c_str());



            LogLine("NOLoader proxy attach");

            LoadConfig();

            InstallGetProcAddressHook();

        });

    }

}



extern "C" BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID)

{

    if (fdwReason == DLL_PROCESS_ATTACH)

    {

        DisableThreadLibraryCalls(hinstDLL);

        noloader::InstallHooks(hinstDLL);

    }

    return TRUE;

}


