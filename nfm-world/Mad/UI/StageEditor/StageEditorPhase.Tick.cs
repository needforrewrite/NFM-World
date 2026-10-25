using System.Collections.ObjectModel;
using Hexa.NET.ImGui;
using Maxine.Extensions;
using Maxine.Extensions.Collections;
using NFMWorld.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorld.UI;

public partial class StageEditorPhase
{
    public override void BeginGameTick()
    {
        base.BeginGameTick();
        ActiveTab?.Scene?.OnBeforeUpdate();
    }

    public override void GameTick()
    {

        base.GameTick();

        if (!_isOpen) return;
        if (ActiveTab == null) return;
        
        // Handle camera movement with WASD in first-person flying mode
        if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.Scene)
        {
            float yaw = ActiveTab.CameraYaw * (float)Math.PI / 180f;
            float pitch = ActiveTab.CameraPitch * (float)Math.PI / 180f;
            
            // Calculate forward vector based on camera orientation
            var forward = new Vector3(
                (float)(Math.Cos(pitch) * Math.Sin(yaw)),
                (float)Math.Sin(pitch),
                (float)(Math.Cos(pitch) * Math.Cos(yaw))
            );
            forward = Vector3.Normalize(forward);
            
            // Calculate right vector (perpendicular to forward on XZ plane)
            var right = new Vector3(
                (float)Math.Cos(yaw),
                0,
                -(float)Math.Sin(yaw)
            );
            right = Vector3.Normalize(right);
            
            var up = Vector3.UnitY;
            
            // Move camera position directly (first-person flying); hold Shift to sprint at 3× speed
            float camSpeed = CAMERA_MOVE_SPEED * (_isShiftPressed ? 3f : 1f);
            if (_moveForward)
                ActiveTab.CameraPosition += forward * camSpeed;
            if (_moveBackward)
                ActiveTab.CameraPosition -= forward * camSpeed;
            if (_moveLeft)
                ActiveTab.CameraPosition -= right * camSpeed;
            if (_moveRight)
                ActiveTab.CameraPosition += right * camSpeed;
            if (_moveUp)
                ActiveTab.CameraPosition += up * camSpeed;
            if (_moveDown)
                ActiveTab.CameraPosition -= up * camSpeed;
            
            UpdateCameraPosition();
        }
        else if (ActiveTab.ViewMode == StageEditorTab.ViewModeEnum.TopDown)
        {
            // Pan controls for top-down view (move the look-at point on XZ plane)
            // Keep pan speed consistent across zoom levels.
            var panSpeed = CAMERA_MOVE_SPEED * (_isShiftPressed ? 3f : 1f);
            
            if (_moveForward)
                ActiveTab.TopDownPanPosition += new Vector3(0, 0, panSpeed);
            if (_moveBackward)
                ActiveTab.TopDownPanPosition -= new Vector3(0, 0, panSpeed);
            if (_moveLeft)
                ActiveTab.TopDownPanPosition -= new Vector3(panSpeed, 0, 0);
            if (_moveRight)
                ActiveTab.TopDownPanPosition += new Vector3(panSpeed, 0, 0);
            
            UpdateCameraPosition();
        }
        
        // Update piece transforms in the stage
        if (ActiveTab?.Stage != null)
        {
            // GameTick the renderer so StageObjectGameObjects sync position from piece.Obj
            ActiveTab.StageRenderer?.GameTick();
        }
    }
    
}
