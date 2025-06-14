# AHK Bootstrapper

A C# loader that authenticates a Roblox username via GamePass and HWID, securely fetches an encrypted AutoHotkey (AHK) script, and launches it with clean console visuals.

---

### 🔐 Features

- Verifies GamePass ownership using Roblox's public API
- Confirms machine identity via server-side HWID check
- Uses Diffie–Hellman to securely fetch the AHK script
- Executes AHK script in memory

---

### ⚙️ Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [AutoHotkey v1.1](https://www.autohotkey.com/) installed on your system
- Setup the [Authentication Server](https://github.com/opsec-bot/gagAuthServer.git)

---

### 🛠️ How to Build

```bash
dotnet build -c Release
````

To run:

```bash
start ./bin/Release/net8.0-windows/Grow_A_Garden_Auth.exe
```

---

### ✏️ Configuration

Before building, make sure to update:

* **Base URL** in `Program.cs`:

  ```csharp
  private const string BaseUrl = "<your_url>";
  ```

* **GamePass ID** in `Authentication.cs`:

  ```csharp
  private static readonly string[] GamePassIds = { "your_gamepass_id_here" };
  ```
