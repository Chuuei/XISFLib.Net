using System.Numerics;

namespace XISFLib;

internal static class XisfNumericTypes
{
    public static XisfSampleFormat ImageFormat(Type type) => type == typeof(byte) ? XisfSampleFormat.UInt8 :
        type == typeof(ushort) ? XisfSampleFormat.UInt16 : type == typeof(uint) ? XisfSampleFormat.UInt32 :
        type == typeof(ulong) ? XisfSampleFormat.UInt64 : type == typeof(float) ? XisfSampleFormat.Float32 :
        type == typeof(double) ? XisfSampleFormat.Float64 : type == typeof(XisfComplex32) ? XisfSampleFormat.Complex32 :
        type == typeof(Complex) ? XisfSampleFormat.Complex64 : throw new NotSupportedException($"'{type.Name}' is not an XISF image sample type.");

    public static (string Scalar, string Prefix, int Size) Numeric(Type type) => type == typeof(sbyte) ? ("Int8", "I8", 1) :
        type == typeof(byte) ? ("UInt8", "UI8", 1) : type == typeof(short) ? ("Int16", "I16", 2) :
        type == typeof(ushort) ? ("UInt16", "UI16", 2) : type == typeof(int) ? ("Int32", "I32", 4) :
        type == typeof(uint) ? ("UInt32", "UI32", 4) : type == typeof(long) ? ("Int64", "I64", 8) :
        type == typeof(ulong) ? ("UInt64", "UI64", 8) : type == typeof(Int128) ? ("Int128", "I128", 16) :
        type == typeof(UInt128) ? ("UInt128", "UI128", 16) : type == typeof(float) ? ("Float32", "F32", 4) :
        type == typeof(double) ? ("Float64", "F64", 8) : type == typeof(XisfFloat128) ? ("Float128", "F128", 16) :
        type == typeof(XisfComplex32) ? ("Complex32", "C32", 8) : type == typeof(Complex) ? ("Complex64", "C64", 16) :
        type == typeof(XisfComplex128) ? ("Complex128", "C128", 32) : throw new NotSupportedException($"'{type.Name}' is not a supported XISF numeric type.");

    public static string PropertyType(object? value)
    {
        if (value is null or string) return "String";
        if (value is bool) return "Boolean";
        if (value is DateTimeOffset or DateTime) return "TimePoint";
        if (value is decimal) return "Float128";
        if (value is Array array)
        {
            if (array.Rank is not (1 or 2)) throw new NotSupportedException("Properties support only vectors and matrices.");
            return Numeric(array.GetType().GetElementType()!).Prefix + (array.Rank == 1 ? "Vector" : "Matrix");
        }
        return Numeric(value.GetType()).Scalar;
    }
}
