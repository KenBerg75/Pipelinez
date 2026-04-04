using System.Globalization;
using System.Reflection;
using System.Text;

namespace Pipelinez.Testing.ApiApproval;

internal static class ApiApprovalTextGenerator
{
    public static string Generate(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var builder = new StringBuilder();
        builder.AppendLine($"Assembly: {assembly.GetName().Name}");

        var exportedTypes = assembly
            .GetExportedTypes()
            .Where(type => !type.IsSpecialName && !IsCompilerGenerated(type))
            .OrderBy(GetTypeSortKey, StringComparer.Ordinal)
            .ToArray();

        foreach (var type in exportedTypes)
        {
            builder.AppendLine();
            AppendType(builder, type);
        }

        return builder.ToString().Replace("\r\n", "\n");
    }

    private static void AppendType(StringBuilder builder, Type type)
    {
        AppendAttributes(builder, type, string.Empty);
        builder.AppendLine(FormatTypeDeclaration(type));

        foreach (var memberSignature in GetMemberSignatures(type))
        {
            builder.Append("  ");
            builder.AppendLine(memberSignature);
        }
    }

    private static IEnumerable<string> GetMemberSignatures(Type type)
    {
        if (IsDelegate(type) || type.IsEnum)
        {
            if (type.IsEnum)
            {
                foreach (var field in type
                             .GetFields(BindingFlags.Public | BindingFlags.Static)
                             .OrderBy(field => field.Name, StringComparer.Ordinal))
                {
                    yield return $"FIELD {FormatField(field)}";
                }
            }

            yield break;
        }

        var members = new List<string>();

        members.AddRange(type
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(ctor => ctor.ToString(), StringComparer.Ordinal)
            .Select(FormatConstructor));

        members.AddRange(type
            .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(field => !field.IsSpecialName)
            .OrderBy(field => field.Name, StringComparer.Ordinal)
            .Select(field => $"FIELD {FormatField(field)}"));

        members.AddRange(type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"PROPERTY {FormatProperty(property)}"));

        members.AddRange(type
            .GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(evt => evt.Name, StringComparer.Ordinal)
            .Select(evt => $"EVENT {FormatEvent(evt)}"));

        members.AddRange(type
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ThenBy(method => method.ToString(), StringComparer.Ordinal)
            .Select(method => $"METHOD {FormatMethod(method)}"));

        foreach (var member in members)
        {
            yield return member;
        }
    }

    private static string FormatTypeDeclaration(Type type)
    {
        if (IsDelegate(type))
        {
            var invoke = type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance)
                         ?? throw new InvalidOperationException($"Delegate type '{type.FullName}' is missing Invoke.");

            return $"{FormatTypeModifiers(type)}delegate {FormatTypeName(type, includeNamespace: true)}({FormatParameters(invoke.GetParameters())}) : {FormatTypeName(invoke.ReturnType, includeNamespace: true)}{FormatGenericConstraints(type)}";
        }

        var kind = type.IsInterface
            ? "interface"
            : type.IsEnum
                ? "enum"
                : type.IsValueType
                    ? "struct"
                    : type.IsAbstract && type.IsSealed
                        ? "static class"
                        : type.IsAbstract
                            ? "abstract class"
                            : "class";

        var declaration = $"{FormatTypeModifiers(type)}{kind} {FormatTypeName(type, includeNamespace: true)}";

        var baseAndInterfaces = new List<string>();

        if (type.BaseType is not null &&
            type.BaseType != typeof(object) &&
            type.BaseType != typeof(ValueType) &&
            !type.IsEnum)
        {
            baseAndInterfaces.Add(FormatTypeName(type.BaseType, includeNamespace: true));
        }

        foreach (var implementedInterface in GetDirectInterfaces(type))
        {
            baseAndInterfaces.Add(FormatTypeName(implementedInterface, includeNamespace: true));
        }

        if (baseAndInterfaces.Count > 0)
        {
            declaration += $" : {string.Join(", ", baseAndInterfaces)}";
        }

        declaration += FormatGenericConstraints(type);
        return declaration;
    }

    private static IEnumerable<Type> GetDirectInterfaces(Type type)
    {
        var allInterfaces = type.GetInterfaces();

        if (type.IsInterface)
        {
            var inheritedInterfaces = allInterfaces
                .SelectMany(@interface => @interface.GetInterfaces())
                .Distinct()
                .ToHashSet();

            return allInterfaces
                .Where(@interface => !inheritedInterfaces.Contains(@interface))
                .OrderBy(interfaceType => interfaceType.FullName, StringComparer.Ordinal);
        }

        var baseInterfaces = type.BaseType?.GetInterfaces() ?? Array.Empty<Type>();
        return allInterfaces
            .Except(baseInterfaces)
            .OrderBy(interfaceType => interfaceType.FullName, StringComparer.Ordinal);
    }

    private static string FormatConstructor(ConstructorInfo constructor)
    {
        var attributes = FormatAttributeList(constructor);
        var prefix = string.IsNullOrWhiteSpace(attributes) ? string.Empty : $"{attributes} ";
        return $"{prefix}CTOR {FormatMemberModifiers(constructor)}{FormatTypeName(constructor.DeclaringType!, includeNamespace: true)}({FormatParameters(constructor.GetParameters())})";
    }

    private static string FormatField(FieldInfo field)
    {
        var prefix = new StringBuilder();
        AppendAttributePrefix(prefix, field);
        prefix.Append(FormatFieldModifiers(field));
        prefix.Append(FormatTypeName(field.FieldType, includeNamespace: true));
        prefix.Append(' ');
        prefix.Append(field.Name);

        if (field.IsLiteral && !field.IsInitOnly)
        {
            prefix.Append(" = ");
            prefix.Append(FormatConstant(field.GetRawConstantValue(), field.FieldType));
        }

        return prefix.ToString();
    }

    private static string FormatProperty(PropertyInfo property)
    {
        var prefix = new StringBuilder();
        AppendAttributePrefix(prefix, property);
        prefix.Append(FormatPropertyModifiers(property));

        if (HasRequiredMemberAttribute(property))
        {
            prefix.Append("required ");
        }

        prefix.Append(FormatTypeName(property.PropertyType, includeNamespace: true));
        prefix.Append(' ');

        if (property.GetIndexParameters().Length > 0)
        {
            prefix.Append("this[");
            prefix.Append(FormatParameters(property.GetIndexParameters()));
            prefix.Append(']');
        }
        else
        {
            prefix.Append(property.Name);
        }

        prefix.Append(" { ");

        if (property.GetMethod?.IsPublic == true)
        {
            prefix.Append("get; ");
        }

        if (property.SetMethod?.IsPublic == true)
        {
            prefix.Append(IsInitOnly(property) ? "init; " : "set; ");
        }

        prefix.Append('}');
        return prefix.ToString().TrimEnd();
    }

    private static string FormatEvent(EventInfo evt)
    {
        var prefix = new StringBuilder();
        AppendAttributePrefix(prefix, evt);
        prefix.Append(FormatEventModifiers(evt));
        prefix.Append(FormatTypeName(evt.EventHandlerType!, includeNamespace: true));
        prefix.Append(' ');
        prefix.Append(evt.Name);
        return prefix.ToString();
    }

    private static string FormatMethod(MethodInfo method)
    {
        var prefix = new StringBuilder();
        AppendAttributePrefix(prefix, method);
        prefix.Append(FormatMemberModifiers(method));
        prefix.Append(FormatTypeName(method.ReturnType, includeNamespace: true));
        prefix.Append(' ');
        prefix.Append(method.Name);

        if (method.IsGenericMethodDefinition)
        {
            prefix.Append('<');
            prefix.Append(string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name)));
            prefix.Append('>');
        }

        prefix.Append('(');
        prefix.Append(FormatParameters(method.GetParameters()));
        prefix.Append(')');
        prefix.Append(FormatGenericConstraints(method));
        return prefix.ToString();
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters)
    {
        return string.Join(", ", parameters.Select(FormatParameter));
    }

    private static string FormatParameter(ParameterInfo parameter)
    {
        var builder = new StringBuilder();

        if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
        {
            builder.Append("params ");
        }

        var parameterType = parameter.ParameterType;
        if (parameterType.IsByRef)
        {
            if (parameter.IsOut)
            {
                builder.Append("out ");
            }
            else if (parameter.IsIn)
            {
                builder.Append("in ");
            }
            else
            {
                builder.Append("ref ");
            }

            parameterType = parameterType.GetElementType()
                            ?? throw new InvalidOperationException($"By-ref parameter '{parameter.Name}' is missing element type.");
        }

        builder.Append(FormatTypeName(parameterType, includeNamespace: true));
        builder.Append(' ');
        builder.Append(parameter.Name);

        if (parameter.HasDefaultValue)
        {
            builder.Append(" = ");
            builder.Append(FormatConstant(parameter.DefaultValue, parameterType));
        }

        return builder.ToString();
    }

    private static string FormatConstant(object? value, Type declaredType)
    {
        if (value is null)
        {
            return "null";
        }

        if (declaredType.IsEnum)
        {
            return $"{FormatTypeName(declaredType, includeNamespace: true)}.{value}";
        }

        return value switch
        {
            string text => $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
            char character => $"'{character}'",
            bool boolean => boolean ? "true" : "false",
            float single => single.ToString(CultureInfo.InvariantCulture) + "F",
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture) + "D",
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture) + "M",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
                 ?? value.ToString()
                 ?? string.Empty
        };
    }

    private static string FormatTypeModifiers(Type type)
    {
        return type.IsNestedPublic ? "public " : "public ";
    }

    private static string FormatMemberModifiers(MethodBase method)
    {
        var modifiers = new List<string> { "public" };

        if (method.IsStatic)
        {
            modifiers.Add("static");
        }
        else if (method.IsAbstract)
        {
            modifiers.Add("abstract");
        }
        else if (method is MethodInfo methodInfo && methodInfo.IsVirtual && methodInfo.GetBaseDefinition() == methodInfo)
        {
            modifiers.Add("virtual");
        }

        return string.Join(" ", modifiers) + " ";
    }

    private static string FormatFieldModifiers(FieldInfo field)
    {
        var modifiers = new List<string> { "public" };

        if (field.IsStatic)
        {
            modifiers.Add("static");
        }

        if (field.IsLiteral && !field.IsInitOnly)
        {
            modifiers.Add("const");
        }
        else if (field.IsInitOnly)
        {
            modifiers.Add("readonly");
        }

        return string.Join(" ", modifiers) + " ";
    }

    private static string FormatPropertyModifiers(PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        var modifiers = new List<string> { "public" };

        if (accessor?.IsStatic == true)
        {
            modifiers.Add("static");
        }

        return string.Join(" ", modifiers) + " ";
    }

    private static string FormatEventModifiers(EventInfo evt)
    {
        var addMethod = evt.AddMethod;
        var modifiers = new List<string> { "public", "event" };

        if (addMethod?.IsStatic == true)
        {
            modifiers.Insert(1, "static");
        }

        return string.Join(" ", modifiers) + " ";
    }

    private static string FormatTypeName(Type type, bool includeNamespace)
    {
        if (type.IsByRef)
        {
            return FormatTypeName(type.GetElementType()!, includeNamespace);
        }

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!, includeNamespace)}[]";
        }

        if (type.IsPointer)
        {
            return $"{FormatTypeName(type.GetElementType()!, includeNamespace)}*";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (TryGetBuiltInTypeAlias(type, out var alias))
        {
            return alias;
        }

        if (type.IsNested)
        {
            var declaringTypeName = FormatTypeName(type.DeclaringType!, includeNamespace);
            var ownGenericArguments = GetOwnGenericArguments(type);
            return $"{declaringTypeName}.{StripArity(type.Name)}{FormatGenericArguments(ownGenericArguments, includeNamespace)}";
        }

        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();
            var namespacePrefix = includeNamespace && !string.IsNullOrWhiteSpace(genericTypeDefinition.Namespace)
                ? genericTypeDefinition.Namespace + "."
                : string.Empty;

            return $"{namespacePrefix}{StripArity(genericTypeDefinition.Name)}{FormatGenericArguments(type.GetGenericArguments(), includeNamespace)}";
        }

        var simpleNamespace = includeNamespace && !string.IsNullOrWhiteSpace(type.Namespace)
            ? type.Namespace + "."
            : string.Empty;

        return simpleNamespace + type.Name;
    }

    private static string FormatGenericArguments(IEnumerable<Type> arguments, bool includeNamespace)
    {
        var argumentList = arguments.ToArray();
        if (argumentList.Length == 0)
        {
            return string.Empty;
        }

        return "<" + string.Join(", ", argumentList.Select(argument => FormatTypeName(argument, includeNamespace))) + ">";
    }

    private static IEnumerable<Type> GetOwnGenericArguments(Type type)
    {
        var allArguments = type.GetGenericArguments();
        if (!type.IsNested)
        {
            return allArguments;
        }

        var declaringArguments = type.DeclaringType?.GetGenericArguments().Length ?? 0;
        return allArguments.Skip(declaringArguments);
    }

    private static string FormatGenericConstraints(Type type)
    {
        return FormatGenericConstraints(type.GetGenericArguments());
    }

    private static string FormatGenericConstraints(MethodInfo method)
    {
        if (!method.IsGenericMethodDefinition)
        {
            return string.Empty;
        }

        return FormatGenericConstraints(method.GetGenericArguments());
    }

    private static string FormatGenericConstraints(IEnumerable<Type> genericArguments)
    {
        var clauses = new List<string>();

        foreach (var argument in genericArguments.Where(argument => argument.IsGenericParameter))
        {
            var constraints = new List<string>();
            var parameterAttributes = argument.GenericParameterAttributes;

            if (parameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
            {
                constraints.Add("class");
            }

            if (parameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                constraints.Add("struct");
            }

            constraints.AddRange(argument
                .GetGenericParameterConstraints()
                .Select(constraint => FormatTypeName(constraint, includeNamespace: true)));

            if (parameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                clauses.Add($"where {argument.Name} : {string.Join(", ", constraints)}");
            }
        }

        return clauses.Count == 0
            ? string.Empty
            : " " + string.Join(" ", clauses);
    }

    private static void AppendAttributes(StringBuilder builder, MemberInfo member, string indent)
    {
        foreach (var attribute in GetRelevantAttributes(member))
        {
            builder.Append(indent);
            builder.AppendLine(attribute);
        }
    }

    private static void AppendAttributePrefix(StringBuilder builder, MemberInfo member)
    {
        var attributes = FormatAttributeList(member);
        if (!string.IsNullOrWhiteSpace(attributes))
        {
            builder.Append(attributes);
            builder.Append(' ');
        }
    }

    private static string FormatAttributeList(MemberInfo member)
    {
        return string.Join(" ", GetRelevantAttributes(member));
    }

    private static IEnumerable<string> GetRelevantAttributes(MemberInfo member)
    {
        foreach (var attribute in member.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == typeof(ObsoleteAttribute).FullName)
            {
                var message = attribute.ConstructorArguments.Count > 0
                    ? Convert.ToString(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture)
                    : null;

                var error = attribute.NamedArguments
                    .FirstOrDefault(argument => argument.MemberName == nameof(ObsoleteAttribute.IsError))
                    .TypedValue.Value as bool?;

                yield return error.HasValue
                    ? $"[Obsolete(\"{EscapeString(message ?? string.Empty)}\", error: {error.Value.ToString().ToLowerInvariant()})]"
                    : $"[Obsolete(\"{EscapeString(message ?? string.Empty)}\")]";
            }
            else if (attribute.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.ExperimentalAttribute")
            {
                var diagnosticId = attribute.ConstructorArguments.Count > 0
                    ? Convert.ToString(attribute.ConstructorArguments[0].Value, CultureInfo.InvariantCulture)
                    : string.Empty;

                yield return $"[Experimental(\"{EscapeString(diagnosticId ?? string.Empty)}\")]";
            }
        }
    }

    private static bool HasRequiredMemberAttribute(MemberInfo member)
    {
        return member.GetCustomAttributesData()
            .Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute");
    }

    private static bool IsInitOnly(PropertyInfo property)
    {
        return property.SetMethod?.ReturnParameter
            .GetRequiredCustomModifiers()
            .Any(modifier => modifier.FullName == "System.Runtime.CompilerServices.IsExternalInit") == true;
    }

    private static bool IsDelegate(Type type)
    {
        return type.BaseType == typeof(MulticastDelegate);
    }

    private static bool IsCompilerGenerated(MemberInfo member)
    {
        return member.GetCustomAttributesData()
            .Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }

    private static string GetTypeSortKey(Type type)
    {
        return type.FullName ?? type.Name;
    }

    private static string StripArity(string name)
    {
        var marker = name.IndexOf('`');
        return marker >= 0 ? name[..marker] : name;
    }

    private static bool TryGetBuiltInTypeAlias(Type type, out string alias)
    {
        alias = type.FullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Char" => "char",
            "System.Decimal" => "decimal",
            "System.Double" => "double",
            "System.Single" => "float",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Object" => "object",
            "System.String" => "string",
            _ => string.Empty
        };

        return alias.Length > 0;
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
