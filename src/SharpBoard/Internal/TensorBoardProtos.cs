using System;
using System.Collections.Generic;
using System.IO;

namespace SharpBoard.Internal;

internal static class TensorBoardProtos {
    private const uint EVENT_WALL_TIME_FIELD = 1;
    private const uint EVENT_STEP_FIELD = 2;
    private const uint EVENT_FILE_VERSION_FIELD = 3;
    private const uint EVENT_SUMMARY_FIELD = 5;

    private const uint SUMMARY_VALUE_FIELD = 1;

    private const uint SUMMARY_VALUE_TAG_FIELD = 1;
    private const uint SUMMARY_VALUE_SIMPLE_VALUE_FIELD = 2;
    private const uint SUMMARY_VALUE_TENSOR_FIELD = 8;

    private const uint TENSOR_DTYPE_FIELD = 1;
    private const uint TENSOR_SHAPE_FIELD = 2;
    private const uint TENSOR_CONTENT_FIELD = 4;
    private const uint TENSOR_FLOAT_VAL_FIELD = 5;
    private const uint TENSOR_DOUBLE_VAL_FIELD = 6;

    private const uint TENSOR_SHAPE_DIM_FIELD = 1;
    private const uint TENSOR_SHAPE_DIM_SIZE_FIELD = 1;

    public static TensorBoardEvent ParseEvent(byte[] protoBytes) {
        if (protoBytes is null) throw new ArgumentNullException(nameof(protoBytes));

        DateTimeOffset wallTime = TensorBoardEvent.UnixEpoch;
        long step = 0;
        string? fileVersion = null;
        var scalars = new List<TensorBoardScalar>();

        var reader = new ProtoReader(protoBytes);
        while (reader.TryReadTag(out uint fieldNumber, out ProtoWireType wireType)) {
            switch (fieldNumber) {
            case EVENT_WALL_TIME_FIELD:
                if (wireType != ProtoWireType.Fixed64) throw new InvalidDataException("Event.wall_time wire type mismatch.");
                wallTime = TensorBoardEvent.FromUnixTimeSecondsDouble(reader.ReadDouble());
                break;
            case EVENT_STEP_FIELD:
                if (wireType != ProtoWireType.Varint) throw new InvalidDataException("Event.step wire type mismatch.");
                step = reader.ReadInt64();
                break;
            case EVENT_FILE_VERSION_FIELD:
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("Event.file_version wire type mismatch.");
                fileVersion = reader.ReadString();
                break;
            case EVENT_SUMMARY_FIELD:
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("Event.summary wire type mismatch.");
                ProtoBytes summaryBytes = reader.ReadBytes();
                ParseSummary(summaryBytes, scalars);
                break;
            default:
                reader.SkipField(wireType);
                break;
            }
        }

        return new TensorBoardEvent(wallTime, step, fileVersion, [.. scalars]);
    }

    public static byte[] SerializeEvent(TensorBoardEvent @event) {
        if (@event is null) throw new ArgumentNullException(nameof(@event));

        using var ms = new MemoryStream();
        var writer = new ProtoWriter(ms);

        writer.WriteTag(EVENT_WALL_TIME_FIELD, ProtoWireType.Fixed64);
        writer.WriteDouble(@event.WallTimeSeconds);

        if (@event.Step != 0) {
            writer.WriteTag(EVENT_STEP_FIELD, ProtoWireType.Varint);
            writer.WriteInt64(@event.Step);
        }

        if (!string.IsNullOrEmpty(@event.FileVersion)) {
            writer.WriteString(EVENT_FILE_VERSION_FIELD, @event.FileVersion!);
        }

        if (@event.Scalars.Count != 0) {
            writer.WriteMessage(EVENT_SUMMARY_FIELD, summaryWriter => {
                foreach (TensorBoardScalar scalar in @event.Scalars) {
                    summaryWriter.WriteMessage(SUMMARY_VALUE_FIELD, valueWriter => {
                        valueWriter.WriteString(SUMMARY_VALUE_TAG_FIELD, scalar.Tag);
                        valueWriter.WriteMessage(SUMMARY_VALUE_TENSOR_FIELD, tensorWriter => {
                            // DT_DOUBLE
                            tensorWriter.WriteTag(TENSOR_DTYPE_FIELD, ProtoWireType.Varint);
                            tensorWriter.WriteVarint(2);

                            tensorWriter.WriteTag(TENSOR_DOUBLE_VAL_FIELD, ProtoWireType.Fixed64);
                            tensorWriter.WriteDouble(scalar.Value);
                        });
                    });
                }
            });
        }

        return ms.ToArray();
    }

    private static void ParseSummary(ProtoBytes summaryBytes, List<TensorBoardScalar> scalars) {
        var reader = new ProtoReader(summaryBytes.Buffer, summaryBytes.Offset, summaryBytes.Length);
        while (reader.TryReadTag(out uint fieldNumber, out ProtoWireType wireType)) {
            if (fieldNumber == SUMMARY_VALUE_FIELD) {
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("Summary.value wire type mismatch.");
                ProtoBytes valueBytes = reader.ReadBytes();
                if (TryParseSummaryValueScalar(valueBytes, out TensorBoardScalar scalar)) scalars.Add(scalar);
            } else {
                reader.SkipField(wireType);
            }
        }
    }

    private static bool TryParseSummaryValueScalar(ProtoBytes valueBytes, out TensorBoardScalar scalar) {
        string? tag = null;
        float? simpleValue = null;
        double? tensorScalar = null;

        var reader = new ProtoReader(valueBytes.Buffer, valueBytes.Offset, valueBytes.Length);
        while (reader.TryReadTag(out uint fieldNumber, out ProtoWireType wireType)) {
            switch (fieldNumber) {
            case SUMMARY_VALUE_TAG_FIELD:
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("Summary.Value.tag wire type mismatch.");
                tag = reader.ReadString();
                break;
            case SUMMARY_VALUE_SIMPLE_VALUE_FIELD:
                if (wireType != ProtoWireType.Fixed32) throw new InvalidDataException("Summary.Value.simple_value wire type mismatch.");
                simpleValue = reader.ReadFloat();
                break;
            case SUMMARY_VALUE_TENSOR_FIELD:
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("Summary.Value.tensor wire type mismatch.");
                ProtoBytes tensorBytes = reader.ReadBytes();
                if (TryParseScalarFromTensor(tensorBytes, out double value)) tensorScalar = value;
                break;
            default:
                reader.SkipField(wireType);
                break;
            }
        }

        if (tag is null) {
            scalar = default;
            return false;
        }

        if (simpleValue.HasValue) {
            scalar = new TensorBoardScalar(tag, simpleValue.Value);
            return true;
        }

        if (tensorScalar.HasValue) {
            scalar = new TensorBoardScalar(tag, tensorScalar.Value);
            return true;
        }

        scalar = default;
        return false;
    }

    private static bool TryParseScalarFromTensor(ProtoBytes tensorBytes, out double value) {
        int dtype = 0;
        long elementCount = 1;
        byte[]? tensorContent = null;
        int tensorContentOffset = 0;
        int tensorContentLength = 0;
        float? firstFloatVal = null;
        double? firstDoubleVal = null;

        var reader = new ProtoReader(tensorBytes.Buffer, tensorBytes.Offset, tensorBytes.Length);
        while (reader.TryReadTag(out uint fieldNumber, out ProtoWireType wireType)) {
            switch (fieldNumber) {
            case TENSOR_DTYPE_FIELD:
                if (wireType != ProtoWireType.Varint) throw new InvalidDataException("TensorProto.dtype wire type mismatch.");
                dtype = checked((int)reader.ReadVarint64());
                break;
            case TENSOR_SHAPE_FIELD:
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("TensorProto.tensor_shape wire type mismatch.");
                ProtoBytes shapeBytes = reader.ReadBytes();
                elementCount = TryGetElementCount(shapeBytes) ?? elementCount;
                break;
            case TENSOR_CONTENT_FIELD:
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("TensorProto.tensor_content wire type mismatch.");
                ProtoBytes content = reader.ReadBytes();
                tensorContent = content.Buffer;
                tensorContentOffset = content.Offset;
                tensorContentLength = content.Length;
                break;
            case TENSOR_FLOAT_VAL_FIELD:
                if (wireType == ProtoWireType.Fixed32) {
                    firstFloatVal ??= reader.ReadFloat();
                } else if (wireType == ProtoWireType.LengthDelimited) {
                    ProtoBytes packed = reader.ReadBytes();
                    firstFloatVal ??= TryReadFirstPackedFloat(packed);
                } else {
                    throw new InvalidDataException("TensorProto.float_val wire type mismatch.");
                }
                break;
            case TENSOR_DOUBLE_VAL_FIELD:
                if (wireType == ProtoWireType.Fixed64) {
                    firstDoubleVal ??= reader.ReadDouble();
                } else if (wireType == ProtoWireType.LengthDelimited) {
                    ProtoBytes packed = reader.ReadBytes();
                    firstDoubleVal ??= TryReadFirstPackedDouble(packed);
                } else {
                    throw new InvalidDataException("TensorProto.double_val wire type mismatch.");
                }
                break;
            default:
                reader.SkipField(wireType);
                break;
            }
        }

        if (elementCount != 1) {
            value = default;
            return false;
        }

        // dtype values from tensorflow/core/framework/types.proto
        const int DtFloat = 1;
        const int DtDouble = 2;

        if (dtype == DtFloat) {
            if (tensorContent is not null && tensorContentLength >= 4) {
                value = ReadSingleLittleEndian(tensorContent, tensorContentOffset);
                return true;
            }
            if (firstFloatVal.HasValue) {
                value = firstFloatVal.Value;
                return true;
            }
        }

        if (dtype == DtDouble) {
            if (tensorContent is not null && tensorContentLength >= 8) {
                value = ReadDoubleLittleEndian(tensorContent, tensorContentOffset);
                return true;
            }
            if (firstDoubleVal.HasValue) {
                value = firstDoubleVal.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static long? TryGetElementCount(ProtoBytes shapeBytes) {
        long elementCount = 1;
        int dimsSeen = 0;

        var reader = new ProtoReader(shapeBytes.Buffer, shapeBytes.Offset, shapeBytes.Length);
        while (reader.TryReadTag(out uint fieldNumber, out ProtoWireType wireType)) {
            if (fieldNumber == TENSOR_SHAPE_DIM_FIELD) {
                if (wireType != ProtoWireType.LengthDelimited) throw new InvalidDataException("TensorShapeProto.dim wire type mismatch.");
                ProtoBytes dimBytes = reader.ReadBytes();
                long? dimSize = TryGetDimSize(dimBytes);
                if (!dimSize.HasValue) continue;
                dimsSeen++;
                if (dimSize.Value < 0) return null;

                try {
                    checked { elementCount *= dimSize.Value; }
                } catch (OverflowException) {
                    return null;
                }
            } else {
                reader.SkipField(wireType);
            }
        }

        if (dimsSeen == 0) return 1;
        return elementCount;
    }

    private static long? TryGetDimSize(ProtoBytes dimBytes) {
        var reader = new ProtoReader(dimBytes.Buffer, dimBytes.Offset, dimBytes.Length);
        while (reader.TryReadTag(out uint fieldNumber, out ProtoWireType wireType)) {
            if (fieldNumber == TENSOR_SHAPE_DIM_SIZE_FIELD) {
                if (wireType != ProtoWireType.Varint) throw new InvalidDataException("TensorShapeProto.Dim.size wire type mismatch.");
                return reader.ReadInt64();
            }
            reader.SkipField(wireType);
        }
        return null;
    }

    private static float? TryReadFirstPackedFloat(ProtoBytes packed) {
        if (packed.Length < 4) return null;
        float value = ReadSingleLittleEndian(packed.Buffer, packed.Offset);
        return value;
    }

    private static double? TryReadFirstPackedDouble(ProtoBytes packed) {
        if (packed.Length < 8) return null;
        double value = ReadDoubleLittleEndian(packed.Buffer, packed.Offset);
        return value;
    }

    private static float ReadSingleLittleEndian(byte[] buffer, int offset) {
        if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(buffer, offset);
        byte[] tmp = [buffer[offset + 3], buffer[offset + 2], buffer[offset + 1], buffer[offset + 0]];
        return BitConverter.ToSingle(tmp, 0);
    }

    private static double ReadDoubleLittleEndian(byte[] buffer, int offset) {
        if (BitConverter.IsLittleEndian) return BitConverter.ToDouble(buffer, offset);
        byte[] tmp =
        [
            buffer[offset + 7],
            buffer[offset + 6],
            buffer[offset + 5],
            buffer[offset + 4],
            buffer[offset + 3],
            buffer[offset + 2],
            buffer[offset + 1],
            buffer[offset + 0],
        ];
        return BitConverter.ToDouble(tmp, 0);
    }
}
