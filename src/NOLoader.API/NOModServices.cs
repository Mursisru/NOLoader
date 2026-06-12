using NOLoader.API.World;

namespace NOLoader.API
{
    public struct NOModServices
    {
        public INOModArrayPool Pool;
        public INOModWorldReader? World;
        public IModExecutionBudgetView? Budget;
    }
}
