using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// "The pointer" for views that aim with a cursor (the repair bench's
/// inspection, the counter phone): the mouse, or the controller's PadCursor
/// while a pad is in use. Primary = left click / RT. Secondary = right click
/// (a pad's B arrives through PlayerInteractor's layered Back instead).
/// </summary>
public static class GamePointer
{
    /// <summary>True while the controller's on-screen cursor stands in for the mouse.</summary>
    public static bool PadCursorActive => PadCursor.IsActive;

    public static bool Available => PadCursor.IsActive || Mouse.current != null;

    public static Vector2 Position
    {
        get
        {
            if (PadCursor.IsActive) return PadCursor.Position;
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * .5f, Screen.height * .5f);
        }
    }

    public static Vector2 Delta
    {
        get
        {
            if (PadCursor.IsActive) return PadCursor.Delta;
            Mouse mouse = Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        }
    }

    public static bool PrimaryPressed
    {
        get
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
            return PadInput.Pressed(PadButton.RightTrigger) && !PadCursor.PressConsumed;
        }
    }

    public static bool PrimaryHeld
    {
        get
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;
            return PadInput.Held(PadButton.RightTrigger) && !PadCursor.PressConsumed;
        }
    }

    public static bool SecondaryPressed
    {
        get
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }
    }

    /// <summary>Call every frame a view wants the controller cursor on screen.</summary>
    public static void WantCursor() => PadCursor.Request();
}
