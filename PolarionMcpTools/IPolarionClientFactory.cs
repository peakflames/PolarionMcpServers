
namespace PolarionMcpTools
{
    public interface IPolarionClientFactory
    {
        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        Task<Result<IPolarionClient>> CreateClientAsync();
        string? ProjectId { get; }
    }

}
