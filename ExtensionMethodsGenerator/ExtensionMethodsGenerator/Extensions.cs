using System;
using System.Collections.Generic;
using System.Linq;
using ExtensionMethodsGenerator.HelpersForCache;
using Microsoft.CodeAnalysis;

namespace ExtensionMethodsGenerator;

internal static class Extensions
{
    internal static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T> collection) where T : IEquatable<T>?
    {
        return new EquatableArray<T>(collection.ToArray());
    }

    internal static TResult? TryCast<TResult>(this object @object) where TResult : class
    {
        return @object as TResult;
    }
    
    internal static TResult? TryFindFirstNode<TResult>(this SyntaxNode node) where TResult : class
    {
        // Check if the node is of the type we're looking for
        if (node is TResult result)
        {
            return result; // Stop traversal here if this is the target
        }

        // Recursively traverse each child node
        foreach (var child in node.ChildNodes())
        {
            return TryFindFirstNode<TResult>(child); // Recursive call for each child node
        }

        return null;
    }
}