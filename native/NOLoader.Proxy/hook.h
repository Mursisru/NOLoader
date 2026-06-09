#pragma once

#include <windows.h>
#include <cstring>

#define RVA2PTR(t, base, rva) ((t)(((PCHAR)(base)) + (rva)))

inline bool IatHookByName(void* dll, const char* targetDll, const char* importName, void* detourFunction)
{
    auto* mz = reinterpret_cast<IMAGE_DOS_HEADER*>(dll);
    if (!dll || mz->e_magic != IMAGE_DOS_SIGNATURE)
        return false;

    auto* nt = RVA2PTR(IMAGE_NT_HEADERS*, mz, mz->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE)
        return false;

    auto* imports = RVA2PTR(IMAGE_IMPORT_DESCRIPTOR*, mz,
        nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress);
    if (!imports)
        return false;

    for (; imports->Name; ++imports)
    {
        char* name = RVA2PTR(char*, mz, imports->Name);
        if (_stricmp(name, targetDll) != 0)
            continue;

        auto* origThunk = RVA2PTR(IMAGE_THUNK_DATA*, mz, imports->OriginalFirstThunk);
        auto* iat = RVA2PTR(IMAGE_THUNK_DATA*, mz, imports->FirstThunk);
        if (!origThunk)
            origThunk = iat;

        for (; origThunk->u1.AddressOfData; ++origThunk, ++iat)
        {
            if (IMAGE_SNAP_BY_ORDINAL(origThunk->u1.Ordinal))
                continue;

            auto* import = RVA2PTR(IMAGE_IMPORT_BY_NAME*, mz, origThunk->u1.AddressOfData);
            if (strcmp(import->Name, importName) != 0)
                continue;

            DWORD oldProtect = 0;
            if (!VirtualProtect(&iat->u1.Function, sizeof(void*), PAGE_READWRITE, &oldProtect))
                return false;

            iat->u1.Function = reinterpret_cast<ULONG_PTR>(detourFunction);
            VirtualProtect(&iat->u1.Function, sizeof(void*), oldProtect, &oldProtect);
            return true;
        }
    }

    return false;
}

inline bool IatHookGetProcAddress(void* dll, void* detourFunction)
{
    return IatHookByName(dll, "KERNEL32.dll", "GetProcAddress", detourFunction)
        || IatHookByName(dll, "KERNELBASE.dll", "GetProcAddress", detourFunction);
}
