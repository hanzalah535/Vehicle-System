namespace MFPS.Internal.Interfaces
{
    public interface IVehicleUpdate
    {
        /// <summary>
        /// This function is called each frame 
        /// WHEN the local player is inside as driver
        /// </summary>
        void WhenInsideUpdateVehicle();

        /// <summary>
        /// This function is called each fixed frame 
        /// WHEN the local player is inside as driver
        /// </summary>
        void WhenInsideFixedUpdateVehicle();
    }
}