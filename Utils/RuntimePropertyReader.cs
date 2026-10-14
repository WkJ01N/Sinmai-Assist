using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SinmaiAssist.Utils;

// Resolve the getter from the loaded assembly. Its result is boxed by reflection,
// so no class/struct assumption about the external return type enters our IL.
internal sealed class RuntimePropertyReader
{
    private readonly Func<Type> _resolveType;
    private readonly string _propertyName;
    private PropertyInfo _property;

    public RuntimePropertyReader(Func<Type> resolveType, string propertyName)
    {
        _resolveType = resolveType;
        _propertyName = propertyName;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public string ReadString()
    {
        if (_property == null)
        {
            var type = _resolveType();
            if (type == null) throw new InvalidOperationException("Information provider is not loaded yet.");
            var property = type.GetProperty(_propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.GetGetMethod() == null)
                throw new MissingMemberException(type.FullName, _propertyName);
            _property = property;
        }
        return _property.GetValue(null, null)?.ToString();
    }
}
