using System.Reflection;

namespace PaleyExpressionsUI.ExpressionService;

public static class Tools
{
    public static string GetReturnTypeName(MethodInfo method)
    {
        return GetTypeName(method.ReturnType);
    }

    public static string Signature(MethodInfo method)
    {
        var result = string.Empty;

        var parameters = method.GetParameters();

        foreach(var parameter in parameters)
        {
            if (result.Length > 0)
            {
                result += ", ";
            }

            if(parameter.IsDefined(typeof(ParamArrayAttribute), false))
            {
                result += "params ";
            }

            if(parameter.ParameterType.Name.StartsWith("Func`"))
            {
                result += GetFunc(parameter);
            }
            else
            {
                result += GetTypeName(parameter.ParameterType);
            }

            result += $" {parameter.Name}";
        }

        return result;
    }

    private static string GetFunc(ParameterInfo parameter)
    {
        var result = "Func";

        var parameterType = parameter.ParameterType;
        bool isArray = parameterType.IsArray;

        if (isArray)
        {
            parameterType = parameter.ParameterType.GetElementType()!;
        }

        if (parameterType.IsGenericType)
        {
            var genericTypes = string.Empty;

            result += "<";
            foreach (var generic in parameterType.GetGenericArguments())
            {
                if(genericTypes.Length > 0)
                {
                    genericTypes += ", ";
                }
                
                genericTypes += $"{GetTypeName(generic)}";
            }
            result += genericTypes + ">";
        }

        if(isArray)
        {
            result += "[]";
        }

        return result;
    }

    private static string GetTypeName(Type type)
    {
        var result = type.Name switch
        {
            "Int32" => "int",
            "Int64" => "long",
            "Double" => "double",
            "Single" => "float",
            "Decimal" => "decimal",
            "Boolean" => "bool",
            "String" => "string",
            "Object" => "object",
            _ => type.Name
        };

        // only works for nullable value types, not reference types
        // and calls them Nullable' so will need mork
        if (type.IsValueType && Nullable.GetUnderlyingType(type) != null)
        {
            result += "?";
        }

        //var nullabilityContext = new NullabilityInfoContext();

        //var nullable = nullabilityContext.Create(type);

        return result;
    }
}


