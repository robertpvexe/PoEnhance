namespace PoEnhance.App.Infrastructure.Input;

internal interface IPhysicalKeyboardState
{
    bool IsPressed(ushort virtualKey);
}
