using System;
using System.IO;

namespace SharpBoard.Internal;

internal static class TfRecord {
    private const uint CRC_MASK_DELTA = 0xA282_EAD8u;

    public static uint MaskedCrc(byte[] data) {
        if (data is null) throw new ArgumentNullException(nameof(data));
        uint crc = Crc32C.Compute(data);
        return Mask(crc);
    }

    public static uint MaskedCrc(byte[] data, int offset, int count) {
        if (data is null) throw new ArgumentNullException(nameof(data));
        uint crc = Crc32C.Compute(data, offset, count);
        return Mask(crc);
    }

    public static uint Mask(uint crc) {
        return unchecked(((crc >> 15) | (crc << 17)) + CRC_MASK_DELTA);
    }

    public static ulong ReadUInt64LittleEndian(byte[] buffer, int offset) {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length - 8) throw new ArgumentOutOfRangeException(nameof(offset));
        return
            (ulong)buffer[offset + 0] |
            ((ulong)buffer[offset + 1] << 8) |
            ((ulong)buffer[offset + 2] << 16) |
            ((ulong)buffer[offset + 3] << 24) |
            ((ulong)buffer[offset + 4] << 32) |
            ((ulong)buffer[offset + 5] << 40) |
            ((ulong)buffer[offset + 6] << 48) |
            ((ulong)buffer[offset + 7] << 56);
    }

    public static uint ReadUInt32LittleEndian(byte[] buffer, int offset) {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length - 4) throw new ArgumentOutOfRangeException(nameof(offset));
        return
            (uint)buffer[offset + 0] |
            ((uint)buffer[offset + 1] << 8) |
            ((uint)buffer[offset + 2] << 16) |
            ((uint)buffer[offset + 3] << 24);
    }

    public static void WriteUInt64LittleEndian(byte[] buffer, int offset, ulong value) {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length - 8) throw new ArgumentOutOfRangeException(nameof(offset));

        buffer[offset + 0] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
        buffer[offset + 4] = (byte)(value >> 32);
        buffer[offset + 5] = (byte)(value >> 40);
        buffer[offset + 6] = (byte)(value >> 48);
        buffer[offset + 7] = (byte)(value >> 56);
    }

    public static void WriteUInt32LittleEndian(byte[] buffer, int offset, uint value) {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || offset > buffer.Length - 4) throw new ArgumentOutOfRangeException(nameof(offset));

        buffer[offset + 0] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    public static bool TryReadRecord(Stream stream, bool validateChecksums, out byte[] data) {
        data = [];

        byte[] header = new byte[12];
        int headerRead = ReadAtLeast(stream, header, 0, header.Length, 1);
        if (headerRead == 0) return false;

        ReadExactly(stream, header, headerRead, header.Length - headerRead);

        ulong length = ReadUInt64LittleEndian(header, 0);
        uint maskedLengthCrc = ReadUInt32LittleEndian(header, 8);

        if (length > int.MaxValue) throw new InvalidDataException($"TFRecord length {length} is too large.");

        data = new byte[(int)length];
        ReadExactly(stream, data, 0, data.Length);

        byte[] trailer = new byte[4];
        ReadExactly(stream, trailer, 0, trailer.Length);
        uint maskedDataCrc = ReadUInt32LittleEndian(trailer, 0);

        if (validateChecksums) {
            byte[] lengthBytes = new byte[8];
            WriteUInt64LittleEndian(lengthBytes, 0, length);
            uint expectedLengthCrc = MaskedCrc(lengthBytes);
            if (maskedLengthCrc != expectedLengthCrc) {
                throw new InvalidDataException("TFRecord header CRC mismatch.");
            }

            uint expectedDataCrc = MaskedCrc(data);
            if (maskedDataCrc != expectedDataCrc) {
                throw new InvalidDataException("TFRecord data CRC mismatch.");
            }
        }

        return true;
    }

    public static void WriteRecord(Stream stream, byte[] data, int offset, int count) {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (data is null) throw new ArgumentNullException(nameof(data));
        if ((uint)offset > (uint)data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)count > (uint)(data.Length - offset)) throw new ArgumentOutOfRangeException(nameof(count));

        byte[] header = new byte[12];
        WriteUInt64LittleEndian(header, 0, (ulong)count);
        WriteUInt32LittleEndian(header, 8, MaskedCrc(header, 0, 8));
        stream.Write(header, 0, header.Length);

        if (count != 0) {
            stream.Write(data, offset, count);
        }

        byte[] trailer = new byte[4];
        WriteUInt32LittleEndian(trailer, 0, MaskedCrc(data, offset, count));
        stream.Write(trailer, 0, trailer.Length);
    }

    private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count) {
        int readTotal = 0;
        while (readTotal < count) {
            int read = stream.Read(buffer, offset + readTotal, count - readTotal);
            if (read == 0) throw new EndOfStreamException();
            readTotal += read;
        }
    }

    private static int ReadAtLeast(Stream stream, byte[] buffer, int offset, int count, int minBytes) {
        int readTotal = 0;
        while (readTotal < minBytes) {
            int read = stream.Read(buffer, offset + readTotal, count - readTotal);
            if (read == 0) return readTotal;
            readTotal += read;
        }
        return readTotal;
    }
}