#include "framework.h"

void* global_data = nullptr;

extern "C"
{
	__declspec(dllexport) void Store(void* data)
	{
		global_data = data;
	}

	__declspec(dllexport) void* Load()
	{
		return global_data;
	}
}