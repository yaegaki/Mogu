#pragma once

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

extern "C"
{
	__declspec(dllexport) void Store(void* data);
	__declspec(dllexport) void* Load();
}