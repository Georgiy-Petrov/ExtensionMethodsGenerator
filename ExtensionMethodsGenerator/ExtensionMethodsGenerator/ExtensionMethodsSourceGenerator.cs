using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExtensionMethodsGenerator.HelpersForCache;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExtensionMethodsGenerator
{
    /// <summary>
    /// This source generator scans for classes with the [ToExtensionMethod] attribute,
    /// then produces corresponding extension methods for each public/internal static method
    /// in that class which has at least one parameter.
    /// </summary>
    [Generator]
    public class ExtensionMethodsSourceGenerator : IIncrementalGenerator
    {
        /// <summary>
        /// Entry point for the source generator. This method is called by the .NET Compiler Platform (Roslyn).
        /// We register our syntax provider (which looks for classes marked with [ToExtensionMethod])
        /// and then perform the actual generation of new source code.
        /// </summary>
        /// <param name="context">IncrementalGeneratorInitializationContext provided by Roslyn.</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Set up a SyntaxProvider to look for the [ToExtensionMethod] attribute and relevant classes/methods.
            var provider = context.SyntaxProvider.ForAttributeWithMetadataName(
                // Fully-qualified type name for the attribute we care about (ToExtensionMethod).
                typeof(ToExtensionMethod).FullName!,

                // Predicate to filter out nodes that don't match:
                // 1. Must be a class declaration (public or internal).
                // 2. Must have at least one public/internal static method with >=1 parameter.
                (s, _) =>
                {
                    // Attempt to cast the encountered syntax node to a ClassDeclarationSyntax.
                    var classDeclaration = s.TryCast<ClassDeclarationSyntax>();

                    // Check if the class has a public or internal modifier.
                    var classIsPublicOrInternal = classDeclaration?.Modifiers.Any(m =>
                        m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword);

                    // Check if the class has at least one static method that is also public/internal and has parameters.
                    var staticMethodsPresence = classDeclaration?.Members.Where(m =>
                    {
                        var method = m.TryCast<MethodDeclarationSyntax>();

                        // Return true if:
                        //   - The method has 'static' modifier.
                        //   - The method is public/internal.
                        //   - The method has at least one parameter.
                        return method?.Modifiers
                                   .Any(st => st.ToString() == "static") is true
                               && method.Modifiers.Any(
                                   m => m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword)
                               && method.ParameterList.Parameters.Count is not 0;
                    }).Count() is not 0;

                    return classIsPublicOrInternal is true && staticMethodsPresence;
                },

                // Transform step to extract relevant details when the predicate passes.
                (ctx, _) =>
                {
                    // Cast to ClassDeclarationSyntax again for actual data extraction.
                    var classDeclaration = ctx.TargetNode.TryCast<ClassDeclarationSyntax>();

                    // Capture the 'usings' at the top of the file.
                    var usings = ctx.SemanticModel.SyntaxTree.GetCompilationUnitRoot().Usings.ToString();

                    // Extract the namespace for the class.
                    var @namespace = ctx.TargetSymbol.ContainingNamespace.ConstituentNamespaces
                        .SingleOrDefault()?.ToString();

                    // Class name, generics, and constraints.
                    var className = classDeclaration!.Identifier.Text;
                    var classGenerics = classDeclaration.TypeParameterList?.ToString();
                    var classConstraints = classDeclaration.ConstraintClauses.ToString();

                    // Extract generic parameters from the class as a sequence of strings.
                    var classGenericsAsSequence = classDeclaration.TypeParameterList?.Parameters
                        .Select(p => p.Identifier.Text)
                        .ToList();

                    // Gather all eligible static methods from the class.
                    var staticMethods = classDeclaration.TryCast<ClassDeclarationSyntax>()?.Members
                        .Where(m =>
                        {
                            var method = m.TryCast<MethodDeclarationSyntax>();

                            // Check if method is static and has at least one parameter.
                            return method?.Modifiers
                                       .Any(st => st.ToString() == "static") is true 
                                   && method.Modifiers.Any(
                                       m => m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword) 
                                   && method.ParameterList.Parameters.Count is not 0;
                        })
                        .Select(m =>
                        {
                            var method = m.TryCast<MethodDeclarationSyntax>();

                            // Collect method-specific information for code generation:
                            var methodName = method!.Identifier.Text;

                            // Convert parameter list to strings (type + name).
                            var methodParameters = method.ParameterList.Parameters
                                .Select(p => p.ToString())
                                .ToEquatableArray();

                            // Capture any constraint clauses for this method (e.g., where T : new()).
                            var methodConstraints = method.ConstraintClauses.ToString();

                            // Generic parameters, e.g., <T, U> from 'void MyMethod<T, U>(…)'.
                            var methodGenericParameters = method.TypeParameterList?.ToString();

                            // Accessibility (public/internal).
                            var accessibilityModifier = method.Modifiers.First(m =>
                                m.Kind() is SyntaxKind.PublicKeyword or SyntaxKind.InternalKeyword).ToString();

                            // Return type of the method.
                            var returnType = method.ReturnType.ToString();

                            return new MethodInfo(
                                methodName,
                                methodParameters,
                                methodGenericParameters,
                                methodConstraints,
                                returnType,
                                accessibilityModifier
                            );
                        });

                    // If the class is generic, we optionally filter out methods that might cause generic collisions.
                    if (classGenericsAsSequence is not null)
                    {
                        // We'll filter the discovered static methods using certain logic
                        // regarding the type parameters in the method vs. the class.
                        staticMethods = staticMethods?.Where(m =>
                        {
                            // For each parameter, we take the type portion and see if it
                            // conflicts with our class generics or method generics.
                            var parametersTypes = m.Parameters
                                .Select(p => p.Split(' ').First())
                                .ToList();

                            // Gather method generics, if any.
                            var methodGenericsForJoin = m.GenericArguments?
                                .Replace("<", "").Replace(">", "").Split(' ') ?? [];

                            // Combine class and method generics, filtering out duplicates.
                            var joinedGenericsWithUniqueValues =
                                classGenericsAsSequence
                                .Union(methodGenericsForJoin)
                                .Where(s => !(classGenericsAsSequence.Contains(s)
                                              && methodGenericsForJoin.Contains(s)))
                                .ToList();

                            // Check if any parameter type is referencing a generic type or name
                            // that might conflict. If so, exclude the method from generation.
                            return !parametersTypes.Any(t =>
                            {
                                if (t.Contains('<'))
                                {
                                    // If it's a generic type, parse inside <…> for collisions.
                                    var genericTypeParameter = t.Split('<', '>', ',');

                                    foreach (var gtp in genericTypeParameter)
                                    {
                                        return classGenericsAsSequence.Contains(gtp);
                                    }
                                }

                                // If the parameter type is in the union of class & method generics, consider it a conflict.
                                return joinedGenericsWithUniqueValues.Contains(t);
                            });
                        });
                    }

                    // Combine the class info with each method info into a tuple array.
                    var usingsAndMethods = staticMethods?
                        .Select(m => (
                            new ClassInfo(
                                usings,
                                @namespace!,
                                className,
                                classGenerics,
                                classConstraints
                            ),
                            m
                        ))
                        .ToEquatableArray();

                    // Return an array of tuples => (ClassInfo, MethodInfo).
                    return usingsAndMethods;
                }
            );

            // Flatten the array of tuples. Each static method -> single item.
            var methodProvider =
                provider.SelectMany<EquatableArray<(ClassInfo classInfo, MethodInfo methodInfo)>?, (ClassInfo classInfo, MethodInfo methodInfo)>(
                    (array, _) => array!
                );

            // Register source output for each method discovered.
            // For each (ClassInfo, MethodInfo) pair, we'll generate extension method code.
            context.RegisterSourceOutput(methodProvider, GenerateExtensionMethods);
        }

        /// <summary>
        /// This method is invoked once per discovered method and emits the extension method code.
        /// </summary>
        /// <param name="context">SourceProductionContext to handle code emission.</param>
        /// <param name="tuple">Contains <see cref="ClassInfo"/> about the original class and <see cref="MethodInfo"/> about the static method.</param>
        private void GenerateExtensionMethods(SourceProductionContext context, (ClassInfo classInfo, MethodInfo methodInfo) tuple)
        {
            var (classInfo, methodInfo) = tuple;

            // Build a name for the extension class by using the first parameter's type name,
            // capitalizing the first letter, and appending "Extensions".
            // e.g. if first parameter is 'myNamespace.MyType someParam', the result might be 'MyTypeExtensions'.
            var typeAsClassName = methodInfo.Parameters.First()
                .Split([' ']).First().TakeWhile(c => c is not '<').ToArray();
            typeAsClassName[0] = char.ToUpper(typeAsClassName[0]);

            var extensionClassName = new string(typeAsClassName) + "Extensions";

            // Build comma-separated parameter list, e.g., "int x, string y".
            var parameters = string.Join(",", methodInfo.Parameters);

            // Build comma-separated arguments list, e.g., "x, y".
            // Splits each parameter by space and grabs the last part (the parameter name).
            var arguments = string.Join(",", methodInfo.Parameters.Select(p => p.Split([' ']).Last()));

            // We'll transform the class's generic parameters (if any) to avoid collisions.
            // e.g., if the class generics are <T>, we might rename them to <TClassGeneric>.
            var classGenerics = string.Join(
                ",",
                classInfo.Generics?.Replace("<", "").Replace(">", "").Split(',')
                    .Select(ct => ct + "ClassGeneric") ?? []
            );

            // Same for the method's generic parameters, if any.
            var methodGenerics = string.Join(
                ",",
                methodInfo.GenericArguments?.Replace("<", "").Replace(">", "").Split(',') ?? []
            );

            // Combine class+method generics into a single string for the extension method signature.
            // For example: <TClassGeneric, U>.
            var generics = "<" + (classGenerics.Length is not 0 ? classGenerics + ", " : "") + methodGenerics + ">";

            // If we have no generics, remove the angle brackets.
            if (generics == "<>")
            {
                generics = "";
            }

            // Reapply angle brackets to the class generics alone if non-empty.
            classGenerics = classGenerics.Length is not 0 ? "<" + classGenerics + ">" : "";

            // We'll append "ClassGeneric" to each class generic found in constraints to keep them distinct.
            string appendText = "ClassGeneric";

            // Build a regex to match each individual generic (like T or U) from the class generics.
            string pattern = $@"\b({string.Join("|", classInfo.Generics?.Replace("<", "").Replace(">", "").Split(',') ?? [])})\b";

            // Perform a replacement in the class-level constraints, e.g. "where T : struct"
            // becomes "where TClassGeneric : struct".
            var classConstraints = Regex.Replace(classInfo.Constraints, pattern, match => match.Value + appendText);

            // Merge class constraints + method constraints. (Add spacing to separate them.)
            var constraints = classConstraints + " " + methodInfo.Constraints;

            var parametersForFileName = parameters.Split(',').Select(p => p.Split(' ').First()).Aggregate(new StringBuilder(), (builder, s) =>
            {
                var chars = s.ToCharArray();

                chars[0] = char.ToUpper(chars[0]);

                s = new string(chars.Append('_').ToArray());
                
                return builder.Append(s);
            }).ToString().TrimEnd('_');
            
            var returnKeyword = methodInfo.ReturnType == "void" ? "" : "return";
            
            // Start building the actual source code to emit.
            var sourceBuilder = new StringBuilder();

            // Include the 'using' statements from the original file.
            sourceBuilder.Append(classInfo.Usings);
            sourceBuilder.Append("\n");
            sourceBuilder.Append("\n");

            // Here, we hardcode the namespace for the generated extension classes as "ExtensionMethods".
            sourceBuilder.Append("namespace ExtensionMethods;");
            sourceBuilder.Append("\n");
            sourceBuilder.Append("\n");

            // Build the extension class and method signature.
            sourceBuilder.Append($$"""
                               public static partial class {{extensionClassName}}
                               {
                                   {{methodInfo.AccessibilityModifier}} static {{methodInfo.ReturnType}} {{methodInfo.Name}}{{generics}}(this {{parameters}}) {{constraints}}
                                   {
                                      {{returnKeyword}} {{classInfo.Namespace}}.{{classInfo.Name}}{{classGenerics}}.{{methodInfo.Name}}{{methodInfo.GenericArguments}}({{arguments}});
                                   }
                               }
                               """);

            // Emit the source code for this extension method to a .g.cs file.
            context.AddSource($"{classInfo.Name}.{methodInfo.Name}_{parametersForFileName}_Extensions.g.cs", sourceBuilder.ToString());
        }
    }

    /// <summary>
    /// Holds information about the original class from which we'll generate extensions.
    /// </summary>
    internal record ClassInfo(
        string Usings,
        string Namespace,
        string Name,
        string? Generics,
        string Constraints
    )
    {
        public string Usings { get; } = Usings;

        public string Namespace { get; } = Namespace;

        public string Name { get; } = Name;

        public string? Generics { get; } = Generics;

        public string Constraints { get; } = Constraints;
    }

    /// <summary>
    /// Holds information about each relevant static method discovered in the target class.
    /// </summary>
    internal record MethodInfo(
        string Name,
        EquatableArray<string> Parameters,
        string? GenericArguments,
        string? Constraints,
        string ReturnType,
        string AccessibilityModifier
    )
    {
        public string Name { get; } = Name;

        public EquatableArray<string> Parameters { get; } = Parameters;

        public string? GenericArguments { get; } = GenericArguments;

        public string? Constraints { get; } = Constraints;

        public string ReturnType { get; } = ReturnType;

        public string? AccessibilityModifier { get; } = AccessibilityModifier;
    }
}
