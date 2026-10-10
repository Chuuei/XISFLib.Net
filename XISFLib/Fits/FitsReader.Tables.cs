using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;

namespace XISFLib;

public static partial class FitsReader
{
    private static FitsTable ParseBinaryTable(FitsHeader header, byte[] data, FitsReaderOptions options, CancellationToken cancellationToken)
    {
        int fieldCount = checked((int)header.GetValueOrDefault<long>("TFIELDS"));
        int rowLength = checked((int)header.GetValueOrDefault<long>("NAXIS1"));
        int rowCount = checked((int)header.GetValueOrDefault<long>("NAXIS2"));
        int heapStart = checked((int)header.GetValueOrDefault<long>("THEAP", checked((long)rowLength * rowCount)));
        if (heapStart < checked(rowLength * rowCount) || heapStart > data.Length)
            throw new FitsFormatException("The binary table heap offset is outside the declared heap.");
        if (checked((long)rowCount * fieldCount) > options.MaximumTableCells)
            throw new FitsFormatException("A FITS table exceeds the configured table cell limit.");
        List<FitsTableColumn> columns = [];
        List<BinaryFormat> formats = [];
        for (int field = 1; field <= fieldCount; field++)
        {
            string format = header.GetValueOrDefault<string>($"TFORM{field}") ?? throw new FitsFormatException($"Missing TFORM{field}.");
            formats.Add(ParseBinaryFormat(format));
            columns.Add(new FitsTableColumn(header.GetValueOrDefault<string>($"TTYPE{field}", $"Column{field}")!, format,
                header.GetValueOrDefault<string>($"TUNIT{field}"), header.GetValueOrDefault<double>($"TSCAL{field}", 1),
                header.GetValueOrDefault<double>($"TZERO{field}", 0), header.GetValueOrDefault<long?>($"TNULL{field}")));
        }
        if (formats.Aggregate(0L, (width, format) => checked(width + format.Width)) > rowLength) throw new FitsFormatException("Binary table fields exceed NAXIS1.");
        List<IReadOnlyList<object?>> rows = [];
        for (int row = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int offset = row * rowLength;
            List<object?> cells = [];
            for (int field = 0; field < formats.Count; field++)
            {
                BinaryFormat format = formats[field];
                cells.Add(ReadBinaryValue(data.AsSpan(offset, format.Width), format, data, heapStart, columns[field]));
                offset += format.Width;
            }
            rows.Add(cells);
        }
        return new FitsTable(columns, rows);
    }

    private static BinaryFormat ParseBinaryFormat(string text)
    {
        Match match = BinaryTableFormatRegex().Match(text.Trim().ToUpperInvariant());
        if (!match.Success) throw new FitsFormatException($"Unsupported binary-table format '{text}'.");
        int repeat = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
        char code = match.Groups[2].Value[0];
        char? element = match.Groups[3].Success ? match.Groups[3].Value[0] : null;
        int scalarWidth = code switch { 'L' or 'B' or 'A' => 1, 'X' => 0, 'I' => 2, 'J' or 'E' => 4, 'K' or 'D' or 'C' or 'P' => 8, 'M' or 'Q' => 16, _ => throw new FitsFormatException($"Unsupported binary-table code '{code}'.") };
        int width = code == 'X' ? (repeat + 7) / 8 : checked(repeat * scalarWidth);
        return new BinaryFormat(repeat, code, element, width);
    }

    private static object? ReadBinaryValue(ReadOnlySpan<byte> field, BinaryFormat format, byte[] data, int heapStart, FitsTableColumn column)
    {
        if (format.Code is 'P' or 'Q')
        {
            long count = format.Code == 'P' ? BinaryPrimitives.ReadInt32BigEndian(field) : BinaryPrimitives.ReadInt64BigEndian(field);
            long offset = format.Code == 'P' ? BinaryPrimitives.ReadInt32BigEndian(field[4..]) : BinaryPrimitives.ReadInt64BigEndian(field[8..]);
            if (count < 0 || offset < 0) throw new FitsFormatException("Invalid variable-length array descriptor.");
            BinaryFormat nested = ParseBinaryFormat($"{count}{format.Element ?? 'B'}");
            long position = checked(heapStart + offset);
            if (checked(position + nested.Width) > data.LongLength) throw new FitsFormatException("Variable-length array points outside the table heap.");
            return ReadBinaryValue(data.AsSpan(checked((int)position), nested.Width), nested, data, heapStart, column);
        }
        if (format.Code == 'A') return Encoding.ASCII.GetString(field).TrimEnd();
        if (format.Code == 'L')
        {
            bool[] logical = new bool[format.Repeat];
            for (int index = 0; index < logical.Length; index++) logical[index] = field[index] == 'T';
            return logical.Length == 1 ? logical[0] : logical;
        }
        if (format.Code == 'X')
        {
            bool[] bits = new bool[format.Repeat];
            for (int index = 0; index < bits.Length; index++) bits[index] = (field[index / 8] & (1 << (7 - index % 8))) != 0;
            return bits;
        }
        List<object?> values = [];
        int size = format.Width / format.Repeat;
        for (int index = 0; index < format.Repeat; index++)
        {
            ReadOnlySpan<byte> value = field.Slice(index * size, size);
            object raw;
            if (format.Code == 'B') raw = value[0];
            else if (format.Code == 'I') raw = BinaryPrimitives.ReadInt16BigEndian(value);
            else if (format.Code == 'J') raw = BinaryPrimitives.ReadInt32BigEndian(value);
            else if (format.Code == 'K') raw = BinaryPrimitives.ReadInt64BigEndian(value);
            else if (format.Code == 'E') raw = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(value));
            else if (format.Code == 'D') raw = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(value));
            else if (format.Code == 'C') raw = new Complex(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(value)), BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(value[4..])));
            else if (format.Code == 'M') raw = new Complex(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(value)), BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(value[8..])));
            else throw new FitsFormatException($"Unsupported binary-table code '{format.Code}'.");
            if (raw is IConvertible && format.Code is not ('C' or 'M'))
            {
                bool integerCode = format.Code is 'B' or 'I' or 'J' or 'K';
                if (column.Null.HasValue && integerCode && Convert.ToInt64(raw, CultureInfo.InvariantCulture) == column.Null.Value)
                    values.Add(null);
                else if (column.Scale != 1 || column.Zero != 0)
                    values.Add(Convert.ToDouble(raw, CultureInfo.InvariantCulture) * column.Scale + column.Zero);
                else values.Add(raw);
            }
            else values.Add(raw);
        }
        return Collapse(values.ToArray());
    }

    private static object? Collapse(object?[] values) => values.Length == 1 ? values[0] : ToTypedArray(values);

    private static object ToTypedArray(object?[] values)
    {
        if (values.All(value => value is byte)) return values.Cast<byte>().ToArray();
        if (values.All(value => value is short)) return values.Cast<short>().ToArray();
        if (values.All(value => value is int)) return values.Cast<int>().ToArray();
        if (values.All(value => value is long)) return values.Cast<long>().ToArray();
        if (values.All(value => value is double)) return values.Cast<double>().ToArray();
        if (values.All(value => value is Complex)) return values.Cast<Complex>().ToArray();
        return values;
    }

    private static FitsTable ParseAsciiTable(FitsHeader header, byte[] data, FitsReaderOptions options, CancellationToken cancellationToken)
    {
        int fields = checked((int)header.GetValueOrDefault<long>("TFIELDS"));
        int rowLength = checked((int)header.GetValueOrDefault<long>("NAXIS1"));
        int rowCount = checked((int)header.GetValueOrDefault<long>("NAXIS2"));
        if (checked((long)rowCount * fields) > options.MaximumTableCells)
            throw new FitsFormatException("A FITS table exceeds the configured table cell limit.");
        List<FitsTableColumn> columns = [];
        List<(int Start, int Width, char Code)> layouts = [];
        for (int field = 1; field <= fields; field++)
        {
            string format = header.GetValueOrDefault<string>($"TFORM{field}") ?? throw new FitsFormatException($"Missing TFORM{field}.");
            Match match = AsciiTableFormatRegex().Match(format.Trim().ToUpperInvariant());
            if (!match.Success) throw new FitsFormatException($"Unsupported ASCII-table format '{format}'.");
            layouts.Add((checked((int)header.GetValueOrDefault<long>($"TBCOL{field}")) - 1, int.Parse(match.Groups[2].Value), match.Groups[1].Value[0]));
            columns.Add(new FitsTableColumn(header.GetValueOrDefault<string>($"TTYPE{field}", $"Column{field}")!, format, header.GetValueOrDefault<string>($"TUNIT{field}"),
                header.GetValueOrDefault<double>($"TSCAL{field}", 1), header.GetValueOrDefault<double>($"TZERO{field}", 0), header.GetValueOrDefault<long?>($"TNULL{field}")));
        }
        List<IReadOnlyList<object?>> rows = [];
        for (int row = 0; row < rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<object?> cells = [];
            for (int field = 0; field < fields; field++)
            {
                (int start, int width, char code) = layouts[field];
                string value = Encoding.ASCII.GetString(data, row * rowLength + start, width).Trim();
                object? parsed = value.Length == 0 ? null : code switch
                {
                    'A' => value, 'I' => long.Parse(value, CultureInfo.InvariantCulture),
                    _ => double.Parse(value.Replace('D', 'E'), NumberStyles.Float, CultureInfo.InvariantCulture)
                };
                cells.Add(parsed is IConvertible && parsed is not string ? Convert.ToDouble(parsed, CultureInfo.InvariantCulture) * columns[field].Scale + columns[field].Zero : parsed);
            }
            rows.Add(cells);
        }
        return new FitsTable(columns, rows);
    }

}
