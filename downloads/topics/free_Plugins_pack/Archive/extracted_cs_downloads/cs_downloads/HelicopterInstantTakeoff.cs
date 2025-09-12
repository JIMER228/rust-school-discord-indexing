namespace Oxide.Plugins
{
     [Info("Helicopter Instant Takeoff", "Scrooge", "")]
    [Description("Allows helicopters to instantly takeoff from the ground.")]
    class HelicopterInstantTakeoff : RustPlugin
    {
        object OnEngineStart(Minicopter heli)
        {
            if (!heli.IsGrounded()) return null;
            heli.engineController.FinishStartingEngine();
            return false;
        }
    }
}
