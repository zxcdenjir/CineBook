# Repository Guidelines

## Project Structure

`CineBook.slnx` is the entry point. Shared Avalonia code is in `CineBook/CineBook/`: `Views/` (AXAML), `Controls/`, `ViewModels/`, `Models/`, `Context/` (EF Core), and `Assets/`. Launchers are `CineBook.Desktop/`, `CineBook.Android/`, and `CineBook.iOS/`; no browser project exists. Put cross-platform behavior in the shared project.

## Build and Run

Run commands from the repository root:

- `dotnet restore CineBook.slnx` restores NuGet packages.
- `dotnet build CineBook/CineBook.Desktop/CineBook.Desktop.csproj` builds the desktop app and shared project.
- `dotnet run --project CineBook/CineBook.Desktop/CineBook.Desktop.csproj` runs the desktop app.
- `dotnet build CineBook/CineBook.Android/CineBook.Android.csproj` builds Android when its .NET workload is installed. iOS builds require the Apple toolchain and iOS workload.

No automated test project exists yet; use `dotnet test CineBook.slnx` when one is added.

## Code and UI Conventions

Use four spaces, file-scoped namespaces, nullable references, `PascalCase` for types/public members, and `camelCase` for locals/parameters. Match filenames to main types. AXAML holds layout/bindings; view models hold presentation logic. Use CommunityToolkit.Mvvm for observable properties and commands. No formatter or linter is configured; follow nearby code.

## UX and UI

Use Semi Design through Ursa controls. Preserve CineBook's warm media-library look: copper accent (`#B85C38`), cream light surfaces, and deep brown dark surfaces. Define shared colors in `App.axaml` theme dictionaries and use `DynamicResource` for both themes. Keep contrast clear, borders restrained, cards rounded, and spacing compact and even.

Support desktop, phone portrait, and landscape without large gaps or stranded content. Align card title, creator, rating, genres, status, and favorite control. Right-align single-line genres; left-align them when wrapped. Use bold capsule status badges. Vary poster-placeholder gradients cyclically by item ID and choose the outline icon by media type. Use a muted outlined heart when not favorite and a filled accent heart when favorite. Review both themes and narrow/wide layouts.

## SQLite Data

The shared project embeds root `cinebook.db` as a seed template. On first launch, `CineBookContext` copies it to local app data (Windows: `%LOCALAPPDATA%\CineBook\cinebook.db`); later launches keep that writable copy. The tracked root file is seed data, never a personal collection. Update it with EF model configuration when schema/data changes. Startup uses `EnsureCreated`, not migrations.

## Changes and Reviews

Use short imperative commit subjects, e.g. `Add collection sorting`. Build the affected target and report the result. Include UI screenshots when practical. Keep IDE settings, credentials, and personal data out of commits.
