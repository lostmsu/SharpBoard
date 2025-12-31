using System;

namespace SharpBoard.Internal;

internal static class Crc32C {
    private const uint POLYNOMIAL = 0x82F63B78u;
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(byte[] data) {
        if (data is null) throw new ArgumentNullException(nameof(data));
        return Compute(data, 0, data.Length);
    }

    public static uint Compute(byte[] data, int offset, int count) {
        if (data is null) throw new ArgumentNullException(nameof(data));
        if ((uint)offset > (uint)data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if ((uint)count > (uint)(data.Length - offset)) throw new ArgumentOutOfRangeException(nameof(count));

        uint crc = 0xFFFF_FFFFu;
        int end = offset + count;
        for (int i = offset; i < end; i++) {
            uint tableIndex = (crc ^ data[i]) & 0xFFu;
            crc = Table[tableIndex] ^ (crc >> 8);
        }

        return crc ^ 0xFFFF_FFFFu;
    }

    private static uint[] CreateTable() {
        uint[] table = new uint[256];
        for (uint i = 0; i < table.Length; i++) {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++) {
                crc = (crc & 1u) != 0 ? POLYNOMIAL ^ (crc >> 1) : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }
}