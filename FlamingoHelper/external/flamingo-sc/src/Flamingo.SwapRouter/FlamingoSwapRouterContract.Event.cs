using System.ComponentModel;

namespace Flamingo.SwapRouter
{
    partial class FlamingoSwapRouterContract
    {
        /// <summary>
        /// params: message, extend data
        /// </summary>
        [DisplayName("Fault")]
        public static event FaultEvent onFault;

        public delegate void FaultEvent(string message, params object[] paras);
    }
}
