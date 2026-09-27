namespace NFMWorld.DriverInterface;

public static class KeyExtensions
{
    extension(Key key)
    {
        public bool IsShiftKey(Key keyCode)
        {
            return (key & Key.Modifiers) == Key.Shift && (key & Key.KeyCode) == keyCode;
        }
        
        public bool IsCtrlKey(Key keyCode)
        {
            return (key & Key.Modifiers) == Key.Control && (key & Key.KeyCode) == keyCode;
        }

        public bool IsAltKey(Key keyCode)
        {
            return (key & Key.Modifiers) == Key.Alt && (key & Key.KeyCode) == keyCode;
        }
        
        public bool IsCtrlShiftKey(Key keyCode)
        {
            return (key & Key.Modifiers) == (Key.Control | Key.Shift) && (key & Key.KeyCode) == keyCode;
        }
        
        public bool IsCtrlShiftAltKey(Key keyCode)
        {
            return (key & Key.Modifiers) == (Key.Control | Key.Shift | Key.Alt) && (key & Key.KeyCode) == keyCode;
        }
    }
}