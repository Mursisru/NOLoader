#pragma once

#include <windows.h>

namespace noloader
{
    HMODULE RealWinHttp();
    void InstallHooks(HMODULE selfModule);
}
