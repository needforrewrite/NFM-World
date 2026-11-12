using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;

namespace NFMWorld.Util
{
    public static class ImguiExtensions
    {
        extension(ImGui)
        {
            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(byte* label, ref Vector3 v, float vSpeed, float vMin)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(byte* label, ref Vector3 v, float vSpeed)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(byte* label, ref Vector3 v)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(byte* label, ref Vector3 v, float vSpeed, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(byte* label, ref Vector3 v, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(byte* label, ref Vector3 v, ImGuiSliderFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                float vSpeed,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                byte* label,
                ref Vector3 v,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(string label, ref Vector3 v, float vSpeed, float vMin)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(string label, ref Vector3 v, float vSpeed)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(string label, ref Vector3 v)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(string label, ref Vector3 v, float vSpeed, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(string label, ref Vector3 v, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                float vMax,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, vMax, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(string label, ref Vector3 v, ImGuiSliderFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                float vMin,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, vMin, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                float vSpeed,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, vSpeed, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat3(
                string label,
                ref Vector3 v,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref v))
                {
                    return ImGui.DragFloat3(label, v1, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(byte* label, ref Vector4 v, float vSpeed, float vMin)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(byte* label, ref Vector4 v, float vSpeed)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(byte* label, ref Vector4 v)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(byte* label, ref Vector4 v, float vSpeed, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(byte* label, ref Vector4 v, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(byte* label, ref Vector4 v, ImGuiSliderFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                float vSpeed,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                byte* label,
                ref Vector4 v,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(string label, ref Vector4 v, float vSpeed, float vMin)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(string label, ref Vector4 v, float vSpeed)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(string label, ref Vector4 v)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(string label, ref Vector4 v, float vSpeed, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(string label, ref Vector4 v, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                float vMax,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, vMax, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(string label, ref Vector4 v, ImGuiSliderFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                float vMin,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, vMin, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                float vSpeed,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, vSpeed, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat4(
                string label,
                ref Vector4 v,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref v))
                {
                    return ImGui.DragFloat4(label, v1, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(byte* label, ref Vector2 v, float vSpeed, float vMin)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(byte* label, ref Vector2 v, float vSpeed)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(byte* label, ref Vector2 v)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(byte* label, ref Vector2 v, float vSpeed, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(byte* label, ref Vector2 v, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(byte* label, ref Vector2 v, ImGuiSliderFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                float vSpeed,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                byte* label,
                ref Vector2 v,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(string label, ref Vector2 v, float vSpeed, float vMin)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(string label, ref Vector2 v, float vSpeed)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(string label, ref Vector2 v)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                byte* format
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(string label, ref Vector2 v, float vSpeed, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(string label, ref Vector2 v, byte* format)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, format);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                float vMax,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, vMax, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(string label, ref Vector2 v, ImGuiSliderFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                float vMin,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, vMin, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                float vSpeed,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, vSpeed, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool DragFloat2(
                string label,
                ref Vector2 v,
                byte* format,
                ImGuiSliderFlags flags
            )
            {
                fixed (float* v1 = &Unsafe.As<Vector2, float>(ref v))
                {
                    return ImGui.DragFloat2(label, v1, format, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(byte* label, ref Vector3 col, ImGuiColorEditFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref col))
                {
                    return ImGui.ColorEdit3(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(byte* label, ref Vector3 col)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref col))
                {
                    return ImGui.ColorEdit3(label, v1);
                }
            }


            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(string label, ref Vector3 col, ImGuiColorEditFlags flags)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref col))
                {
                    return ImGui.ColorEdit3(label, v1, flags);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(string label, ref Vector3 col)
            {
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref col))
                {
                    return ImGui.ColorEdit3(label, v1);
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(byte* label, ref Color3 col, ImGuiColorEditFlags flags)
            {
                Vector3 vec = col;
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref vec))
                {
                    var result = ImGui.ColorEdit3(label, v1, flags);
                    col = (Color3)vec;
                    return result;
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(byte* label, ref Color3 col)
            {
                Vector3 vec = col;
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref vec))
                {
                    var result = ImGui.ColorEdit3(label, v1);
                    col = (Color3)vec;
                    return result;
                }
            }


            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(string label, ref Color3 col, ImGuiColorEditFlags flags)
            {
                Vector3 vec = col;
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref vec))
                {
                    var result = ImGui.ColorEdit3(label, v1, flags);
                    col = (Color3)vec;
                    return result;
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit3(string label, ref Color3 col)
            {
                Vector3 vec = col;
                fixed (float* v1 = &Unsafe.As<Vector3, float>(ref vec))
                {
                    var result = ImGui.ColorEdit3(label, v1);
                    col = (Color3)vec;
                    return result;
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit4(byte* label, ref Color col, ImGuiColorEditFlags flags)
            {
                Vector4 vec = col.ToVector4();
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref vec))
                {
                    var result = ImGui.ColorEdit4(label, v1, flags);
                    col = new Color(vec);
                    return result;
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit4(byte* label, ref Color col)
            {
                Vector4 vec = col.ToVector4();
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref vec))
                {
                    var result = ImGui.ColorEdit4(label, v1);
                    col = new Color(vec);
                    return result;
                }
            }


            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit4(string label, ref Color col, ImGuiColorEditFlags flags)
            {
                Vector4 vec = col.ToVector4();
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref vec))
                {
                    var result = ImGui.ColorEdit4(label, v1, flags);
                    col = new Color(vec);
                    return result;
                }
            }

            /// <summary>To be documented.</summary>
            public static unsafe bool ColorEdit4(string label, ref Color col)
            {
                Vector4 vec = col.ToVector4();
                fixed (float* v1 = &Unsafe.As<Vector4, float>(ref vec))
                {
                    var result = ImGui.ColorEdit4(label, v1);
                    col = new Color(vec);
                    return result;
                }
            }
        }
    }
}
