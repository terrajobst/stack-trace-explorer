using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Terrajobst.StackTraces
{
    public abstract class CompilationStackTraceWriter : StackTraceWriter
    {
        private readonly ImmutableArray<Compilation> _compilations;

        public CompilationStackTraceWriter(ImmutableArray<Compilation> compilations)
        {
            _compilations = compilations;
        }

        protected override void WriteMethodText(string methodName)
        {
            var method = _compilations.Select(c => Resolve(c, methodName))
                                      .Where(s => s != null)
                                      .FirstOrDefault();

            if (method == null)
            {
                WriteText(methodName);
            }
            else
            {
                var displayParts = method.ToDisplayParts(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

                foreach (var part in displayParts)
                {
                    var partSymbol = part.Symbol;
                    var partText = part.ToString();

                    if (partSymbol == null && method.AssociatedSymbol != null)
                    {
                        // For some reason, accessors don't have symbols

                        if (method.AssociatedSymbol.Kind == SymbolKind.Property ||
                            method.AssociatedSymbol.Kind == SymbolKind.Event)
                        {
                            if (part.Kind == SymbolDisplayPartKind.Keyword &&
                                method.Name == partText + "_" + method.AssociatedSymbol.Name)
                            {
                                partSymbol = method;
                            }
                        }
                    }

                    if (partSymbol != null)
                        WriteSymbol(partText, partSymbol);
                    else
                        WriteText(partText);
                }
            }
        }

        protected abstract void WriteSymbol(string text, ISymbol symbol);

        private static IMethodSymbol Resolve(Compilation compilation, string methodName)
        {
            var parts = methodName.ToString().Replace(".ctor", "#ctor").Split('.');

            var currentContainer = (INamespaceOrTypeSymbol)compilation.Assembly.Modules.Single().GlobalNamespace;

            for (var i = 0; currentContainer != null && i < parts.Length - 1; i++)
            {
                ParseTypeName(parts[i], out var typeOrNamespaceName, out var typeArity);
                currentContainer = currentContainer.GetMembers(typeOrNamespaceName)
                                                   .Where(n => typeArity == 0 ||
                                                               n is INamedTypeSymbol t && t.Arity == typeArity)
                                                   .FirstOrDefault() as INamespaceOrTypeSymbol;
            }

            if (currentContainer == null)
                return null;

            var methodNameAndSignature = parts.Last();
            var name = GetMethodName(methodNameAndSignature);
            var methodArity = GetMethodArity(methodNameAndSignature);
            var parameterTypes = GetMethodParameterTypes(methodNameAndSignature);

            var method = currentContainer.GetMembers(name)
                                         .OfType<IMethodSymbol>()
                                         .Where(m => m.Arity == methodArity)
                                         .Where(m => IsMatch(m, parameterTypes))
                                         .FirstOrDefault();
            return method;
        }

        private static void ParseTypeName(string typeName, out string name, out int arity)
        {
            var backtick = typeName.IndexOf('`');

            if (backtick < 0)
            {
                name = typeName;
                arity = 0;
            }
            else
            {
                name = typeName.Substring(0, backtick);
                var arityText = typeName.Substring(backtick + 1);
                arity = int.Parse(arityText);
            }
        }

        private static string GetMethodName(string methodNameAndSignature)
        {
            var bracket = methodNameAndSignature.IndexOf('[');
            var parenthesis = methodNameAndSignature.IndexOf('(');
            var nameEnd = bracket >= 0 && bracket < parenthesis
                                ? bracket
                                : parenthesis;
            var result = methodNameAndSignature.Substring(0, nameEnd);
            if (result == "#ctor")
                return ".ctor";
            return result;
        }

        private static int GetMethodArity(string methodNameAndSignature)
        {
            var parenthesis = methodNameAndSignature.IndexOf('(');

            var openBracket = methodNameAndSignature.IndexOf('[', 0, parenthesis);
            if (openBracket < 0)
                return 0;

            var closeBracket = methodNameAndSignature.IndexOf(']', 0, parenthesis);
            if (closeBracket < 0)
                return 0;

            var result = 1;
            for (var i = openBracket; i <= closeBracket; i++)
            {
                if (methodNameAndSignature[i] == ',')
                    result++;
            }
            return result;
        }

        private static IReadOnlyList<string> GetMethodParameterTypes(string methodNameAndSignature)
        {
            var openParenthesis = methodNameAndSignature.IndexOf('(');
            var closeParenthesis = methodNameAndSignature.IndexOf(')');
            var signatureStart = openParenthesis + 1;
            var signatureLength = closeParenthesis - signatureStart;
            var signature = methodNameAndSignature.Substring(signatureStart, signatureLength);
            var parameters = signature.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < parameters.Length; i++)
                parameters[i] = parameters[i].Trim();

            var result = new List<string>(parameters.Length);
            foreach (var parameter in parameters)
            {
                var space = parameter.IndexOf(' ');
                var typeName = parameter.Substring(0, space);
                result.Add(typeName);
            }

            return result;
        }

        private static bool IsMatch(IMethodSymbol method, IReadOnlyList<string> parameterTypes)
        {
            if (method.Parameters.Length != parameterTypes.Count)
                return false;

            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var symbolTypeName = GetTypeName(method.Parameters[i]);
                var frameTypename = parameterTypes[i];

                if (symbolTypeName != frameTypename)
                    return false;
            }

            return true;
        }

        private static string GetTypeName(IParameterSymbol symbol)
        {
            var sb = new StringBuilder();
            if (symbol.Type is IArrayTypeSymbol array)
            {
                sb.Append(array.ElementType.MetadataName);
                sb.Append("[]");
            }
            else if (symbol.Type is IPointerTypeSymbol pointer)
            {
                sb.Append(pointer.PointedAtType.MetadataName);
                sb.Append('*');
            }
            else
            {
                sb.Append(symbol.Type.MetadataName);
            }

            if (symbol.RefKind != RefKind.None)
                sb.Append("&");

            return sb.ToString();
        }
    }
}
