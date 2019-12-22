# Mogu
🍄Manged Dll Injection and hook function

## Usage

```csharp

// A Main method for host process.
static async Task Main(string[] args)
{
    // get notepad's pid.
    var pid = (uint)Process.GetProcessesByName("notepad").First().Id;
    var injector = new Injector();

    // inject self dll and invoke EntoryPoint on "notepad.exe".
    using (var con = await injector.InjectAsync(pid, c => EntryPoint(c)))
    {
        var buffer = new byte[1024];
        while (con.IsConnected)
        {
            // wait message from "notepad.exe".
            var count = await con.Pipe.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
            var str = Encoding.UTF8.GetString(buffer, 0, count);
            Console.WriteLine($"recv:{str}");
        }
    }
}

// A EntryPoint method for target process.
public static async ValueTask EntryPoint(Connection con)
{
    var text = "Hello from notepad.exe!";
    var buf = Encoding.UTF8.GetBytes(text);
    // can communicate to host throgh Connection.
    await con.Pipe.WriteAsync(buf, 0, buf.Length);
}

```
