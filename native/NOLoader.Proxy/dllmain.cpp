#include "bootstrap.h"

#include <string>
#include <windows.h>
#include <winhttp.h>

namespace
{
    template<typename T>
    T ResolveReal(const char* name)
    {
        return reinterpret_cast<T>(GetProcAddress(noloader::RealWinHttp(), name));
    }
}

extern "C" __declspec(dllexport) HINTERNET __stdcall WinHttpOpen(
    LPCWSTR pszAgentW,
    DWORD dwAccessType,
    LPCWSTR pszProxyW,
    LPCWSTR pszProxyBypassW,
    DWORD dwFlags)
{
    auto fn = ResolveReal<decltype(&WinHttpOpen)>("WinHttpOpen");
    return fn ? fn(pszAgentW, dwAccessType, pszProxyW, pszProxyBypassW, dwFlags) : nullptr;
}

extern "C" __declspec(dllexport) BOOL __stdcall WinHttpCloseHandle(HINTERNET hInternet)
{
    auto fn = ResolveReal<decltype(&WinHttpCloseHandle)>("WinHttpCloseHandle");
    return fn ? fn(hInternet) : FALSE;
}

extern "C" __declspec(dllexport) BOOL __stdcall WinHttpGetProxyForUrl(
    HINTERNET hSession,
    LPCWSTR lpcwszUrl,
    WINHTTP_AUTOPROXY_OPTIONS* pAutoProxyOptions,
    WINHTTP_PROXY_INFO* pProxyInfo)
{
    auto fn = ResolveReal<decltype(&WinHttpGetProxyForUrl)>("WinHttpGetProxyForUrl");
    return fn ? fn(hSession, lpcwszUrl, pAutoProxyOptions, pProxyInfo) : FALSE;
}

extern "C" __declspec(dllexport) BOOL __stdcall WinHttpGetIEProxyConfigForCurrentUser(
    WINHTTP_CURRENT_USER_IE_PROXY_CONFIG* pProxyConfig)
{
    auto fn = ResolveReal<decltype(&WinHttpGetIEProxyConfigForCurrentUser)>("WinHttpGetIEProxyConfigForCurrentUser");
    return fn ? fn(pProxyConfig) : FALSE;
}
