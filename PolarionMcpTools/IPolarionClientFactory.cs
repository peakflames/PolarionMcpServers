
namespace PolarionMcpTools
{
    public interface IPolarionClientFactory
    {
        [RequiresUnreferencedCode("Uses Polarion API which requires reflection")]
        Task<Result<IPolarionClient>> CreateClientAsync();
        string? ProjectId { get; }

        /// <summary>
        /// The real Polarion project ID for the selected configuration. Valid only after CreateClientAsync succeeds.
        /// </summary>
        string? PolarionProjectId { get; }
    }

}
