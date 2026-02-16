using System;

namespace AutoMinePactify.Services;

/// <summary>
/// Provides safety checks during mining operations.
/// Since we operate as an external overlay (no game memory access),
/// safety is limited to ensuring Minecraft is still focused and
/// integrating anti-fall (sneaking) into patterns.
/// </summary>
public class SafetyService
{
    private readonly MinecraftWindowService _mcService;
    private readonly InputSimulator _inputSimulator;

    public SafetyService(MinecraftWindowService mcService, InputSimulator inputSimulator)
    {
        _mcService = mcService;
        _inputSimulator = inputSimulator;
    }

    /// <summary>
    /// Checks if it's safe to continue mining.
    /// Returns false if Minecraft is no longer detected or focused.
    /// </summary>
    public bool IsSafeToContinue()
    {
        if (!_mcService.IsMinecraftDetected)
            return false;

        if (!_mcService.IsMinecraftFocused())
            return false;

        return true;
    }

    /// <summary>
    /// Performs an emergency stop: releases all keys and mouse buttons.
    /// </summary>
    public void EmergencyRelease()
    {
        _inputSimulator.ReleaseAllKeys();
    }

    /// <summary>
    /// Attempts to re-focus Minecraft if it lost focus.
    /// Returns true if successfully re-focused.
    /// </summary>
    public bool TryRefocusMinecraft()
    {
        if (!_mcService.IsMinecraftDetected)
            return false;

        return _mcService.BringToForeground();
    }
}
