using System;
using System.IO;

namespace SharpBoard.Tests;

internal sealed class TempDirectory: IDisposable {
    public TempDirectory(string? prefix = null) {
        prefix ??= "SharpBoard.Tests";
        this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + "." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.Path);
    }

    public string Path { get; }

    public void Dispose() {
        try {
            if (Directory.Exists(this.Path)) Directory.Delete(this.Path, recursive: true);
        } catch {
            // Best effort cleanup (tests should not fail on locked temp files).
        }
    }
}
