using System.Threading.Tasks;

namespace AIAgentMvc.Services
{
    public interface IAgent
    {
        Task<string> RespondAsync(string userMessage);
    }
}
