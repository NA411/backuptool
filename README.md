# BackupTool

<img width="979" height="469" alt="image" src="https://github.com/user-attachments/assets/6b2e6469-02f4-47bc-9db6-3dd3061aa6e7" />


BackupTool is a cross-platform, snapshot-based file backup utility written in C# for .NET 8. It efficiently stores and de-duplicates file content, supports integrity checking, and provides easy restore and pruning operations. The tool is designed for reliability and repeatability, with a focus on clear documentation and simple usage on Unix-like systems. It was built with System.CommandLine providing a robust command-line interface and Entity Framework Core to provide database support.

## Features

- **Snapshot backups**: Create point-in-time backups of directories.
- **De-duplication**: Only unique file content is stored, saving disk space.
- **Restore**: Restore any snapshot to a target directory.
- **Prune**: Remove snapshots and reclaim space.
- **Integrity check**: Scan for corrupted or missing file content.
- **Detailed reporting**: List snapshots with disk usage statistics.
- **Verbose logging**: Optional console logging for diagnostics.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Git](https://git-scm.com/) (for cloning the repository)
- SQLite (no manual setup required; used automatically for metadata and content storage)

# Quick Start
## From Releases
1. Download the latest release from [GitHub Releases](https://github.com/NA411/backuptool/releases).
2. Extract the archive to your desired location.
3. Run the executable from the command line:

## Build from Code
### 1. Clone the Repository

```
git clone https://github.com/NA411/backuptool.git
```

### 2. Build the Project

```
dotnet build BackupTool.sln
```

### 3. (Optional) Run the Test Suite

To verify your build, you can run the included unit tests:

```
dotnet test BackupTool.sln
```

### 4. Execute the Backup Tool

Navigate to the console project directory and run:

```
dotnet run --project src/backuptool.console [command] [options]
```

## Usage Examples

### Create a Snapshot

Create a backup snapshot of a directory:

``` 
backuptool snapshot --target-directory <target-directory>
```

### List Snapshots

List all snapshots and their disk usage:

```
backuptool list
```

### Restore a Snapshot

Restore a snapshot to a target directory:

```
backuptool restore --snapshot-number <snapshot-number> --output-directory <output-directory>
```

Target directory must exist and will overwrite existing files. Restore has an optional command, `--create-directory`, to create the directory for restoration.

### Prune a Snapshot

Remove a snapshot and reclaim space:

```
backuptool prune --snapshot <snapshot_number>
```

### Check for Corrupted Content

Scan the database for corrupted or missing file content:

```
backuptool check
```

### Enable Verbose Logging

Add `--verbose` or `-v` to any command for detailed console logs:

```
backuptool snapshot --target-directory <target-directory> --verbose
```

## Command Reference

| Command   | Description                                      |
|-----------|--------------------------------------------------|
| snapshot  | Create a new snapshot of a directory              |
| list      | List all snapshots and their disk usage           |
| restore   | Restore a snapshot to a directory                 |
| prune     | Remove a snapshot and reclaim space               |
| check     | Scan for corrupted or missing file content        |

## Database

- The tool uses a local SQLite database (`backup.db`) in the working directory.
- No manual database setup is required.

## Troubleshooting

- Ensure the .NET 8 SDK is installed and available in your PATH.
- For permission issues, run commands with appropriate user privileges.
- For database inspection, use the `sqlite3` CLI or a GUI tool.

## Contributing

Contributions, issues, and feature requests are welcome! Please open an issue or submit a pull request on [GitHub](https://github.com/NA411/backuptool).

## License

This project has no license but uses packages licensed under the MIT License.
