## 🏗️ Building from Source

### Prerequisites

- .NET Framework 4.8 (https://dotnet.microsoft.com/download/dotnet-framework/net48)
- Visual Studio 2019+ (https://visualstudio.microsoft.com/) or .NET SDK (https://dotnet.microsoft.com/download)
- Git (https://git-scm.com/) (optional)

### Clone the Repository

git clone https://github.com/yourusername/FireKeeper.git
cd FireKeeper

### Build with Visual Studio

1. Open FireKeeper.sln or FireKeeper.csproj
2. Right-click the solution -> Restore NuGet Packages
3. Build -> Build Solution (Ctrl+Shift+B)
4. The executable will be in bin\Release\net48\

### Build with Command Line

# Restore dependencies
dotnet restore

# Build Release version
dotnet build -c Release -f net48

# Run the application
dotnet run

# Publish as single executable (recommended)
dotnet publish -c Release -r win-x86 --self-contained false -p:PublishSingleFile=true

# Output location:
# bin\Release\net48\win-x86\publish\FireKeeper.exe

### Build Options

| Command                   | Description                               |
| ------------------------- | ----------------------------------------- |
| dotnet build              | Builds the project                        |
| dotnet run                | Builds and runs                           |
| dotnet publish            | Creates a standalone executable           |
| -c Release                | Build in Release mode (optimized)         |
| -f net48                  | Target .NET Framework 4.8                 |
| -r win-x86                | Target 32-bit Windows                     |
| --self-contained false    | Uses system .NET Framework (smaller file) |
| -p:PublishSingleFile=true | Creates single executable file            |

### Output Files

After building, you'll find:

FireKeeper/
├── bin/
│   └── Release/
│       └── net48/
│           ├── win-x86/
│           │   └── publish/
│           │       └── FireKeeper.exe    ← Your application
│           └── FireKeeper.dll
└── obj/                                    ← Intermediate files (can delete)

### Required Files for Distribution

To distribute FireKeeper, you need:

FireKeeper.exe                    ← Main executable
firekeeper.ico                    ← Icon file

Optional:
README.md                         ← Documentation
LICENSE                           ← License file