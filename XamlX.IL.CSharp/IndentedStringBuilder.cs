// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// The MIT License (MIT)
// 
// Copyright (c) .NET Foundation and Contributors
// 
// All rights reserved.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Avalonia.Generators.Common;

/// <summary>
///     <para>
///         A thin wrapper over <see cref="StringBuilder" /> that adds indentation to each line built.
///     </para>
/// </summary>
public class IndentedStringBuilder(byte indentSize = 4, int indent = 0)
{
    private int _indent = indent;
    private bool _indentPending = true;

    private readonly StringBuilder _stringBuilder = new();

    /// <summary>
    ///     Gets the current indent level.
    /// </summary>
    /// <value>The current indent level.</value>
    public virtual int IndentCount
        => _indent;

    /// <summary>
    ///     The current length of the built string.
    /// </summary>
    public virtual int Length
        => _stringBuilder.Length;

    public virtual IndentedStringBuilder Append(char value, int repeatCount)
    {
        DoIndent();

        _stringBuilder.Append(value, repeatCount);

        return this;
    }
    
    /// <summary>
    ///     Appends the current indent and then the given string to the string being built.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Append(ReadOnlySpan<char> value)
    {
        if (value.Length > 0)
        {
            DoIndent();
        }

        _stringBuilder.Append(value);

        return this;
    }

    /// <summary>
    ///     Appends the current indent and then the given string to the string being built.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Append(string? value)
    {
        if (value?.Length > 0)
        {
            DoIndent();
        }

        _stringBuilder.Append(value);

        return this;
    }

    /// <summary>
    ///     Appends the current indent and then the given string to the string being built.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Append(FormattableString value)
    {
        if (value.Format.Length > 0)
        {
            DoIndent();
        }

        _stringBuilder.Append(value);

        return this;
    }

    /// <summary>
    ///     Appends the current indent and then the given char to the string being built.
    /// </summary>
    /// <param name="value">The char to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Append(char value)
    {
        DoIndent();

        _stringBuilder.Append(value);

        return this;
    }

    /// <summary>
    ///     Appends the current indent and then the given strings to the string being built.
    /// </summary>
    /// <param name="value">The strings to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Append(IEnumerable<string> value)
    {
        DoIndent();

        foreach (var str in value)
        {
            _stringBuilder.Append(str);
        }

        return this;
    }

    /// <summary>
    ///     Appends the current indent and then the given chars to the string being built.
    /// </summary>
    /// <param name="value">The chars to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Append(IEnumerable<char> value)
    {
        DoIndent();

        foreach (var chr in value)
        {
            _stringBuilder.Append(chr);
        }

        return this;
    }

    /// <summary>
    ///     Appends a new line to the string being built.
    /// </summary>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendLine()
    {
        _stringBuilder.AppendLine();

        _indentPending = true;

        return this;
    }

    /// <summary>
    ///     Appends the current indent, the given string, and a new line to the string being built.
    /// </summary>
    /// <remarks>
    ///     If the given string itself contains a new line, the part of the string after that new line will not be indented.
    /// </remarks>
    /// <param name="value">The string to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendLine(string value)
    {
        if (value.Length != 0)
        {
            DoIndent();
        }

        _stringBuilder.AppendLine(value);

        _indentPending = true;

        return this;
    }

    /// <summary>
    ///     Appends the current indent, the given string, and a new line to the string being built.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendLine(FormattableString value)
    {
        DoIndent();

        _stringBuilder.Append(value);

        _indentPending = true;

        return this;
    }

    /// <summary>
    ///     Separates the given string into lines, and then appends each line, prefixed
    ///     by the current indent and followed by a new line, to the string being built.
    /// </summary>
    /// <param name="value">The string to append.</param>
    /// <param name="skipFinalNewline">If <see langword="true" />, then the terminating new line is not added after the last line.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendLines(string value, bool skipFinalNewline = false)
    {
        using (var reader = new StringReader(value))
        {
            var first = true;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    AppendLine();
                }

                if (line.Length != 0)
                {
                    Append(line);
                }
            }
        }

        if (!skipFinalNewline)
        {
            AppendLine();
        }

        return this;
    }

    /// <summary>
    ///     Concatenates the members of the given collection, using the specified separator between each member,
    ///     and then appends the resulting string,
    /// </summary>
    /// <param name="values">The values to concatenate.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendJoin(
        IEnumerable<string> values,
        string separator = ", ")
    {
        DoIndent();

        _stringBuilder.AppendJoin(separator, values);

        return this;
    }

#if NET10_0_OR_GREATER
    /// <summary>
    ///     Concatenates the members of the given collection, using the specified separator between each member,
    ///     and then appends the resulting string,
    /// </summary>
    /// <param name="values">The values to concatenate.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendJoin(
        string separator,
        params ReadOnlySpan<string> values)
    {
        DoIndent();

        _stringBuilder.AppendJoin(separator, values);

        return this;
    }
#else
    /// <summary>
    ///     Concatenates the members of the given collection, using the specified separator between each member,
    ///     and then appends the resulting string,
    /// </summary>
    /// <param name="values">The values to concatenate.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder AppendJoin(
        string separator,
        params string[] values)
    {
        DoIndent();

        _stringBuilder.AppendJoin(separator, values);

        return this;
    }
#endif

    /// <summary>Appends the specified interpolated string to this instance.</summary>
    /// <param name="handler">The interpolated string to append.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedStringBuilder Append([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler) => this;

    /// <summary>Appends the specified interpolated string to this instance.</summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="handler">The interpolated string to append.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedStringBuilder Append(IFormatProvider? provider, [InterpolatedStringHandlerArgument("", nameof(provider))] ref AppendInterpolatedStringHandler handler) => this;

    /// <summary>Appends the specified interpolated string followed by the default line terminator to the end of the current StringBuilder object.</summary>
    /// <param name="handler">The interpolated string to append.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedStringBuilder AppendLine([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler)
    {
        _stringBuilder.AppendLine();

        _indentPending = true;

        return this;
    }

    /// <summary>Appends the specified interpolated string followed by the default line terminator to the end of the current StringBuilder object.</summary>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="handler">The interpolated string to append.</param>
    /// <returns>A reference to this instance after the append operation has completed.</returns>
    public IndentedStringBuilder AppendLine(IFormatProvider? provider, [InterpolatedStringHandlerArgument("", nameof(provider))] ref AppendInterpolatedStringHandler handler)
    {
        _stringBuilder.AppendLine();

        _indentPending = true;

        return this;
    }
    
    /// <summary>Provides a handler used by the language compiler to append interpolated strings into <see cref="StringBuilder"/> instances.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [InterpolatedStringHandler]
    public struct AppendInterpolatedStringHandler
    {
        // Implementation note:
        // As this type is only intended to be targeted by the compiler, public APIs eschew argument validation logic
        // in a variety of places, e.g. allowing a null input when one isn't expected to produce a NullReferenceException rather
        // than an ArgumentNullException.

        /// <summary>The associated StringBuilder to which to append.</summary>
        internal readonly IndentedStringBuilder _stringBuilder;
        /// <summary>Optional provider to pass to IFormattable.ToString or ISpanFormattable.TryFormat calls.</summary>
        private readonly IFormatProvider? _provider;
        /// <summary>Whether <see cref="_provider"/> provides an ICustomFormatter.</summary>
        /// <remarks>
        /// Custom formatters are very rare.  We want to support them, but it's ok if we make them more expensive
        /// in order to make them as pay-for-play as possible.  So, we avoid adding another reference type field
        /// to reduce the size of the handler and to reduce required zero'ing, by only storing whether the provider
        /// provides a formatter, rather than actually storing the formatter.  This in turn means, if there is a
        /// formatter, we pay for the extra interface call on each AppendFormatted that needs it.
        /// </remarks>
        private readonly bool _hasCustomFormatter;

        /// <summary>Creates a handler used to append an interpolated string into a <see cref="StringBuilder"/>.</summary>
        /// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="stringBuilder">The associated StringBuilder to which to append.</param>
        /// <remarks>This is intended to be called only by compiler-generated code. Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
        public AppendInterpolatedStringHandler(int literalLength, int formattedCount, IndentedStringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
            _provider = null;
            _hasCustomFormatter = false;
            
            stringBuilder.DoIndent();
        }

        /// <summary>Creates a handler used to translate an interpolated string into a <see cref="string"/>.</summary>
        /// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="stringBuilder">The associated StringBuilder to which to append.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <remarks>This is intended to be called only by compiler-generated code. Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
        public AppendInterpolatedStringHandler(int literalLength, int formattedCount, IndentedStringBuilder stringBuilder, IFormatProvider? provider)
        {
            _stringBuilder = stringBuilder;
            _provider = provider;
            _hasCustomFormatter = provider is not null && HasCustomFormatter(provider);
            
            stringBuilder.DoIndent();
        }
        
        /// <summary>Gets whether the provider provides a custom formatter.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)] // only used in a few hot path call sites
        internal static bool HasCustomFormatter(IFormatProvider provider)
        {
            Debug.Assert(provider is not null);
            Debug.Assert(provider is not CultureInfo || provider.GetFormat(typeof(ICustomFormatter)) is null, "Expected CultureInfo to not provide a custom formatter");
            return
                provider.GetType() != typeof(CultureInfo) && // optimization to avoid GetFormat in the majority case
                provider.GetFormat(typeof(ICustomFormatter)) != null;
        }

        /// <summary>Writes the specified string to the handler.</summary>
        /// <param name="value">The string to write.</param>
        public void AppendLiteral(string value) => _stringBuilder.Append(value);

        #region AppendFormatted
        // Design note:
        // This provides the same set of overloads and semantics as DefaultInterpolatedStringHandler.

        #region AppendFormatted T
        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value)
        {
            // This method could delegate to AppendFormatted with a null format, but explicitly passing
            // default as the format to TryFormat helps to improve code quality in some cases when TryFormat is inlined,
            // e.g. for Int32 it enables the JIT to eliminate code in the inlined method based on a length check on the format.

            if (_hasCustomFormatter)
            {
                // If there's a custom formatter, always use it.
                AppendCustomFormatter(value, format: null);
                return;
            }

            if (value is null)
            {
                return;
            }

            if (value is IFormattable)
            {
                // Check first for IFormattable, even though we'll prefer to use ISpanFormattable, as the latter
                // requires the former.  For value types, it won't matter as the type checks devolve into
                // JIT-time constants.  For reference types, they're more likely to implement IFormattable
                // than they are to implement ISpanFormattable: if they don't implement either, we save an
                // interface check over first checking for ISpanFormattable and then for IFormattable, and
                // if it only implements IFormattable, we come out even: only if it implements both do we
                // end up paying for an extra interface check.

                if (typeof(T).IsEnum)
                {
                    AppendFormattedWithTempSpace(value, 0, format: null);
                }
#if NET7_0_OR_GREATER
                else if (value is ISpanFormattable)
                {
                    AppendFormattedWithTempSpace(value, 0, format: null);
                }
#endif
                else
                {
                    _stringBuilder.Append(((IFormattable)value).ToString(format: null, _provider)); // constrained call avoiding boxing for value types
                }
            }
            else
            {
                _stringBuilder.Append(value.ToString());
            }
        }

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="format">The format string.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value, string? format)
        {
            if (_hasCustomFormatter)
            {
                // If there's a custom formatter, always use it.
                AppendCustomFormatter(value, format);
                return;
            }

            if (value is null)
            {
                return;
            }

            if (value is IFormattable)
            {
                // Check first for IFormattable, even though we'll prefer to use ISpanFormattable, as the latter
                // requires the former.  For value types, it won't matter as the type checks devolve into
                // JIT-time constants.  For reference types, they're more likely to implement IFormattable
                // than they are to implement ISpanFormattable: if they don't implement either, we save an
                // interface check over first checking for ISpanFormattable and then for IFormattable, and
                // if it only implements IFormattable, we come out even: only if it implements both do we
                // end up paying for an extra interface check.

                if (typeof(T).IsEnum)
                {
                    AppendFormattedWithTempSpace(value, 0, format);
                }
#if NET7_0_OR_GREATER
                else if (value is ISpanFormattable)
                {
                    AppendFormattedWithTempSpace(value, 0, format);
                }
#endif
                else
                {
                    _stringBuilder.Append(((IFormattable)value).ToString(format, _provider)); // constrained call avoiding boxing for value types
                }
            }
            else
            {
                _stringBuilder.Append(value.ToString());
            }
        }

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Minimum number of characters that should be written for this value.  If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value, int alignment) =>
            AppendFormatted(value, alignment, format: null);

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="format">The format string.</param>
        /// <param name="alignment">Minimum number of characters that should be written for this value.  If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        public void AppendFormatted<T>(T value, int alignment, string? format)
        {
            if (alignment == 0)
            {
                // This overload is used as a fallback from several disambiguation overloads, so special-case 0.
                AppendFormatted(value, format);
            }
            else if (alignment < 0)
            {
                // Left aligned: format into the handler, then append any additional padding required.
                int start = _stringBuilder.Length;
                AppendFormatted(value, format);
                int paddingRequired = -alignment - (_stringBuilder.Length - start);
                if (paddingRequired > 0)
                {
                    _stringBuilder.Append(' ', paddingRequired);
                }
            }
            else
            {
                // Right aligned: format into temporary space and then copy that into the handler, appropriately aligned.
                AppendFormattedWithTempSpace(value, alignment, format);
            }
        }

        /// <summary>Formats into temporary space and then appends the result into the StringBuilder.</summary>
        private void AppendFormattedWithTempSpace<T>(T value, int alignment, string? format)
        {
            // It's expected that either there's not enough space in the current chunk to store this formatted value,
            // or we have a non-0 alignment that could require padding inserted. So format into temporary space and
            // then append that written span into the StringBuilder: StringBuilder.Append(span) is able to split the
            // span across the current chunk and any additional chunks required.

#if NET10_0_OR_GREATER
            var handler = new DefaultInterpolatedStringHandler(0, 0, _provider, stackalloc char[256]);
            handler.AppendFormatted(value, format);
            AppendFormatted(handler.Text, alignment);
            handler.Clear();
#else
            // .NET 8.0 doesn't expose handler.Text, hacky workaround
            if (alignment == 0)
            {
                _stringBuilder.Append(value is IFormattable formattable
                    ? formattable.ToString(format, _provider) // constrained call avoiding boxing for value types
                    : value?.ToString());
            }
            else
            {
                var handler = new DefaultInterpolatedStringHandler(0, 0, _provider, stackalloc char[256]);
                handler.AppendFormatted(value, format);
                AppendFormatted(handler.ToStringAndClear(), alignment);
            }
#endif
        }
        #endregion

        #region AppendFormatted ReadOnlySpan<char>
        /// <summary>Writes the specified character span to the handler.</summary>
        /// <param name="value">The span to write.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _stringBuilder.Append(value);

        /// <summary>Writes the specified string of chars to the handler.</summary>
        /// <param name="value">The span to write.</param>
        /// <param name="alignment">Minimum number of characters that should be written for this value.  If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
        /// <param name="format">The format string.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
        {
            if (alignment == 0)
            {
                _stringBuilder.Append(value);
            }
            else
            {
                bool leftAlign = false;
                if (alignment < 0)
                {
                    leftAlign = true;
                    alignment = -alignment;
                }

                int paddingRequired = alignment - value.Length;
                if (paddingRequired <= 0)
                {
                    _stringBuilder.Append(value);
                }
                else if (leftAlign)
                {
                    _stringBuilder.Append(value);
                    _stringBuilder.Append(' ', paddingRequired);
                }
                else
                {
                    _stringBuilder.Append(' ', paddingRequired);
                    _stringBuilder.Append(value);
                }
            }
        }
        #endregion

        #region AppendFormatted string
        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        public void AppendFormatted(string? value)
        {
            if (!_hasCustomFormatter)
            {
                _stringBuilder.Append(value);
            }
            else
            {
                AppendFormatted<string?>(value);
            }
        }

        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Minimum number of characters that should be written for this value.  If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
        /// <param name="format">The format string.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) =>
            // Format is meaningless for strings and doesn't make sense for someone to specify.  We have the overload
            // simply to disambiguate between ROS<char> and object, just in case someone does specify a format, as
            // string is implicitly convertible to both. Just delegate to the T-based implementation.
            AppendFormatted<string?>(value, alignment, format);
        #endregion

        #region AppendFormatted object
        /// <summary>Writes the specified value to the handler.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="alignment">Minimum number of characters that should be written for this value.  If the value is negative, it indicates left-aligned and the required minimum is the absolute value.</param>
        /// <param name="format">The format string.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) =>
            // This overload is expected to be used rarely, only if either a) something strongly typed as object is
            // formatted with both an alignment and a format, or b) the compiler is unable to target type to T. It
            // exists purely to help make cases from (b) compile. Just delegate to the T-based implementation.
            AppendFormatted<object?>(value, alignment, format);
        #endregion
        #endregion

        /// <summary>Formats the value using the custom formatter from the provider.</summary>
        /// <param name="value">The value to write.</param>
        /// <param name="format">The format string.</param>
        /// <typeparam name="T">The type of the value to write.</typeparam>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AppendCustomFormatter<T>(T value, string? format)
        {
            // This case is very rare, but we need to handle it prior to the other checks in case
            // a provider was used that supplied an ICustomFormatter which wanted to intercept the particular value.
            // We do the cast here rather than in the ctor, even though this could be executed multiple times per
            // formatting, to make the cast pay for play.
            Debug.Assert(_hasCustomFormatter);
            Debug.Assert(_provider != null);

            var formatter = (ICustomFormatter?)_provider.GetFormat(typeof(ICustomFormatter));
            Debug.Assert(formatter != null, "An incorrectly written provider said it implemented ICustomFormatter, and then didn't");

            if (formatter is not null)
            {
                _stringBuilder.Append(formatter.Format(format, value, _provider));
            }
        }
    }

    /// <summary>
    ///     Resets this builder ready to build a new string.
    /// </summary>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder Clear()
    {
        _stringBuilder.Clear();
        _indent = 0;

        return this;
    }

    /// <summary>
    ///     Increments the indent.
    /// </summary>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder IncrementIndent()
    {
        _indent++;

        return this;
    }

    /// <summary>
    ///     Decrements the indent.
    /// </summary>
    /// <returns>This builder so that additional calls can be chained.</returns>
    public virtual IndentedStringBuilder DecrementIndent()
    {
        if (_indent > 0)
        {
            _indent--;
        }

        return this;
    }

    /// <summary>
    ///     Creates a scoped indenter that will increment the indent, then decrement it when disposed.
    /// </summary>
    /// <returns>An indenter.</returns>
    public virtual Indenter Indent()
        => new Indenter(this);

    /// <summary>
    ///     Temporarily disables all indentation. Restores the original indentation when the returned object is disposed.
    /// </summary>
    /// <returns>An object that restores the original indentation when disposed.</returns>
    public virtual IndentSuspender SuspendIndent()
        => new IndentSuspender(this);

    /// <summary>
    ///     Clones this <see cref="IndentedStringBuilder" />, copying the built string and current indent level.
    /// </summary>
    /// <returns>New instance of <see cref="IndentedStringBuilder" />.</returns>
    public virtual IndentedStringBuilder Clone()
    {
        var result = new IndentedStringBuilder();
        result._stringBuilder.Append(_stringBuilder);
        result._indent = _indent;
        result._indentPending = _indentPending;
        return result;
    }

    /// <summary>
    ///     Returns the built string.
    /// </summary>
    /// <returns>The built string.</returns>
    public override string ToString()
        => _stringBuilder.ToString();

    private void DoIndent()
    {
        if (_indentPending && _indent > 0)
        {
            _stringBuilder.Append(' ', _indent * indentSize);
        }

        _indentPending = false;
    }

    public readonly struct Indenter : IDisposable
    {
        private readonly IndentedStringBuilder _stringBuilder;

        public Indenter(IndentedStringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;

            _stringBuilder.IncrementIndent();
        }

        public void Dispose()
            => _stringBuilder.DecrementIndent();
    }

    public readonly struct IndentSuspender : IDisposable
    {
        private readonly IndentedStringBuilder _stringBuilder;
        private readonly int _indent;

        public IndentSuspender(IndentedStringBuilder stringBuilder)
        {
            _stringBuilder = stringBuilder;
            _indent = _stringBuilder._indent;
            _stringBuilder._indent = 0;
        }

        public void Dispose()
            => _stringBuilder._indent = _indent;
    }
}