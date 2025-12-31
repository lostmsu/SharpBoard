# SharpBoard

TensorBoard event file support for .NET Standard 2.0.

- Read/write TFRecord streams that contain TensorBoard `Event` protobuf messages
- Log scalar values via `TensorBoardLogWriter` (scalars are written as `TensorProto` with `dtype=DT_DOUBLE`)

Scope: scalar summaries only (no images/audio/histograms yet).

## Quick start

```csharp
using SharpBoard;

using var log = new TensorBoardLogWriter(@"C:\runs\myrun");
log.AddScalar("loss", 1.23, step: 100);
log.AddScalar("lr", 0.0001, step: 100);
log.Flush();
```

`AddScalar` accepts an optional `DateTimeOffset wallTime` if you want deterministic timestamps.

## Reading

```csharp
using SharpBoard;

using var reader = TensorBoardEventFileReader.Open(path);
while (reader.TryRead(out var record)) {
    TensorBoardEvent ev = record.Parse();
    foreach (TensorBoardScalar s in ev.Scalars) {
        Console.WriteLine($"{ev.Step} {s.Tag}={s.Value}");
    }
}
```

## Tests

Run:

`dotnet test SharpBoard.slnx -c Release`

- `WriteRead_RoundtripScalars`: synthetic write-read roundtrip
- `ReadWriteRead_MinGptV64_*`: integration roundtrips using the local minGPT `v-64` run directory

To override the default local minGPT run path, set:

- `SHARPBOARD_MINIGPT_V64_RUN_DIR` to the directory containing `events.out.tfevents.*`
