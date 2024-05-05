#include <stdio.h>
#include <stdlib.h>
#include <assert.h>
#include <string.h>
#include <dlfcn.h>
#include <limits.h>

#include <iostream>
#include <string>

#include <nethost.h>
#include <coreclr_delegates.h>
#include <hostfxr.h>

#define MAX_PATH 4095
#define DIR_SEPARATOR '/'


// Globals to hold the exports from hostfxr
hostfxr_initialize_for_dotnet_command_line_fn init_for_cmd_line_fptr;
hostfxr_initialize_for_runtime_config_fn init_for_run_config_fptr;
hostfxr_get_runtime_delegate_fn get_delegate_fptr;
hostfxr_run_app_fn run_app_fptr;
hostfxr_close_fn close_fptr;

struct csharp_app_data
{
    const char *path;
    const char *entry_point;
    const char *mnamespace;
    const char *mclass;
    size_t argc;
    const char **args;
};

int run_csharp_app(const char *native_host_path, csharp_app_data *app_data);
bool load_hostfxr(const char *hostfxr_path);
load_assembly_and_get_function_pointer_fn get_dotnet_load_assembly(const char *assembly);
std::string trim_extension(std::string path);
const char *get_name_from_path(const char *path);

void *load_library(const char_t *);
void *get_export(void *, const char *);


// ARGV[0]: This executable's name.
// ARGV[1]: The C# app we want to run.
// ARGV[2]: The C# app entry point.
// ARGV[3]: The C# app namespace.
// ARGV[4]: The C# app class.
// ARGV[5..]: The parameters the C# app might require.
int main(int argc, char *argv[])
{
    // Make sure we got the name of the C# app to run.
    assert(argc >= 5);

    // Get the native host path here (i.e. this current executable).
    char host_path[MAX_PATH];

    // According to the Standard C Library documentation, here "resolved_path"
    // and "host_path" should have the same contents.
    char *resolved_path = realpath(argv[0], host_path);
    if (resolved_path == nullptr)
    {
        std::cout << "\nPath Error: Failed to resolve the path '" << argv[0] << "' :(" << std::endl;
        return -1;
    }

    // We actually want the directory containing this executable, not the full
    // path itself.
    for (int i = strlen(host_path)-1; i > 0; i--)
    {
        if (host_path[i] == DIR_SEPARATOR)
        {
            host_path[i+1] = '\0';
            break;
        }
    }

    csharp_app_data app_data = {
        argv[1],
        argv[2],
        argv[3],
        argv[4],
        (size_t) (argc - 5), // The first five arguments in this program's argv are other stuff.
        argc > 5 ? (const char **) &argv[5] : nullptr,
    };

    int exit_code = run_csharp_app(host_path, &app_data);

    std::cout << "\nC# app '" << app_data.path << "' exit code: " << exit_code << std::endl;
    return exit_code;
}


int run_csharp_app(const char *native_host_path, csharp_app_data *app_data)
{
    // Load the host (hostfxr) to get the exported hosting functions.
    if (!load_hostfxr(nullptr))
    {
        std::cout << "Host Error: Failed to load the host in load_hostfxr() :(" << std::endl;
        return -1;
    }

    std::string app_path(app_data->path);
    std::string app_base_path = trim_extension(app_path);
    std::string app_config_path = app_base_path + ".runtimeconfig.json";

    // Load and initialize the .NET runtime.
    hostfxr_handle context = nullptr;
    void *load_asm_and_get_fn_voidptr = nullptr;

    int rc = init_for_run_config_fptr(app_config_path.c_str(), nullptr, &context);

    if (rc != 0 || context == nullptr)
    {
        std::cout << "Init Error: .NET initialization failed :(" << std::endl;
        close_fptr(context);
        return -1;
    }

    // Get the load assembly function pointer.
    rc = get_delegate_fptr(
        context,
        hdt_load_assembly_and_get_function_pointer,
        &load_asm_and_get_fn_voidptr);

    if (rc != 0 || load_asm_and_get_fn_voidptr == nullptr)
    {
        std::cout << "Init Error: Getting the delegate failed :(" << std::endl;
        close_fptr(context);
        return -1;
    }
    close_fptr(context);

    load_assembly_and_get_function_pointer_fn load_asm_and_get_fn_ptr =
        (load_assembly_and_get_function_pointer_fn) load_asm_and_get_fn_voidptr;

    // Load the managed C# assembly and get the function pointer to the desired
    // entry point method.
    const char *entry_method = app_data->entry_point;

    std::string dotnet_type(app_data->mnamespace);
    dotnet_type = dotnet_type + "."
                              + app_data->mclass
                              + ", "
                              + get_name_from_path(app_data->path);

    // std::cout << "DEBUG: AppData->Path = " << app_data->path << std::endl;
    // std::cout << "DEBUG: Dotnet Type = " << dotnet_type.c_str() << std::endl;
    // std::cout << "DEBUG: Entry Method = " << entry_method << std::endl;
    // std::cout << "DEBUG: Get Name From Path(AppData->Path) = " << get_name_from_path(app_data->path) << std::endl;

    component_entry_point_fn entry_caller_fn = nullptr;
    rc = load_asm_and_get_fn_ptr(
        app_data->path,
        dotnet_type.c_str(),
        entry_method,
        nullptr,
        nullptr,
        (void **) &entry_caller_fn);

    if (rc != 0 || entry_caller_fn == nullptr)
    {
        std::cout << "Entry Error: Getting the entry point function pointer failed :(" << std::endl;
        return -1;
    }

    // Now, we can finally run our managed app. At long last!
    int csharp_exit_code = entry_caller_fn(nullptr, 0);
    return csharp_exit_code;
}


bool load_hostfxr(const char *hostfxr_path)
{
    // Defined in nethost.h
    get_hostfxr_parameters params { sizeof(get_hostfxr_parameters),
                                    hostfxr_path,
                                    nullptr };

    char hostfxr_path_buffer[MAX_PATH];
    size_t buffer_size = sizeof(hostfxr_path_buffer) / sizeof(char);

    // Calling nethost.h
    int rc = get_hostfxr_path(hostfxr_path_buffer, &buffer_size, &params);
    if (rc != 0)
        return false;

    // Now, we can load hostfxr and get all the exports we might need.
    void *lib = load_library(hostfxr_path_buffer);

    init_for_cmd_line_fptr = (hostfxr_initialize_for_dotnet_command_line_fn)
        get_export(lib, "hostfxr_initialize_for_dotnet_command_line");

    init_for_run_config_fptr = (hostfxr_initialize_for_runtime_config_fn)
        get_export(lib, "hostfxr_initialize_for_runtime_config");

    get_delegate_fptr = (hostfxr_get_runtime_delegate_fn)
        get_export(lib, "hostfxr_get_runtime_delegate");

    run_app_fptr = (hostfxr_run_app_fn) get_export(lib, "hostfxr_run_app");
    close_fptr = (hostfxr_close_fn) get_export(lib, "hostfxr_close");

    return (init_for_run_config_fptr && get_delegate_fptr && close_fptr);
}


std::string trim_extension(std::string path)
{
    size_t extension_start = path.find_last_of('.');
    return path.substr(0, extension_start);
}


const char *get_name_from_path(const char *path)
{
    // First, we need to ignore the file extension. For practical purposes, we will
    // assume we will always get a valid path :)    
    const char *ptr = (path + strlen(path) - 1);
    while (*ptr != '.') ptr--;

    // Get the size of the app name. We could just allocate a big chunk of memory
    // and skip this part, but let's add a little more spice and challenge to this
    // exercise :)
    size_t name_size = 0;
    while (*(--ptr) != '/') name_size++;

    // Copy the result to an array of its own that we can return from this function.
    char *result = (char *) calloc(name_size, sizeof(char));
    int i;

    for (i = 0; i < name_size; i++)
    {
        // We add a +1 here to "ptr" because it ended at the '/' in the previous
        // loop, and that's the directory separator, not part of the name.
        *(result + i) = *(ptr + i + 1);
    }
    return (const char *) result;
}



void *load_library(const char *path)
{
    void *h = dlopen(path, RTLD_LAZY | RTLD_LOCAL);
    assert(h != nullptr);
    return h;
}


void *get_export(void *h, const char *name)
{
    void *f = dlsym(h, name);
    assert(f != nullptr);
    return f;
}
