using System;
using ImGuiNET;
using NFMWorld.Mad;

namespace NFMWorld.Mad.UI
{
    public class DebugCameraWindow
    {
        private bool _isOpen = false;
        private float _fov = 90.0f;
        private int _followY = 0;
        private int _followZ = 0;

        public bool IsOpen => _isOpen;

        public void Toggle()
        {
            _isOpen = !_isOpen;
            
            if (_isOpen)
            {
                // init values from Medium
                _fov = InRacePhase.camera.Fov;
                _followY = InRacePhase.PlayerFollowCamera.FollowYOffset;
                _followZ = InRacePhase.PlayerFollowCamera.FollowZOffset;
            }
        }

        public void Render()
        {
            if (!_isOpen) return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(300, 205), ImGuiCond.Always);
            
            bool isOpen = _isOpen;
            if (ImGui.Begin("Debug Camera Settings", ref isOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoNavInputs | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.Spacing();
                
                ImGui.Text("Field of View:");
                if (ImGui.SliderFloat("##FOV", ref _fov, 70.0f, 120.0f, "%.1f°"))
                {
                    InRacePhase.camera.Fov = _fov;
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Follow Y Offset
                ImGui.Text("Follow Y Offset:");
                if (ImGui.SliderInt("##FollowY", ref _followY, -160, 500))
                {
                    InRacePhase.PlayerFollowCamera.FollowYOffset = _followY;
                }
                
                ImGui.Spacing();
                
                // Follow Z Offset
                ImGui.Text("Follow Z Offset:");
                if (ImGui.SliderInt("##FollowZ", ref _followZ, -500, 500))
                {
                    InRacePhase.PlayerFollowCamera.FollowZOffset = _followZ;
                }
                
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
                
                // Reset button
                if (ImGui.Button("Reset to Defaults", new System.Numerics.Vector2(-1, 0)))
                {
                    _fov = 90.0f;
                    _followY = 0;
                    _followZ = 0;
                    InRacePhase.camera.Fov = _fov;
                    InRacePhase.PlayerFollowCamera.FollowYOffset = _followY;
                    InRacePhase.PlayerFollowCamera.FollowZOffset = _followZ;
                }
            }
            ImGui.End();
            
            _isOpen = isOpen;
        }
    }
}
