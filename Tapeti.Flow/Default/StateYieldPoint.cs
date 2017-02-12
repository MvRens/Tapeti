namespace Tapeti.Flow.Default
{
    internal class StateYieldPoint : IStateYieldPoint
    {
        public bool StoreState { get; }


        public StateYieldPoint(bool storeState)
        {
            StoreState = storeState;
        }
    }
}
