using System;
using System.Buffers;
using System.Runtime.InteropServices;
using Xilium.CefGlue;
using Xilium.CefGlue.BrowserProcess.Handlers;

#nullable enable

namespace NFMWorld.UI.Cef;

/// <summary>
/// Render process handler that sets up the V8 JavaScript context.
/// Injects the nfmw bridge object into the global scope when a JS context is created.
/// </summary>
internal sealed class NfmwRenderProcessHandler : RenderProcessHandler
{
    protected override void OnContextCreated(CefBrowser browser, CefFrame frame, CefV8Context context)
    {
        base.OnContextCreated(browser, frame, context);

        WithErrorHandling(() =>
        {
            using var _ = CefObjectTracker.StartTracking();
            if (context.Enter())
            {
                try
                {
                    var global = context.GetGlobal();
                    const CefV8PropertyAttribute attrs = CefV8PropertyAttribute.ReadOnly |
                                                         CefV8PropertyAttribute.DontDelete;

                    // Register nfmw.call directly on window (flat naming, matches CefMessageRouter pattern).
                    // The JS bridge will call window.__nfmwCall(methodName, jsonPayload).
                    using var func = CefV8Value.CreateFunction("__nfmwCall", new NfmwV8Handler("nfmwCall"));
                    global.SetValue("__nfmwCall", func, attrs);
                }
                finally
                {
                    context.Exit();
                }
            }
        }, frame);
    }

    protected override void OnContextReleased(CefBrowser browser, CefFrame frame, CefV8Context context)
    {
        base.OnContextReleased(browser, frame, context);
    }

    /// <summary>
    /// Handle C# → JS push messages sent via CefProcessMessage from the browser process.
    /// Dispatches to window.__nfmwDispatch(event, data) using V8 interop for binary payloads.
    /// </summary>
    protected override bool OnProcessMessageReceived(CefBrowser browser, CefFrame frame,
        CefProcessId sourceProcess, CefProcessMessage message)
    {
        if (message.Name != "nfmwPush")
            return base.OnProcessMessageReceived(browser, frame, sourceProcess, message);

        var args = message.Arguments;
        if (args == null || args.Count < 1)
        {
            Console.WriteLine($"Invalid nfmwPush message received from browser process: {message.Name}");
            return true;
        }

        var eventName = args.GetString(0);
        var ctx = frame.V8Context;
        if (ctx is not { IsValid: true })
        {
            Console.WriteLine($"V8 context is not valid for frame {frame.Identifier} when dispatching event '{eventName}'");
            return true;
        }

        ctx.Enter();
        try
        {
            var global = ctx.GetGlobal();
            var dispatch = global.GetValue("__nfmwDispatch");
            if (dispatch is not { IsFunction: true })
            {
                Console.WriteLine($"window.__nfmwDispatch is not a function in frame {frame.Identifier} when dispatching event '{eventName}'");
                return true;
            }

            // Build JS arguments: [eventName, payload]
            CefV8Value payload;
            if (args.Count >= 2 && args.GetValueType(1) == CefValueType.Binary)
            {
                // Binary payload → convert to Uint8Array in JS
                
                // Allocate a pooled array to be freed later
                var raw = args.GetBinary(1);
                var pooled = ArrayPool<byte>.Shared.Rent((int)raw.Size);
                raw.GetData(pooled, raw.Size, 0);
                payload = CefV8Value.CreateArrayBuffer(GCHandle.Alloc(pooled, GCHandleType.Pinned).AddrOfPinnedObject(), (ulong)raw.Size, new PooledArrayReleaseCallback());
            }
            else if (args.Count >= 2)
            {
                // JSON string payload
                var json = args.GetString(1);
                payload = CefV8Value.CreateString(json);
            }
            else
            {
                payload = CefV8Value.CreateNull();
            }

            dispatch.ExecuteFunction(global, [CefV8Value.CreateString(eventName), payload]);
        }
        finally
        {
            ctx.Exit();
        }

        return true;
    }

    private class PooledArrayReleaseCallback : CefV8ArrayBufferReleaseCallback
    {
        protected override void ReleaseBuffer(IntPtr buffer)
        {
            // Release the pinned array back to the pool
            var handle = GCHandle.FromIntPtr(buffer);
            if (handle is { IsAllocated: true, Target: byte[] array })
            {
                ArrayPool<byte>.Shared.Return(array);
                handle.Free();
            }
            else
            {
                Console.WriteLine("Attempted to release a buffer that was not allocated or not a byte array.");
            }
        }
    }
}

/// <summary>
/// Handles V8 function calls from JavaScript. Sends the request to the browser
/// process via CefProcessMessage for processing by GameBridge.
/// </summary>
internal sealed class NfmwV8Handler(string functionName) : CefV8Handler
{
    protected override bool Execute(string name, CefV8Value obj, CefV8Value[] arguments,
        out CefV8Value returnValue, out string exception)
    {
        returnValue = CefV8Value.CreateNull();
        exception = string.Empty;

        try
        {
            // Get browser and frame from the current V8 context.
            var ctx = CefV8Context.GetCurrentContext();
            if (ctx == null)
            {
                exception = "No current V8 context";
                return true;
            }

            var browser = ctx.GetBrowser();
            if (browser == null)
            {
                exception = "No browser in current V8 context";
                return true;
            }

            var frameId = ctx.GetFrame().Identifier;
            var frame = browser.GetFrame(frameId);
            if (frame == null || !frame.IsValid)
            {
                exception = "Frame is not valid";
                return true;
            }

            // Build the process message
            var msg = CefProcessMessage.Create(functionName);
            if (msg == null)
            {
                exception = $"Failed to create process message '{functionName}'";
                return true;
            }

            var msgArgs = msg.Arguments;
            msgArgs!.SetSize(arguments.Length);
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsString)
                    msgArgs.SetString(i, arguments[i].GetStringValue());
                else if (arguments[i].IsDouble || arguments[i].IsInt || arguments[i].IsUInt)
                    msgArgs.SetDouble(i, arguments[i].GetDoubleValue());
                else if (arguments[i].IsBool)
                    msgArgs.SetBool(i, arguments[i].GetBoolValue());
                else if (arguments[i].IsNull || arguments[i].IsUndefined)
                    msgArgs.SetNull(i);
                else if (arguments[i].IsObject)
                {
                    var dict = CefDictionaryValue.Create();
                    CefV8JsonObject2DictionaryValue(arguments[i], dict);
                    msgArgs.SetDictionary(i, dict);
                }
                else if (arguments[i].IsArray)
                {
                    var list = CefListValue.Create();
                    CefV8Array2ListValue(arguments[i], list);
                    msgArgs.SetList(i, list);
                }
                else
                {
                    exception = "Invalid argument type";
                    return true;
                }
            }

            frame.SendProcessMessage(CefProcessId.Browser, msg);

            return true;
        }
        catch (Exception ex)
        {
            exception = ex.ToString();
            return false;
        }
    }

    private static void CefV8Array2ListValue(CefV8Value source, CefListValue target)
    {
        System.Diagnostics.Debug.Assert(source.IsArray);

        var arg_length = source.GetArrayLength();
        if (arg_length == 0)
            return;

        // Start with null types in all spaces.
        target.SetSize(arg_length);

        for (var i = 0; i < arg_length; ++i)
        {
            var value = source.GetValue(i);
            if (value.IsBool)
            {
                target.SetBool(i, value.GetBoolValue());
            }
            else if (value.IsInt || value.IsUInt)
            {
                target.SetInt(i, value.GetIntValue());
            }
            else if (value.IsDouble)
            {
                target.SetDouble(i, value.GetDoubleValue());
            }
            else if (value.IsNull)
            {
                target.SetNull(i);
            }
            else if (value.IsString || value.IsDate)
            {
                target.SetString(i, value.GetStringValue());
            }
            else if (value.IsArray)
            {
                var new_list = CefListValue.Create();
                CefV8Array2ListValue(value, new_list);
                target.SetList(i, new_list);
            }
            else if (value.IsObject)
            {
                var new_dictionary = CefDictionaryValue.Create();
                CefV8JsonObject2DictionaryValue(value, new_dictionary);
                target.SetDictionary(i, new_dictionary);
            }
        }
    }

    private static void CefListValue2V8Array(ICefListValue source, CefV8Value target)
    {
        System.Diagnostics.Debug.Assert(target.IsArray);

        var arg_length = source.Count;
        if (arg_length == 0)
            return;

        for (var i = 0; i < arg_length; ++i)
        {
            CefV8Value? new_value = null;

            var type = source.GetValueType(i);
            switch (type)
            {
                case CefValueType.Bool:
                    new_value = CefV8Value.CreateBool(source.GetBool(i));
                    break;
                case CefValueType.Double:
                    new_value = CefV8Value.CreateDouble(source.GetDouble(i));
                    break;
                case CefValueType.Int:
                    new_value = CefV8Value.CreateInt(source.GetInt(i));
                    break;
                case CefValueType.String:
                    new_value = CefV8Value.CreateString(source.GetString(i));
                    break;
                case CefValueType.Null:
                    new_value = CefV8Value.CreateNull();
                    break;
                case CefValueType.List:
                {
                    var list = source.GetList(i);
                    new_value = CefV8Value.CreateArray(list.Count);
                    CefListValue2V8Array(list, new_value);
                }
                    break;
                case CefValueType.Dictionary:
                {
                    var dictionary = source.GetDictionary(i);
                    new_value = CefV8Value.CreateObject();
                    CefDictionaryValue2V8JsonObject(dictionary, new_value);
                }
                    break;
                default:
                    break;
            }

            if (new_value != null)
            {
                target.SetValue(i, new_value);
            }
            else
            {
                target.SetValue(i, CefV8Value.CreateNull());
            }
        }
    }

    private static void CefV8JsonObject2DictionaryValue(CefV8Value source, CefDictionaryValue target)
    {
        System.Diagnostics.Debug.Assert(source.IsObject);

        var keys = source.GetKeys();
        foreach (var key in keys)
        {
            var value = source.GetValue(key);

            if (value.IsBool)
            {
                target.SetBool(key, value.GetBoolValue());
            }
            else if (value.IsDouble)
            {
                target.SetDouble(key, value.GetDoubleValue());
            }
            else if (value.IsInt || value.IsUInt)
            {
                target.SetInt(key, value.GetIntValue());
            }
            else if (value.IsNull)
            {
                target.SetNull(key);
            }
            else if (value.IsString || value.IsDate)
            {
                target.SetString(key, value.GetStringValue());
            }
            else if (value.IsArray)
            {
                var listValue = CefListValue.Create();
                CefV8Array2ListValue(value, listValue);
                target.SetList(key, listValue);
            }
            else if (value.IsObject)
            {
                var dictionaryValue = CefDictionaryValue.Create();
                CefV8JsonObject2DictionaryValue(value, dictionaryValue);
                target.SetDictionary(key, dictionaryValue);
            }
        }
    }

    private static void CefDictionaryValue2V8JsonObject(ICefDictionaryValue source, CefV8Value target)
    {
        System.Diagnostics.Debug.Assert(target.IsObject);

        var keys = source.GetKeys();
        foreach (var key in keys)
        {
            CefV8Value? new_value = null;
            var type = source.GetValueType(key);

            switch (type)
            {
                case CefValueType.Bool:
                    new_value = CefV8Value.CreateBool(source.GetBool(key));
                    break;
                case CefValueType.Double:
                    new_value = CefV8Value.CreateDouble(source.GetDouble(key));
                    break;
                case CefValueType.Int:
                    new_value = CefV8Value.CreateInt(source.GetInt(key));
                    break;
                case CefValueType.String:
                    new_value = CefV8Value.CreateString(source.GetString(key));
                    break;
                case CefValueType.Null:
                    new_value = CefV8Value.CreateNull();
                    break;
                case CefValueType.List:
                {
                    var list = source.GetList(key);
                    new_value = CefV8Value.CreateArray(list.Count);
                    CefListValue2V8Array(list, new_value);
                }
                    break;
                case CefValueType.Dictionary:
                {
                    var dictionary = source.GetDictionary(key);
                    new_value = CefV8Value.CreateObject();
                    CefDictionaryValue2V8JsonObject(dictionary, new_value);
                }
                    break;
                default:
                    break;
            }

            if (new_value != null)
            {
                target.SetValue(key, new_value);
            }
            else
            {
                target.SetValue(key, CefV8Value.CreateNull());
            }
        }
    }
}