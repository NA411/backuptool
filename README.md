# BackupTool

<img width="979" height="469" alt="image" src="https://github.com/user-attachments/assets/6b2e6469-02f4-47bc-9db6-3dd3061aa6e7" />

[![Build Status](https://github.com/NA411/backuptool/workflows/.NET%20Console%20App%20Build/badge.svg)](https://github.com/NA411/backuptool/actions) [![Release](https://img.shields.io/github/v/release/NA411/backuptool)](https://github.com/NA411/backuptool/releases) [![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0) [![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey)](#quick-start)

BackupTool is a cross-platform, snapshot-based file backup utility written in C# for .NET 8. It efficiently stores and de-duplicates file content, supports integrity checking, and provides easy restore and pruning operations. The tool is designed for reliability and repeatability, with a focus on clear documentation and simple usage on Unix-like systems. It was built with System.CommandLine providing a robust command-line interface and Entity Framework Core to provide database support.

## Features

- **Snapshot backups**: Create point-in-time backups of directories with full subdirectory traversal
- **Content-based de-duplication**: Only unique file content is stored using SHA-256 hashing, saving disk space across snapshots
- **Restore functionality**: Restore any snapshot to a target directory with complete directory structure recreation
- **Pruning operations**: Remove snapshots and automatically reclaim orphaned content storage
- **Integrity checking**: Scan for corrupted or missing file content using hash verification
- **Detailed reporting**: List snapshots with accurate disk usage statistics and distinct content ownership
- **Verbose logging**: Optional console logging for diagnostics and progress tracking
- **Cross-platform support**: Runs on Windows, Linux, and macOS with self-contained executables
- **Binary content support**: Handles arbitrary binary files, large files, empty files, and special characters in filenames

## Architecture

BackupTool uses a sophisticated storage architecture:

- **SQLite database** stores metadata (snapshots, file references, and content hashes)
- **Content-based storage** with SHA-256 hashing ensures each unique file content is stored only once
- **Referential integrity** with cascading deletes ensures data consistency
- **Transaction support** protects against data corruption during operations

## Prerequisites (Only for Build)

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building from source)
- [Git](https://git-scm.com/) (for cloning the repository)
- SQLite support (included automatically with .NET)

## Quick Start

### From Pre-built Releases (Recommended)

1. **Download** the latest release from [GitHub Releases](https://github.com/NA411/backuptool/releases)
   - **Windows (x64)**: Download `backuptool-win-x64.zip`
   - **Linux (x64)**: Download `backuptool-linux-x64.tar.gz` 
   - **macOS (x64)**: Download `backuptool-osx-x64.tar.gz`

2. **Extract** the archive to your desired location

3. **Run** the executable from the command line:
   ```
   # Windows
   .\backuptool.exe --help
   
   # Linux/macOS
   ./backuptool --help
   ```

The pre-built releases are **self-contained executables** that include the .NET runtime and any dependencies - no additional installation required!

### Build from Source - See Prerequisites

1. **Clone the Repository**
   ```
   git clone https://github.com/NA411/backuptool.git
   cd backuptool
   ```

2. **Build the Project**
   ```
   dotnet build BackupTool.sln
   ```

3. **Run Tests** (Optional) - Windows Only
   ```
   dotnet test BackupTool.sln
   ```

4. **Run the Tool**
   ```
   dotnet run --project src/backuptool.console [command] [options]
   ```

## Usage Examples

### Create a Snapshot

Create a backup snapshot of a directory:
```
backuptool snapshot --target-directory /path/to/backup
```

### List Snapshots

List all snapshots with storage statistics:
```
backuptool list
```

**Output explanation:**
- `SNAPSHOT`: Unique identifier for each snapshot
- `TIMESTAMP`: When the snapshot was created
- `SIZE`: Total size of all files in this snapshot
- `DISTINCT_SIZE`: Size of unique content "owned" by this snapshot (accounting for de-duplication)
- `DIRECTORY`: Source directory that was backed up

### Restore a Snapshot

Restore a snapshot to a target directory:
```
backuptool restore --snapshot-number 1 --output-directory /path/to/restore
```

**Options:**
- `--create-directory`: Create the output directory if it doesn't exist

### Prune a Snapshot

Remove a snapshot and automatically reclaim orphaned storage:
```
backuptool prune --snapshot 1
```

**Important:** Pruning removes the snapshot and any file content that is no longer referenced by other snapshots.

### Check for Data Corruption

Scan for corrupted or missing file content:
```
backuptool check
```

### Enable Detailed Logging

Add `--verbose` or `-v` to any command for detailed console output:
```
backuptool snapshot --target-directory /path/to/backup --verbose
```

## Command Reference

| Command   | Description                                           | Key Options |
|-----------|-------------------------------------------------------|-------------|
| `snapshot`| Create a new snapshot of a directory                 | `--target-directory`, `--verbose` |
| `list`    | List all snapshots with storage usage statistics     | `--verbose` |
| `restore` | Restore a snapshot to a directory                     | `--snapshot-number`, `--output-directory`, `--create-directory`, `--verbose` |
| `prune`   | Remove a snapshot and reclaim orphaned storage       | `--snapshot`, `--verbose` |
| `check`   | Scan for corrupted or missing file content           | `--verbose` |

## Storage and De-duplication

BackupTool uses advanced de-duplication to minimize storage usage:

1. **Content Hashing**: Each file's content is hashed using SHA-256
2. **Single Storage**: Identical content is stored only once, regardless of filename or location
3. **Reference Counting**: Multiple snapshots can reference the same content
4. **Automatic Cleanup**: Pruning operations automatically remove unreferenced content

**Example:** If you backup the same directory twice without changes:
- First snapshot: Stores all file content
- Second snapshot: Only stores metadata (references to existing content)
- Storage usage: ~1x the original directory size (not 2x)

## Database and File Storage

- **Database**: SQLite database (`backup.db`) created in the working directory
- **No manual setup required**: Database schema is created automatically
- **Portable**: Database file can be moved with the executable
- **Inspection**: Use SQLite tools to examine the database structure if needed

## Advanced Features

### Cross-Platform Path Handling

- Automatically handles different path separators (Windows `\` vs Unix `/`)
- Supports special characters in filenames and directory names
- Handles Unicode filenames on supported platforms
- Preserves exact directory structure during restore operations

### Binary File Support

- Handles all file types: text, binary, executables, images, compressed files
- Preserves exact bit-for-bit content integrity
- No file size limitations (tested with multi-GB files)
- Empty files are supported and handled correctly

### Error Handling and Recovery

- **Transactional operations**: Failed operations don't leave partial data
- **Graceful degradation**: Single file failures don't stop entire operations
- **Detailed error reporting**: Clear messages for troubleshooting
- **Integrity verification**: Hash validation ensures data hasn't been corrupted

## Performance Characteristics

- **Memory efficient**: Streams large files instead of loading into memory
- **I/O optimized**: Minimizes redundant file reads through content hashing
- **Database indexed**: Fast snapshot listing and content lookups
- **Progress reporting**: Shows progress for large operations (every 50-100 files)

## Troubleshooting

### Common Issues

**"Directory does not exist"**
- Ensure the path exists and is accessible
- Use `--create-directory` flag for restore operations if needed

**"Snapshot X does not exist"**
- Use `backuptool list` to see available snapshots
- Snapshot may have been pruned

**Permission issues**
- Run with appropriate user privileges for the source/destination directories
- Ensure the database file location is writable

### Database Issues

**Database locked**
- Ensure no other BackupTool instances are running
- Check if the database file has correct permissions

**Database corruption**
- Use `backuptool check` to verify content integrity
- Consider backing up the database file before major operations

### Performance Issues

**Slow snapshot creation**
- Large numbers of small files can be slower than fewer large files
- Network drives may impact performance significantly
- Use `--verbose` to see progress and identify bottlenecks

## Development and Testing

The project includes comprehensive test coverage:

- **Unit tests**: Test individual components and business logic
- **Integration tests**: Test end-to-end workflows and data integrity
- **UI tests**: Test command-line interface and user interactions
- **Cross-platform tests**: Verify behavior on different operating systems

Run the test suite:
```
dotnet test BackupTool.sln --verbosity normal
```

## CI/CD and Releases

The project uses GitHub Actions for:
- **Automated building** on Windows, Linux, and macOS
- **Test execution** across multiple platforms
- **Self-contained executable creation** with no runtime dependencies
- **Automatic releases** when version tags are pushed

## Architecture Notes

The codebase follows clean architecture principles:

- **Domain layer**: Core entities and business logic
- **Application layer**: Use cases and orchestration (BackupService)
- **Infrastructure layer**: Data access and external dependencies
- **Presentation layer**: Command-line interface

Key patterns used:
- **Repository pattern** for data access abstraction
- **Unit of Work pattern** for transaction management
- **Dependency injection** for loose coupling
- **Command pattern** for CLI structure

## License

This project uses packages licensed under the MIT License. The project itself has no explicit license - see individual package licenses for their terms.

## Version History and Compatibility

- **Current version**: Built with .NET 8
- **Database compatibility**: SQLite databases are forward and backward compatible
- **File format**: Uses standard file system storage - no proprietary formats
- **Breaking changes**: Semantic versioning indicates compatibility
