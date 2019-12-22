#include "framework.h"

using char_t = wchar_t;
using string_t = std::basic_string<char_t>;

const size_t max_path = 1024;
char_t path_buffer[max_path];

DWORD WINAPI init(LPVOID lpParameter);
string_t get_directory_path(string_t path);
string_t get_file_name(string_t path);
string_t create_temporary_runtimeconfig_json(string_t version);

typedef void(__stdcall* entrypoint_fn)(void*, int);

entrypoint_fn get_entrypoint(HMODULE hModule);


BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
	{
		char_t path_buffer[max_path];
		GetModuleFileNameW(hModule, path_buffer, max_path);
		string_t mogu_native_dll_path = path_buffer;
		

		DWORD thread_id;
		const auto handle = CreateThread(nullptr, 0, init, hModule, 0, &thread_id);
		
		if (handle != nullptr)
		{
			// not wait invoke managed code
			CloseHandle(handle);
		}

		return TRUE;
	}
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

DWORD WINAPI init(LPVOID lpParameter)
{
	const auto hModule = static_cast<HMODULE>(lpParameter);
	
	const auto entrypoint = get_entrypoint(hModule);
	if (entrypoint == nullptr)
	{
		FreeLibraryAndExitThread(hModule, -999);
		return -999;
	}

	// invoke managed code(Mogu.Injector.InjectedEntryPoint)
	entrypoint(nullptr, 0);

	FreeLibraryAndExitThread(hModule, 0);
	return 0;
}

string_t get_directory_path(string_t path)
{
	const auto last_index = path.rfind(L'\\');
	return path.substr(0, last_index);
}

string_t get_file_name(string_t path)
{
	auto last_index = path.rfind(L'\\');
	if (last_index + 1 == path.length())
	{
		last_index = path.rfind(L'\\', last_index - 1);
	}

	return path.substr(last_index + 1);
}

string_t create_temporary_runtimeconfig_json(string_t version)
{
	std::wstringstream ss;

	ss << LR"({
  "runtimeOptions": {
    "framework": {
      "name": "Microsoft.WindowsDesktop.App",
      "version": ")" << version << LR"("
    }
  }
})";

	const auto runtimeconfig = ss.str();
	GetTempPathW(max_path, path_buffer);
	const auto file_name = std::wstring(L"mogu_") + std::to_wstring(GetProcessId(GetCurrentProcess())) + L"_" + std::to_wstring(GetTickCount64()) + L".runtimeconfig.json";
	const auto runtimeconfig_path = std::wstring(path_buffer) + L"\\" + file_name;
	std::wofstream ofs(runtimeconfig_path);
	ofs << runtimeconfig;
	ofs.close();

	return runtimeconfig_path;
}

entrypoint_fn get_entrypoint(HMODULE hModule)
{
	GetModuleFileNameW(hModule, path_buffer, max_path);
	string_t mogu_native_dll_path = path_buffer;
	const auto mogu_directory = get_directory_path(mogu_native_dll_path);
	const auto mogu_managed_dll_path = mogu_directory + L"\\Mogu.dll";

#if defined(_WIN64)
	const auto mogu_store_name = L"MoguStore_x64.dll";
	const auto nethost_name = L"nethost_x64.dll";
#else
	const auto mogu_store_name = L"MoguStore_x86.dll";
	const auto nethost_name = L"nethost_x86.dll";
#endif

	const auto mogu_store_path = mogu_directory + L"\\" + mogu_store_name;
	const auto nethost_path = mogu_directory + L"\\" + nethost_name;

	const auto store_lib = LoadLibraryW(mogu_store_path.c_str());
	if (store_lib == nullptr)
	{
		return nullptr;
	}
	const auto store = reinterpret_cast<void(*)(void*)>(GetProcAddress(store_lib, "Store"));
	const auto load = reinterpret_cast<void*(*)()>(GetProcAddress(store_lib, "Load"));

	auto cached = load();
	if (cached != nullptr)
	{
		FreeLibrary(store_lib);
		return reinterpret_cast<entrypoint_fn>(cached);
	}


	const auto nethost_lib = LoadLibraryW(nethost_path.c_str());
	if (nethost_lib == nullptr)
	{
		return nullptr;
	}
	const auto get_hostfxr_path = reinterpret_cast<int(__stdcall*)(char_t*, size_t*, const void*)>(GetProcAddress(nethost_lib, "get_hostfxr_path"));

	size_t size = max_path;
	get_hostfxr_path(path_buffer, &size, nullptr);
	const string_t hostfxr_path = path_buffer;

	FreeLibrary(nethost_lib);

	auto hostfxr_lib = LoadLibraryW(hostfxr_path.c_str());
	if (hostfxr_lib == nullptr)
	{
		return nullptr;
	}

	// for example, fxr_path is 'DOTNET_ROOT/host/3.0.0/hostfxr.dll'.
	// get framework version from fxr_path(3.0.0);
	const auto version = get_file_name(get_directory_path(path_buffer));
	const auto runtimeconfig_path = create_temporary_runtimeconfig_json(version);


	// get proc address from hostfxr.dll
	auto hostfxr_init = (hostfxr_initialize_for_runtime_config_fn)GetProcAddress(hostfxr_lib, "hostfxr_initialize_for_runtime_config");
	auto hostfxr_get_delegate = (hostfxr_get_runtime_delegate_fn)GetProcAddress(hostfxr_lib, "hostfxr_get_runtime_delegate");
	auto hostfxr_close = (hostfxr_close_fn)GetProcAddress(hostfxr_lib, "hostfxr_close");

	// create context
	hostfxr_handle context = nullptr;
	if (hostfxr_init(runtimeconfig_path.c_str(), nullptr, &context) != 0)
	{
		FreeLibraryAndExitThread(hModule, -2);
		return nullptr;
	}

	// get runtime delegate function pointer proc address
	load_assembly_and_get_function_pointer_fn load_assembly_and_get_function_pointer = nullptr;
	if (hostfxr_get_delegate(context, hdt_load_assembly_and_get_function_pointer, reinterpret_cast<void**>(&load_assembly_and_get_function_pointer)) != 0)
	{
		FreeLibraryAndExitThread(hModule, -3);
		return nullptr;
	}

	// get managed entrypoint
	entrypoint_fn entrypoint;
	const auto type_name = L"Mogu.Injector, Mogu";
	const auto method_name = L"InjectedEntryPoint";
	if (load_assembly_and_get_function_pointer(mogu_managed_dll_path.c_str(), type_name, method_name, nullptr, nullptr, (void**)&entrypoint) != 0)
	{
		FreeLibraryAndExitThread(hModule, -4);
		return nullptr;
	}

	store(entrypoint);

	return entrypoint;
}
