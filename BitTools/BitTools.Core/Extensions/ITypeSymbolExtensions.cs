﻿using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.CodeAnalysis
{
    public static class ITypeSymbolExtensions
    {
        public static ITypeSymbol GetUnderlyingTypeSymbol(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            string typeName = type.Name;

            if (typeName == nameof(Task) || typeName == nameof(Nullable))
            {
                INamedTypeSymbol namedTypeSymbol = type as INamedTypeSymbol;

                if (namedTypeSymbol != null && namedTypeSymbol.TypeArguments.Any())
                    return namedTypeSymbol.TypeArguments.Single();
            }

            return type;
        }

        public static bool IsVoid(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            type = type.GetUnderlyingTypeSymbol();

            string typeName = type.Name;

            if (typeName == typeof(void).Name || typeName == nameof(Task))
                return true;
            else
                return false;
        }

        public static bool IsCollectionType(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            type = type.GetUnderlyingTypeSymbol();

            return type.Name != nameof(String) && ((type is IArrayTypeSymbol) || type.Name == "IEnumerable" || type.AllInterfaces.Any(i => i.Name == "IEnumerable"));
        }

        public static bool IsQueryableType(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            type = type.GetUnderlyingTypeSymbol();

            return type.AllInterfaces.Any(i => i.Name == "IQueryable");
        }

        public static bool IsNullable(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            string typeName = type.Name;

            return type.IsValueType == false || typeName == nameof(Nullable);
        }

        /* useArrayForIEnumerableTypes >> For Dto props & method parameters >> True , for return values of methods >> False */

        public static string GetEdmElementTypeName(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            ITypeSymbol elementType = type.GetElementType();

            return elementType.GetEdmTypeName(useArrayForIEnumerableTypes: true);
        }

        public static string GetEdmTypeName(this ITypeSymbol type, bool useArrayForIEnumerableTypes)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            type = type.GetUnderlyingTypeSymbol();

            string typeName = type.Name;

            if (typeName == nameof(String))
                return "Edm.String";
            else if (typeName == nameof(DateTimeOffset))
                return "Edm.DateTimeOffset";
            else if (typeName == nameof(Boolean))
                return "Edm.Boolean";
            if (typeName == nameof(Int16))
                return "Edm.Int16";
            if (typeName == nameof(Int32))
                return "Edm.Int32";
            if (typeName == nameof(Int64))
                return "Edm.Int64";
            if (typeName == nameof(Guid))
                return "Edm.Guid";
            if (typeName == nameof(Decimal))
                return "Edm.Decimal";
            if (typeName == nameof(Single))
                return "Edm.Single";
            else
            {
                if (type.IsQueryableType() || type.IsCollectionType())
                    return useArrayForIEnumerableTypes ? "Array" : "$data.Queryable";
                return type.ToDisplayString();
            }
        }

        public static string GetTypeScriptElementTypeName(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            ITypeSymbol elementType = type.GetElementType();

            return elementType.GetTypescriptTypeName(useArrayForIEnumerableTypes: true);
        }

        public static string GetTypescriptTypeName(this ITypeSymbol type, bool useArrayForIEnumerableTypes)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            type = type.GetUnderlyingTypeSymbol();

            string typeName = type.Name;

            if (typeName == nameof(String))
                return "string";
            else if (typeName == nameof(DateTimeOffset))
                return "Date";
            else if (typeName == nameof(Boolean))
                return "boolean";
            if (typeName == nameof(Int16))
                return "number";
            if (typeName == nameof(Int32))
                return "number";
            if (typeName == nameof(Int64))
                return "string";
            if (typeName == nameof(Guid))
                return "string";
            if (typeName == nameof(Decimal))
                return "string";
            if (typeName == nameof(Single))
                return "string";
            else
            {
                if (type.IsQueryableType() || type.IsCollectionType())
                    return (useArrayForIEnumerableTypes ? "Array" : "$data.Queryable") + $"<{type.GetTypeScriptElementTypeName()}>";
                return type.ToDisplayString();
            }
        }

        public static string GetJavaScriptDefaultValue(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.IsCollectionType())
                return "[]";

            return "null";
        }

        public static ITypeSymbol GetUnderlyingComplexType(this ITypeSymbol symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            symbol = symbol.GetUnderlyingTypeSymbol();

            if (symbol.IsCollectionType())
            {
                symbol = symbol.GetElementType();
            }

            return symbol;
        }

        public static bool IsComplexType(this ITypeSymbol symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            symbol = symbol.GetUnderlyingComplexType();

            return symbol.GetAttributes().Any(att => att.AttributeClass.Name == "ComplexTypeAttribute");
        }

        public static bool IsDto(this ITypeSymbol symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            if (symbol is ITypeParameterSymbol)
                return ((ITypeParameterSymbol)symbol).ConstraintTypes.Any(t => t.IsDto());

            return symbol.TypeKind == TypeKind.Class && IsDto(symbol.AllInterfaces);
        }

        private static bool IsDto(ImmutableArray<INamedTypeSymbol> typeInterfaces)
        {
            foreach (ITypeSymbol tInterface in typeInterfaces)
            {
                if (tInterface.Name == "IDto")
                    return true;

                bool isDto = IsDto(tInterface.AllInterfaces);

                if (isDto == true)
                    return true;
            }

            return false;
        }

        public static ITypeSymbol GetElementType(this ITypeSymbol type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            ITypeSymbol elementType = type.GetUnderlyingTypeSymbol();

            if (!elementType.IsCollectionType())
                throw new InvalidOperationException("type is not a collection type");

            if (elementType is IArrayTypeSymbol)
                return ((IArrayTypeSymbol)elementType).ElementType;

            if (elementType is INamedTypeSymbol && ((INamedTypeSymbol)elementType).TypeArguments.Any())
                elementType = ((INamedTypeSymbol)elementType).TypeArguments.Single();

            return elementType;
        }

        public static bool IsEnum(this ITypeSymbol symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            return symbol.GetUnderlyingTypeSymbol()?.BaseType?.Name == "Enum";
        }
    }
}
