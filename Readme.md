# ExtensionMethodsSourceGenerator

## What Is It For?

`ExtensionMethodsSourceGenerator` is a Roslyn-based source generator that automatically produces extension methods from classes marked with the `[ToExtensionMethod]` attribute. By scanning your code for public/internal static methods that have at least one parameter, it generates `.g.cs` files containing the corresponding extension methods. This allows you to call those methods in a more fluent, extension-like syntax without manually duplicating code.

All generated extension method classes are marked as `public`, regardless of the original class's accessibility. However, the accessibility of the methods themselves matches the original method modifier (`public` or `internal`).

## How to Use

### Installation~~~~

#### Via NuGet

```bash
Install-Package ExtensionMethodsSourceGenerator
```

#### Via Project Reference

1. Clone or add the `ExtensionMethodsSourceGenerator` project to your solution.
2. Reference it as an analyzer in your consuming project’s `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\ExtensionMethodsSourceGenerator\ExtensionMethodsSourceGenerator.csproj" 
                    OutputItemType="Analyzer" 
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### Adding the `[ToExtensionMethod]` Attribute

Decorate a class with `[ToExtensionMethod]`:

```csharp
[ToExtensionMethod]
public static class MathUtilities
{
    public static int Add(int x, int y) => x + y;
    
    public static string Repeat(string value, int count) 
        => new string(value[0], count);
}
```

When you build, the source generator will create an extension class (or classes) in `.g.cs` files. These will contain extension methods like:

```csharp
namespace ExtensionMethods
{
    public static partial class IntExtensions
    {
        public static int Add(this int x, int y)
        {
            return MathUtilities.Add(x, y);
        }
    }

    public static partial class StringExtensions
    {
        public static string Repeat(this string value, int count)
        {
            return MathUtilities.Repeat(value, count);
        }
    }
}
```

## Specific Conditions for Generation

- **Class-Level Attribute:** The class must be annotated with `[ToExtensionMethod]`.
- **Class Accessibility:** The class should be declared as `public` or `internal`. This determines the accessibility of the generated methods.
- **Static Classes and Methods:** The generator only considers `static` methods.
- **Parameter Requirement:** Methods must have at least one parameter to be a valid candidate for extension method generation.

## File Naming Convention

The generator creates a separate file for each discovered method. The naming pattern is:

```
{ClassName}.{MethodName}_{ParametersForFileName}_Extensions.g.cs
```

Here, `ParametersForFileName` is formed by concatenating the types of the method's parameters, with each type's name separated by underscores (`_`). For example:
- A method with parameters `(int x, int y)` generates a file named `MathUtilities.Add_Int_Int_Extensions.g.cs`.
- A method with parameters `(string value, int count)` generates a file named `MathUtilities.Repeat_String_Int_Extensions.g.cs`.

## Special Cases Handled Automatically

- **Namespace & Usings:** The generator captures your original `using` directives and uses them as needed in the emitted code.
- **Generic Classes & Methods:** Handles generic classes and methods, ensuring constraints (e.g., `where T : class`) are preserved or adjusted to avoid conflicts.
- **Avoiding Generic Collisions:** Ensures no conflicts between class-level and method-level generics by appending suffixes to the class generics when necessary (e.g., `TClassGeneric`).
- **Method Overloads:** Multiple static methods with the same name but different parameter signatures are handled gracefully, each generating a unique file.
- **Async & Task Methods:** Handles methods returning `Task` or `Task<T>` (both with and without `await`).
