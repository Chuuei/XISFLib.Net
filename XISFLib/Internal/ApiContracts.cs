using System.Globalization;

namespace XISFLib;

internal static class ApiContracts
{
    public static void Defined<T>(T value, string parameter) where T : struct, Enum
    {
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(parameter, value, "An undefined enum value is not supported.");
    }

    public static T ConvertValue<T>(object value)
    {
        if (value is T typed) return typed;
        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (target.IsEnum) return (T)Enum.Parse(target, Convert.ToString(value, CultureInfo.InvariantCulture)!, true);
        return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
