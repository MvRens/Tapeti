using System.Threading.Tasks;

namespace _07_ParallelizationTest
{
    public interface IMessageParallelization
    {
        Task WaitForBatch();
    }
}
