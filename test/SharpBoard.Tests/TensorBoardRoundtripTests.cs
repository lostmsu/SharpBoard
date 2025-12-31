using System;
using System.Collections.Generic;
using System.IO;

using Xunit;

namespace SharpBoard.Tests;

public sealed class TensorBoardRoundtripTests {
    private const string DEFAULT_MINIGPT_RUN_DIR = @"C:\Users\lost\Projects\Research\minGPT\runs\Tiny\v-64";
    private const string MINIGPT_RUN_DIR_ENV_VAR = "SHARPBOARD_MINIGPT_V64_RUN_DIR";

    [Fact]
    public void WriteRead_RoundtripScalars() {
        using var temp = new TempDirectory();

        using var log = new TensorBoardLogWriter(temp.Path, fileName: "events.out.tfevents.test");
        log.AddScalar("loss", 1.25, step: 1, wallTime: new DateTimeOffset(2020, 01, 02, 03, 04, 05, TimeSpan.Zero));
        log.AddScalar("lr", 0.0001, step: 1, wallTime: new DateTimeOffset(2020, 01, 02, 03, 04, 06, TimeSpan.Zero));
        log.Flush();

        var scalars = new List<(string Tag, double Value, long Step)>();
        string? fileVersion = null;

        using var reader = TensorBoardEventFileReader.Open(log.FilePath, validateChecksums: true);
        while (reader.TryRead(out TensorBoardEventRecord? record)) {
            TensorBoardEvent ev = record!.Parse();
            if (!string.IsNullOrEmpty(ev.FileVersion)) fileVersion = ev.FileVersion;
            foreach (TensorBoardScalar s in ev.Scalars) {
                scalars.Add((s.Tag, s.Value, ev.Step));
            }
        }

        Assert.Equal(TensorBoardLogWriter.DEFAULT_FILE_VERSION, fileVersion);
        Assert.Contains(scalars, s => s.Tag == "loss" && s.Step == 1 && Math.Abs(s.Value - 1.25) < 1e-6);
        Assert.Contains(scalars, s => s.Tag == "lr" && s.Step == 1 && Math.Abs(s.Value - 0.0001) < 1e-9);
    }

    [Fact]
    public void ReadWriteRead_MinGptV64_RoundtripRawRecords() {
        string sourcePath = GetMiniGptEventFilePath();

        using var temp = new TempDirectory();
        string inputCopy = System.IO.Path.Combine(temp.Path, "input.tfevents");
        File.Copy(sourcePath, inputCopy, overwrite: true);

        var originalProtoRecords = new List<byte[]>();
        var originalTags = new HashSet<string>(StringComparer.Ordinal);

        using (var reader = TensorBoardEventFileReader.Open(inputCopy, validateChecksums: true)) {
            while (reader.TryRead(out TensorBoardEventRecord? record)) {
                byte[] proto = record!.GetEventProtoBytes();
                originalProtoRecords.Add(proto);

                TensorBoardEvent ev = record!.Parse();
                foreach (TensorBoardScalar scalar in ev.Scalars) originalTags.Add(scalar.Tag);
            }
        }

        Assert.NotEmpty(originalProtoRecords);
        Assert.Contains("loss", originalTags);

        string copiedPath = System.IO.Path.Combine(temp.Path, "copied.tfevents");
        using (var writer = TensorBoardEventFileWriter.Create(copiedPath)) {
            foreach (byte[] proto in originalProtoRecords) {
                writer.Write(new TensorBoardEventRecord(proto));
            }
            writer.Flush();
        }

        var copiedProtoRecords = new List<byte[]>();
        using (var reader = TensorBoardEventFileReader.Open(copiedPath, validateChecksums: true)) {
            while (reader.TryRead(out TensorBoardEventRecord? record)) {
                copiedProtoRecords.Add(record!.GetEventProtoBytes());
            }
        }

        Assert.Equal(originalProtoRecords.Count, copiedProtoRecords.Count);
        for (int i = 0; i < originalProtoRecords.Count; i++) {
            Assert.Equal(originalProtoRecords[i], copiedProtoRecords[i]);
        }
    }

    [Fact]
    public void ReadWriteRead_MinGptV64_RoundtripParsedEvents() {
        string sourcePath = GetMiniGptEventFilePath();

        using var temp = new TempDirectory();
        string inputCopy = System.IO.Path.Combine(temp.Path, "input.tfevents");
        File.Copy(sourcePath, inputCopy, overwrite: true);

        var parsedEvents = new List<TensorBoardEvent>();

        using (var reader = TensorBoardEventFileReader.Open(inputCopy, validateChecksums: true)) {
            while (reader.TryRead(out TensorBoardEventRecord? record)) {
                parsedEvents.Add(record!.Parse());
            }
        }

        Assert.NotEmpty(parsedEvents);

        string rewrittenPath = System.IO.Path.Combine(temp.Path, "rewritten.tfevents");
        using (var writer = TensorBoardEventFileWriter.Create(rewrittenPath)) {
            foreach (TensorBoardEvent ev in parsedEvents) writer.Write(ev);
            writer.Flush();
        }

        var rewrittenEvents = new List<TensorBoardEvent>();
        using (var reader = TensorBoardEventFileReader.Open(rewrittenPath, validateChecksums: true)) {
            while (reader.TryRead(out TensorBoardEventRecord? record)) {
                rewrittenEvents.Add(record!.Parse());
            }
        }

        Assert.Equal(parsedEvents.Count, rewrittenEvents.Count);

        for (int i = 0; i < parsedEvents.Count; i++) {
            Assert.Equal(parsedEvents[i].FileVersion, rewrittenEvents[i].FileVersion);
            Assert.Equal(parsedEvents[i].Step, rewrittenEvents[i].Step);
            Assert.Equal(parsedEvents[i].Scalars.Count, rewrittenEvents[i].Scalars.Count);

            for (int j = 0; j < parsedEvents[i].Scalars.Count; j++) {
                TensorBoardScalar a = parsedEvents[i].Scalars[j];
                TensorBoardScalar b = rewrittenEvents[i].Scalars[j];

                Assert.Equal(a.Tag, b.Tag);
                Assert.True(Math.Abs(a.Value - b.Value) < 1e-4, $"Scalar changed too much: {a.Tag} {a.Value} -> {b.Value}");
            }
        }
    }

    private static string GetMiniGptEventFilePath() {
        string runDir = Environment.GetEnvironmentVariable(MINIGPT_RUN_DIR_ENV_VAR) ?? DEFAULT_MINIGPT_RUN_DIR;
        if (!Directory.Exists(runDir)) {
            throw new DirectoryNotFoundException(
                $"minGPT run directory not found: '{runDir}'. Set {MINIGPT_RUN_DIR_ENV_VAR} to point at the local v-64 run directory.");
        }

        foreach (string file in Directory.EnumerateFiles(runDir, "events.out.tfevents.*", SearchOption.TopDirectoryOnly)) {
            return file;
        }

        throw new FileNotFoundException($"No TensorBoard event files found in '{runDir}'.");
    }
}
